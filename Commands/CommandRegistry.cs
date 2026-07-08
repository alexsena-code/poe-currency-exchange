using System;
using System.Collections.Generic;
using System.Linq;

namespace CurrencyExchange.Commands;

/// <summary>
/// Mapa nome→fábrica de IAction. Registrar um comando novo = 1 linha (Register("nome", (ctx,args)=>...)).
/// Substitui o switch gigante + 3 edições por comando do projeto antigo.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, Func<CommandContext, string[], IAction>> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Names => _factories.Keys.OrderBy(n => n);

    public void Register(string name, Func<CommandContext, string[], IAction> factory) => _factories[name] = factory;

    public bool TryCreate(string name, CommandContext ctx, string[] args, out IAction action, out string error)
    {
        action = null; error = null;
        if (string.IsNullOrWhiteSpace(name) || !_factories.TryGetValue(name, out var f))
        { error = $"comando desconhecido: '{name}' (use: {string.Join(", ", Names)})"; return false; }
        try { action = f(ctx, args ?? Array.Empty<string>()); return action != null; }
        catch (Exception e) { error = $"falha ao criar '{name}': {e.Message}"; return false; }
    }
}
