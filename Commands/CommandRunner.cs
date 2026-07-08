using System;
using System.Collections.Concurrent;
using CurrencyExchange.Input;

namespace CurrencyExchange.Commands;

/// <summary>
/// Coração do executor. Fila thread-safe (qualquer transporte enfileira) + UMA ação por vez, avançada na
/// RENDER THREAD (Advance() todo frame). Nunca toca memória do jogo fora daqui. Ao terminar, publica o
/// resultado (completa o Tcs do transporte) e loga. Solta teclas de risco entre comandos (segurança).
/// </summary>
public sealed class CommandRunner
{
    private readonly ConcurrentQueue<CommandRequest> _queue = new();
    private readonly CommandRegistry _reg;
    private readonly CommandContext _ctx;
    private readonly LogBus _log;

    private CommandRequest _cur;
    private IAction _curAction;

    public CommandRunner(CommandRegistry reg, CommandContext ctx, LogBus log)
    { _reg = reg; _ctx = ctx; _log = log; }

    public string CurrentName => _cur?.Name;
    public int Pending => _queue.Count;

    public void Enqueue(CommandRequest r) => _queue.Enqueue(r);

    /// <summary>Chamar TODO frame (render thread). Pega o próximo comando se ocioso e avança a ação atual.</summary>
    public void Advance()
    {
        if (_curAction == null)
        {
            if (!_queue.TryDequeue(out _cur)) return;
            if (!_reg.TryCreate(_cur.Name, _ctx, _cur.Args, out _curAction, out var err))
            { Finish(false, err, null); return; }
            _log.Add($"> {_cur.Name} {string.Join(' ', _cur.Args)}".TrimEnd());
        }

        ActionStatus st;
        try { st = _curAction.Advance(); }
        catch (Exception e) { Finish(false, $"exceção: {e.Message}", null); return; }

        if (st == ActionStatus.InProgress) return;
        bool ok = st == ActionStatus.Done;
        Finish(ok, ok ? null : _curAction.Error, ok ? _curAction.Data : null);
    }

    private void Finish(bool ok, string msg, object data)
    {
        _log.Add($"{_cur?.Name}: {(ok ? "OK" : "FALHOU — " + msg)}");
        try { _cur?.Tcs?.TrySetResult(new CommandResult(ok, msg, data)); } catch { }
        UiInput.ReleaseRiskKeys();   // rede de segurança: nunca deixar Ctrl/tecla presa entre comandos
        _cur = null; _curAction = null;
    }
}
