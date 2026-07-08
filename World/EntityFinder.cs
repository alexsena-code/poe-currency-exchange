using System;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace CurrencyExchange.World;

/// <summary>
/// Resolução de entidades do mundo por substring do Path (NPCs/objetos: Faustus, Stash, etc.).
/// Centraliza o que no projeto antigo estava DUPLICADO em EntityClicker e PathNavigator.
///
/// REGRA: nunca cacheia Entity entre frames — cada chamada re-varre gc.Entities e devolve o vivo do
/// frame atual (entidade stale lê estado ANTIGO). Quem usa resolve por identidade (Path) a cada tick.
/// </summary>
public sealed class EntityFinder
{
    private readonly GameController _gc;

    public EntityFinder(GameController gc) { _gc = gc; }

    private static bool PathMatches(Entity e, string needle)
    {
        try
        {
            return e != null && e.IsValid && e.Path != null
                   && e.Path.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }

    /// <summary>Primeira entidade cujo Path contém `needle` (case-insensitive). null se não achar.</summary>
    public Entity FindByPath(string needle)
    {
        try { return _gc.Entities.FirstOrDefault(e => PathMatches(e, needle)); }
        catch { return null; }
    }

    /// <summary>Entidade mais PRÓXIMA do jogador cujo Path contém `needle` (desempata NPCs duplicados). null se não achar.</summary>
    public Entity FindNearestByPath(string needle)
    {
        try
        {
            return _gc.Entities
                .Where(e => PathMatches(e, needle))
                .OrderBy(e => { try { return e.DistancePlayer; } catch { return float.MaxValue; } })
                .FirstOrDefault();
        }
        catch { return null; }
    }
}
