using System;
using System.Threading.Tasks;

namespace CurrencyExchange.Commands;

/// <summary>Resultado de um comando (o que volta pro transporte: HTTP, console, futuro agente da VPS).</summary>
public sealed record CommandResult(bool Ok, string Message, object Data);

/// <summary>
/// Um comando enfileirado. Transporte-agnóstico: o HTTP cria com um Tcs (awaita a resposta); o console
/// cria fire-and-forget (Tcs null, resultado só vai pro log). A render thread completa o Tcs quando terminar.
/// </summary>
public sealed class CommandRequest
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; init; }
    public string[] Args { get; init; } = Array.Empty<string>();
    public TaskCompletionSource<CommandResult> Tcs { get; init; }   // null = fire-and-forget
}
