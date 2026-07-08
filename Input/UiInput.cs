using System;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Forms;
using ExileCore;

namespace CurrencyExchange.Input;

/// <summary>Resultado de um passo de input multi-tick.</summary>
public enum StepResult { Working, Done, Failed }

/// <summary>
/// PRIMITIVO DE INPUT unificado — a ÚNICA implementação de clique / Ctrl+click / clique-com-modificador /
/// digitação. Multi-tick (1 fase por frame, pacing interno): NUNCA usa Thread.Sleep, que congela a render
/// thread do HUD (e todos os plugins) durante o hold do clique. Uma instância POR AÇÃO (estado de fase próprio).
///
/// Portado e limpo do projeto antigo (validado in-game). Melhoria vs antigo: sem acoplamento ao contador
/// de rate-limit do CX (isso é preocupação da camada de trade, entra depois via hook) — aqui é só input.
/// </summary>
public sealed class UiInput
{
    private readonly GameController _gc;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _nextAt;
    private int _phase;
    private Vector2 _point;
    private bool _ctrlHeld, _typeKeyHeld, _modHeld;
    private Keys _typeKey, _modKey;
    // Tecla de digitação segurada AGORA (KeyDown num frame, KeyUp no seguinte). Estática porque o
    // ReleaseRiskKeys (chamado antes de todo Escape) é estático e precisa soltá-la — senão um char fica
    // PRESO no SO se a digitação é interrompida no meio.
    private static Keys? _globalHeldTypeKey;

    public string LastError { get; private set; }

    public UiInput(GameController gc) { _gc = gc; }

    private bool Waiting => _clock.ElapsedMilliseconds < _nextAt;
    private void Gap(int min, int max) => _nextAt = _clock.ElapsedMilliseconds + Humanizer.Delay(min, max);

    /// <summary>Clique em 2 fases (mover → assentar → clicar), com jitter humanizado. Chamar todo frame até != Working.</summary>
    public StepResult ClickStep(ExileCore.PoEMemory.Element el, bool right = false, bool jitter = true, int settleMs = 60)
    {
        if (Waiting) return StepResult.Working;
        switch (_phase)
        {
            case 0:
                if (!ResolvePoint(el, jitter)) return FailStep("rect inválido p/ clicar");
                ExileCore.Input.SetCursorPos(_point);
                _phase = 1; Gap(settleMs, settleMs + 40); return StepResult.Working;
            default:
                if (right) ExileCore.Input.Click(MouseButtons.Right); else ExileCore.Input.Click(MouseButtons.Left);
                _phase = 0; return StepResult.Done;
        }
    }

    /// <summary>
    /// Ctrl+click multi-tick SEM sleep: mover → assentar → CtrlDown → (gap p/ o jogo AMOSTRAR o
    /// modificador) → ButtonDown → (gap: down+up instantâneo não é amostrado) → ButtonUp → CtrlUp.
    /// right=true p/ Ctrl+botão-direito.
    /// </summary>
    public StepResult CtrlClickStep(ExileCore.PoEMemory.Element el, bool right = false, bool jitter = true)
    {
        if (Waiting) return StepResult.Working;
        switch (_phase)
        {
            case 0:
                if (!ResolvePoint(el, jitter)) return FailStep("rect inválido p/ Ctrl+click");
                ExileCore.Input.SetCursorPos(_point);
                _phase = 1; Gap(55, 100); return StepResult.Working;
            case 1:
                ExileCore.Input.KeyDown(Keys.LControlKey); _ctrlHeld = true;
                _phase = 2; Gap(60, 100); return StepResult.Working;
            case 2:
                if (right) ExileCore.Input.RightDown(); else ExileCore.Input.LeftDown();
                _phase = 3; Gap(70, 110); return StepResult.Working;
            case 3:
                if (right) ExileCore.Input.RightUp(); else ExileCore.Input.LeftUp();
                _phase = 4; Gap(45, 80); return StepResult.Working;
            default:
                ExileCore.Input.KeyUp(Keys.LControlKey); _ctrlHeld = false;
                _phase = 0; return StepResult.Done;
        }
    }

    /// <summary>
    /// Clique multi-tick NA POSIÇÃO ATUAL do cursor (quem chama já posicionou — ex.: mira coordenada de
    /// mundo clampada), segurando um modificador OPCIONAL (Ctrl p/ interação direta, Move-Only p/ caminhada).
    /// O gap com o mod segurado faz o jogo AMOSTRAR o modificador no clique. Mod solto no MESMO frame do up.
    /// </summary>
    public StepResult ModClickHereStep(Keys? mod, bool right = false)
    {
        if (Waiting) return StepResult.Working;
        switch (_phase)
        {
            case 0:
                if (mod.HasValue)
                {
                    _modKey = mod.Value; ExileCore.Input.KeyDown(_modKey); _modHeld = true;
                    _phase = 1; Gap(60, 90); return StepResult.Working;
                }
                _phase = 1; return StepResult.Working;
            case 1:
                if (right) ExileCore.Input.RightDown(); else ExileCore.Input.LeftDown();
                _phase = 2; Gap(80, 110); return StepResult.Working;
            default:
                if (right) ExileCore.Input.RightUp(); else ExileCore.Input.LeftUp();
                if (_modHeld) { ExileCore.Input.KeyUp(_modKey); _modHeld = false; }
                _phase = 0; return StepResult.Done;
        }
    }

