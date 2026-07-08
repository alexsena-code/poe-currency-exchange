using CurrencyExchange.World;

namespace CurrencyExchange.Commands.Builtins;

/// <summary>
/// Snapshot do sensing (World), instantâneo: grid pronto?, posição do jogador, movendo?, Faustus achado +
/// distância + célula. Prova a leitura in-game via o mesmo caminho de comando (sem input). O caminho/Dijkstra
/// completo fica pro futuro comando `goto` (multi-tick).
/// </summary>
public sealed class StatusCommand : IAction
{
    private readonly CommandContext _ctx;
    public string Error { get; private set; }
    public object Data { get; private set; }

    public StatusCommand(CommandContext ctx) { _ctx = ctx; }

    public ActionStatus Advance()
    {
        var gc = _ctx.Gc;
        _ctx.Grid.Ensure(gc);
        var player = TerrainGrid.PlayerGrid(gc);
        var moving = TerrainGrid.PlayerMoving(gc);
        var faustus = _ctx.Finder.FindNearestByPath("Faustus");

        float dist = -1; int fx = 0, fy = 0; bool found = faustus != null;
        if (found)
        {
            try { dist = faustus.DistancePlayer; } catch { }
            if (TerrainGrid.EntityGrid(faustus, out var cell)) { fx = cell.X; fy = cell.Y; }
        }

        Data = new
        {
            gridReady = _ctx.Grid.Ready,
            player = new { x = player.X, y = player.Y },
            moving = moving,
            faustus = new { found, dist, x = fx, y = fy },
        };
        return ActionStatus.Done;
    }
}
