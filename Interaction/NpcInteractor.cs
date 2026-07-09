using System;
using System.Numerics;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using CurrencyExchange.Commands;
using CurrencyExchange.Input;
using CurrencyExchange.Navigation;
using CurrencyExchange.World;

namespace CurrencyExchange.Interaction;

/// <summary>
/// Anda até um NPC (por Path) e o ABRE: Ctrl+click esquerdo no ponto de interação (InteractCenterNum)
/// abre o painel DIRETO; se não abrir, FALLBACK pelo menu (clica a opção com o texto dado). Verify polla
/// a fonte tipada (ex.: CurrencyExchangePanel.IsVisible). Decoupled: compõe o `Mover` pra caminhada.
/// </summary>
public sealed class NpcInteractor : IAction
{
    private enum Phase { Walk, Aim, Click, Verify, MenuAim, MenuClick, Done, Err }

    private readonly CommandContext _ctx;
    private readonly string _path;
    private readonly Func<bool> _isOpen;
    private readonly string _menuText;
    private readonly int _arriveCells;
    private readonly UiInput _in;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    private Mover _mover;
    private Phase _phase = Phase.Walk;
    private long _nextAt, _deadline, _clickAt;
    private int _clicks;
    private Element _menuOpt;

    public string Error { get; private set; }
    public object Data { get; private set; }

    public NpcInteractor(CommandContext ctx, string entityPath, Func<bool> isOpen, string menuText = null, int arriveCells = 10)
    { _ctx = ctx; _path = entityPath; _isOpen = isOpen; _menuText = menuText; _arriveCells = arriveCells; _in = new UiInput(ctx.Gc); }

    public ActionStatus Advance()
    {
        if (_phase == Phase.Done) return ActionStatus.Done;
        if (_phase == Phase.Err) return ActionStatus.Failed;
        if (_clock.ElapsedMilliseconds < _nextAt) return ActionStatus.InProgress;

        // já aberto? (idempotência — atalho em qualquer fase)
        if (SafeIsOpen()) return Finish();

        switch (_phase)
        {
            case Phase.Walk:
            {
                _mover ??= new Mover(_ctx, _path, arriveCells: _arriveCells);
                var st = _mover.Advance();
                if (st == ActionStatus.InProgress) return ActionStatus.InProgress;
                if (st == ActionStatus.Failed)
                    _ctx.Log?.Invoke($"[npc] pathfinding não chegou ({_mover.Error}) — tento clique direto");
                _mover = null;
                if (_deadline == 0) _deadline = _clock.ElapsedMilliseconds + 14000;
                _phase = Phase.Aim; Delay(150);
                break;
            }
            case Phase.Aim:   // posiciona o cursor no ponto de interação do NPC (clampado à janela)
            {
                if (!AimAtNpc()) return Fail($"não consegui projetar o NPC '{_path}' na tela");
                _phase = Phase.Click;
                break;
            }
            case Phase.Click: // Ctrl+click multi-tick na posição atual do cursor
            {
                var r = _in.ModClickHereStep(Keys.LControlKey);
                if (r == StepResult.Working) break;
                _clicks++; _clickAt = _clock.ElapsedMilliseconds;
                _phase = Phase.Verify; Delay(250);
                break;
            }
            case Phase.Verify:
            {
                if (SafeIsOpen()) return Finish();
                // fallback menu: clique normal abre o menu do NPC → acha a opção pelo texto
                if (_menuText != null)
                {
                    _menuOpt = FindUiText(_ctx.Gc?.IngameState?.IngameUi, _menuText, 0);
                    if (_menuOpt != null) { _phase = Phase.MenuClick; Delay(120); break; }
                }
                if (_clock.ElapsedMilliseconds >= _deadline)
                    return Fail($"NPC não abriu em ~14s (fora de alcance? bloqueado?)");
                // poll antes de re-clicar (deixa o painel/menu abrir — re-clique cedo FECHA o menu)
                if (_clock.ElapsedMilliseconds - _clickAt >= 2500) { _phase = Phase.Aim; Delay(80); }
                else Delay(150);
                break;
            }
            case Phase.MenuClick:
            {
                var r = _in.ClickStep(_menuOpt);   // re-resolve o rect do elemento a cada frame
                if (r == StepResult.Working) break;
                if (r == StepResult.Failed) { _phase = Phase.Verify; Delay(200); break; }
                _clickAt = _clock.ElapsedMilliseconds; _phase = Phase.Verify; Delay(400);
                break;
            }
        }
        return ActionStatus.InProgress;
    }

    private bool AimAtNpc()
    {
        try
        {
            var ent = _ctx.Finder.FindNearestByPath(_path);
            if (ent == null) return false;
            var render = ent.GetComponent<Render>();
            if (render == null) return false;
            var world = render.InteractCenterNum;   // ponto de INTERAÇÃO (melhor que PosNum p/ abrir)
            var scr = _ctx.Gc.IngameState.Camera.WorldToScreen(world);
            if (float.IsNaN(scr.X) || float.IsNaN(scr.Y)) return false;
            var win = _ctx.Gc.Window.GetWindowRectangle();
            const float mgn = 14f;
            float x = Math.Clamp(scr.X, mgn, Math.Max(mgn, win.Width - mgn));
            float y = Math.Clamp(scr.Y, mgn, Math.Max(mgn, win.Height - mgn));
            ExileCore.Input.SetCursorPos(new Vector2(x + win.X, y + win.Y));
            return true;
        }
        catch { return false; }
    }

    private bool SafeIsOpen() { try { return _isOpen(); } catch { return false; } }

    /// <summary>Acha um elemento VISÍVEL da UI com o texto exato (opção do menu do NPC). Poda ramos invisíveis.</summary>
    private static Element FindUiText(Element el, string text, int depth)
    {
        if (el == null || depth > 14) return null;
        try
        {
            if (!el.IsVisible) return null;
            string t = null; try { t = el.Text?.Trim(); } catch { }
            if (string.Equals(t, text, StringComparison.OrdinalIgnoreCase))
            { var r = el.GetClientRect(); if (r.Width > 0 && r.Height > 0) return el; }
        }
        catch { }
        System.Collections.Generic.IList<Element> ch = null;
        try { ch = el.Children; } catch { }
        if (ch != null) foreach (var c in ch) { var f = FindUiText(c, text, depth + 1); if (f != null) return f; }
        return null;
    }

    private ActionStatus Finish()
    {
        _in.ReleaseAll();
        Data = new { opened = true, clicks = _clicks };
        _ctx.Log?.Invoke($"[npc] '{_path}' aberto ({_clicks} cliques)");
        _phase = Phase.Done; return ActionStatus.Done;
    }

    private ActionStatus Fail(string why)
    { Error = why; _phase = Phase.Err; _in.ReleaseAll(); _ctx.Log?.Invoke($"[npc] FALHA: {why}"); return ActionStatus.Failed; }

    private void Delay(int ms) => _nextAt = _clock.ElapsedMilliseconds + ms;
}
