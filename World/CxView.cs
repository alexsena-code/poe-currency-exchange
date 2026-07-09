using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements.Village;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Models;

namespace CurrencyExchange.World;

/// <summary>
/// Leitura TIPADA do CurrencyExchangePanel (sensing puro). Fonte da verdade pro que a VPS decide:
/// par selecionado, taxa de mercado, o BOOK (stock dos 2 lados) e as ORDENS do jogador. Nada de decisão
/// aqui — só transforma a memória do jogo em dados. Nunca cacheia Element entre frames (lê fresco).
///
/// A API dá de graça o que o repo antigo rastreava à mão: `PlacedCurrencyExchangeOrder.CreationDate`
/// (idade da ordem) e `Competing*RatioPart` (preço competindo → undercut).
/// </summary>
public static class CxView
{
    public const int DefaultMaxOrders = 10;
    private const int BookCap = 20;   // níveis de book por lado no snapshot (evita payload gigante)

    public static CurrencyExchangePanel Panel(GameController gc)
    { try { return gc?.IngameState?.IngameUi?.CurrencyExchangePanel; } catch { return null; } }

    public static bool IsOpen(GameController gc)
    { var p = Panel(gc); try { return p != null && p.IsVisible; } catch { return false; } }

    /// <summary>Snapshot completo do painel (ou null se fechado). Objeto anônimo → serializa direto pro JSON do resultado.</summary>
    public static object Snapshot(GameController gc)
    {
        var p = Panel(gc);
        if (p == null || !p.IsVisible) return null;

        var orders = ReadOrders(p);
        return new
        {
            open = true,
            pair = new { offered = Name(SafeType(p, o: true)), wanted = Name(SafeType(p, o: false)) },
            marketRate = new { get = SafeShort(() => p.MarketRateGet), give = SafeShort(() => p.MarketRateGive) },
            orders = new { count = orders.Count, max = DefaultMaxOrders },
            book = new
            {
                wanted = ReadStock(SafeStock(() => p.WantedItemStock)),
                offered = ReadStock(SafeStock(() => p.OfferedItemStock)),
            },
            myOrders = orders,
        };
    }

    // ---- book ----
    /// <summary>Níveis do book: cada um é um rung (dá Give p/ receber Get, com ListedCount disponível). Top BookCap.</summary>
    private static List<object> ReadStock(List<CurrencyExchangeStock> stock)
    {
        var outp = new List<object>();
        if (stock == null) return outp;
        foreach (var s in stock.Take(BookCap))
        {
            try { outp.Add(new { give = s.Give, get = s.Get, listed = s.ListedCount }); }
            catch { }
        }
        return outp;
    }

    // ---- ordens do jogador ----
    /// <summary>Ordens abertas/fechadas do jogador (com idade nativa, fill e preço competindo).</summary>
    public static List<object> ReadOrders(CurrencyExchangePanel p)
    {
        var outp = new List<object>();
        List<PlacedCurrencyExchangeOrder> ords = null;
        try { ords = p.Orders; } catch { }
        if (ords == null) return outp;
        foreach (var o in ords)
        {
            try
            {
                var origStack = o.OriginalOfferedItemStackSize;
                var stack = o.OfferedItemStackSize;
                double? ageMin = null;
                try { ageMin = Math.Round((DateTimeOffset.UtcNow - o.CreationDate).TotalMinutes, 1); } catch { }
                outp.Add(new
                {
                    offered = Name(TrySafe(() => o.OfferedItemType)),
                    wanted = Name(TrySafe(() => o.WantedItemType)),
                    offeredRatio = o.OfferedItemRatioPart,
                    wantedRatio = o.WantedItemRatioPart,
                    stack,
                    origStack,
                    filled = Math.Max(0, origStack - stack),   // quanto já encheu (unidades ofertadas)
                    completed = o.IsCompleted,
                    canceled = o.IsCanceled,
                    competingOfferedRatio = o.CompetingOfferedItemRatioPart,
                    competingWantedRatio = o.CompetingWantedItemRatioPart,
                    ageMin,
                    orderId = o.PlayerOrderId,
                });
            }
            catch { }
        }
        return outp;
    }

    // ---- helpers ----
    private static BaseItemType SafeType(CurrencyExchangePanel p, bool o)
    { try { return o ? p.OfferedItemType : p.WantedItemType; } catch { return null; } }
    private static BaseItemType TrySafe(Func<BaseItemType> f) { try { return f(); } catch { return null; } }
    private static string Name(BaseItemType t) { try { return t?.BaseName; } catch { return null; } }
    private static int SafeShort(Func<short> f) { try { return f(); } catch { return 0; } }
    private static List<CurrencyExchangeStock> SafeStock(Func<List<CurrencyExchangeStock>> f) { try { return f(); } catch { return null; } }
}