    /// <summary>Clique "humano" (down → gap → up) numa COORDENADA FIXA de tela, sem resolução de rect.</summary>
    public StepResult ClickAtStep(Vector2 point, bool right = false, int settleMs = 60)
    {
        if (Waiting) return StepResult.Working;
        switch (_phase)
        {
            case 0:
                _point = point; ExileCore.Input.SetCursorPos(_point);
                _phase = 1; Gap(settleMs, settleMs + 40); return StepResult.Working;
            case 1:
                if (right) ExileCore.Input.RightDown(); else ExileCore.Input.LeftDown();
                _phase = 2; Gap(75, 110); return StepResult.Working;
            default:
                if (right) ExileCore.Input.RightUp(); else ExileCore.Input.LeftUp();
                _phase = 0; return StepResult.Done;
        }
    }

    /// <summary>
    /// Digita text char-a-char (down num frame, up no seguinte — o jogo engole KeyPress instantâneo).
    /// pos é o cursor do chamador (começa em 0). Chars sem tecla mapeável são PULADOS.
    /// </summary>
    public StepResult TypeStep(string text, ref int pos, int charDelayMs = 35)
    {
        if (Waiting) return StepResult.Working;
        if (text == null || pos >= text.Length) return StepResult.Done;
        if (!_typeKeyHeld)
        {
            var key = CharToKey(text[pos]);
            if (key == null) { pos++; return pos >= text.Length ? StepResult.Done : StepResult.Working; }
            _typeKey = key.Value; ExileCore.Input.KeyDown(_typeKey); _typeKeyHeld = true; _globalHeldTypeKey = _typeKey;
            Gap(charDelayMs, charDelayMs + 25); return StepResult.Working;
        }
        ExileCore.Input.KeyUp(_typeKey); _typeKeyHeld = false; _globalHeldTypeKey = null; pos++;
        Gap(charDelayMs, charDelayMs + 25);
        return pos >= text.Length ? StepResult.Done : StepResult.Working;
    }

    /// <summary>Ctrl+A → Backspace (limpa campo focado). Um frame só.</summary>
    public void ClearField()
    {
        ExileCore.Input.KeyDown(Keys.LControlKey);
        ExileCore.Input.KeyPressRelease(Keys.A);
        ExileCore.Input.KeyUp(Keys.LControlKey);
        ExileCore.Input.KeyPressRelease(Keys.Back);
    }

    /// <summary>Solta as teclas de RISCO no nível do SO (Ctrl/Espaço/R/Alt + botões). Chamar ANTES de qualquer
    /// Escape (Ctrl preso + Esc = menu Iniciar) e ENTRE sub-ações. Idempotente.</summary>
    public static void ReleaseRiskKeys()
    {
        try { ExileCore.Input.LeftUp(); } catch { }
        try { ExileCore.Input.RightUp(); } catch { }
        try { ExileCore.Input.KeyUp(Keys.LControlKey); } catch { }
        try { ExileCore.Input.KeyUp(Keys.Space); } catch { }
        try { ExileCore.Input.KeyUp(Keys.R); } catch { }
        try { ExileCore.Input.KeyUp(Keys.LMenu); } catch { }
        if (_globalHeldTypeKey.HasValue)
        { try { ExileCore.Input.KeyUp(_globalHeldTypeKey.Value); } catch { } _globalHeldTypeKey = null; }
    }

    /// <summary>Solta TUDO que possa ter ficado preso. Chamar em Fail/cancelamento. Idempotente.</summary>
    public void ReleaseAll()
    {
        if (_ctrlHeld) { try { ExileCore.Input.KeyUp(Keys.LControlKey); } catch { } _ctrlHeld = false; }
        if (_typeKeyHeld) { try { ExileCore.Input.KeyUp(_typeKey); } catch { } _typeKeyHeld = false; }
        if (_globalHeldTypeKey.HasValue) { try { ExileCore.Input.KeyUp(_globalHeldTypeKey.Value); } catch { } _globalHeldTypeKey = null; }
        if (_modHeld) { try { ExileCore.Input.KeyUp(_modKey); } catch { } _modHeld = false; }
        try { ExileCore.Input.KeyUp(Keys.LControlKey); } catch { }
        try { ExileCore.Input.KeyUp(Keys.Space); } catch { }
        try { ExileCore.Input.KeyUp(Keys.R); } catch { }
        try { ExileCore.Input.LeftUp(); } catch { }
        try { ExileCore.Input.RightUp(); } catch { }
        _phase = 0;
    }

    /// <summary>Re-projeta o rect DO FRAME ATUAL (nunca cacheia Element entre frames) → ponto de tela + jitter.</summary>
    private bool ResolvePoint(ExileCore.PoEMemory.Element el, bool jitter)
    {
        try
        {
            if (el == null) return false;
            var r = el.GetClientRect();
            if (r.Width <= 0 || r.Height <= 0) return false;
            var p = jitter ? Humanizer.PointIn(r) : new Vector2(r.Center.X, r.Center.Y);
            var win = _gc.Window.GetWindowRectangle();
            _point = new Vector2(p.X + win.X, p.Y + win.Y);
            return true;
        }
        catch { return false; }
    }

    private StepResult FailStep(string why) { LastError = why; ReleaseAll(); return StepResult.Failed; }

    private static Keys? CharToKey(char ch)
    {
        ch = char.ToLowerInvariant(ch);
        if (ch == ' ') return Keys.Space;
        if (ch >= 'a' && ch <= 'z') return (Keys)('A' + (ch - 'a'));
        if (ch >= '0' && ch <= '9') return (Keys)('0' + (ch - '0'));
        return null;   // apóstrofo e demais: NÃO digitados (tecla varia por layout)
    }
}
