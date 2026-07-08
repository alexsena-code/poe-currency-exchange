using System;
using System.Collections.Concurrent;
using System.Linq;

namespace CurrencyExchange.Commands;

/// <summary>
/// Buffer circular thread-safe de linhas de log — consumido pelo painel do DevConsole (render thread) e
/// pelo endpoint GET /logs (thread do HTTP). Também espelha pro log do HUD (via callback opcional).
/// </summary>
public sealed class LogBus
{
    private const int Max = 300;
    private readonly ConcurrentQueue<string> _lines = new();
    private readonly Action<string> _mirror;   // ex.: LogMessage do HUD

    public LogBus(Action<string> mirror = null) { _mirror = mirror; }

    public void Add(string line)
    {
        var stamped = $"{DateTime.Now:HH:mm:ss} {line}";
        _lines.Enqueue(stamped);
        while (_lines.Count > Max) _lines.TryDequeue(out _);
        try { _mirror?.Invoke(line); } catch { }
    }

    public string[] Snapshot() => _lines.ToArray();

    public string Tail(int n) => string.Join("\n", _lines.Reverse().Take(n).Reverse());
}
