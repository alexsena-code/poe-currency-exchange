using System;
using System.Numerics;
using ExileCore;
using CurrencyExchange.World;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

namespace CurrencyExchange.Debug;

/// <summary>
/// Overlay de SMOKE TEST (read-only, ZERO input): prova in-game que a camada World funciona —
/// plugin carregado, Faustus achado (EntityFinder), posição do jogador (TerrainGrid.GridPosNum),
/// grid de terreno pronto, e Dijkstra calculando o caminho até o Faustus. Não clica nem anda.
/// Desenha um bloco de diagnóstico + marca o Faustus e o caminho na tela; loga um resumo 1x/s.
/// </summary>
public sealed class SmokeOverlay
{
    private readonly System.Diagnostics.Stopwatch _logClock = System.Diagnostics.Stopwatch.StartNew();

    public void Draw(GameController gc, Graphics g, TerrainGrid grid, EntityFinder finder, Action<string, float> log)
    {
        grid.Ensure(gc);

        var player = TerrainGrid.PlayerGrid(gc);
        var moving = TerrainGrid.PlayerMoving(gc);
        var faustus = finder.FindNearestByPath("Faustus");

        var lines = new System.Collections.Generic.List<string>
        {
            "== CurrencyExchange smoke (World) ==",
            $"grid pronto: {grid.Ready}  | jogador: ({player.X},{player.Y})  movendo: {moving?.ToString() ?? "?"}",
        };

        string summary;
        if (faustus == null)
        {
            lines.Add("Faustus: NÃO encontrado (está no hideout, perto do NPC?)");
            summary = $"[SMOKE] gridReady={grid.Ready} player=({player.X},{player.Y}) faustus=NAO";
        }
        else
        {
            float dist = SafeDist(faustus);
            TerrainGrid.EntityGrid(faustus, out var raw);
            var snapped = grid.SnapToWalkable(raw);
            bool fieldReady = grid.EnsureField(snapped);
            var path = fieldReady ? grid.Path(player) : null;
            int pathLen = path?.Count ?? -1;

            lines.Add($"Faustus: OK  dist={dist:0} célula=({raw.X},{raw.Y}) snap=({snapped.X},{snapped.Y})");
            lines.Add($"campo Dijkstra: {(fieldReady ? "pronto" : "calculando…")}  caminho: {(pathLen < 0 ? "—" : pathLen + " células")}");

            // marca o Faustus (célula snapada) na tela
            DrawMarker(gc, g, grid.CellToScreen(gc, snapped), "Faustus", Color.Yellow);
            // desenha o caminho (1 ponto a cada 5 células)
            if (path != null)
                for (int i = 0; i < path.Count; i += 5)
                    g.DrawText("•", grid.CellToScreen(gc, path[i]), Color.Lime);

            summary = $"[SMOKE] gridReady={grid.Ready} player=({player.X},{player.Y}) " +
                      $"faustusDist={dist:0} field={(fieldReady ? "ready" : "calc")} path={pathLen}";
        }

        // bloco de texto no canto superior esquerdo
        var pos = new Vector2(90, 140);
        foreach (var ln in lines) { g.DrawText(ln, pos, Color.White); pos.Y += 18; }

        // log throttled (1x/s) pra dar pra validar pelos Logs do HUD sem depender do print
        if (_logClock.ElapsedMilliseconds >= 1000) { _logClock.Restart(); log?.Invoke(summary, 1f); }
    }

    private static float SafeDist(ExileCore.PoEMemory.MemoryObjects.Entity e)
    { try { return e.DistancePlayer; } catch { return -1; } }

    private static void DrawMarker(GameController gc, Graphics g, Vector2 screen, string label, Color color)
    {
        if (float.IsNaN(screen.X) || float.IsNaN(screen.Y)) return;
        // marcador sem DrawFrame (só DrawText existe não-obsoleto na API atual): um "+" no ponto + label
        g.DrawText("+", new Vector2(screen.X - 4, screen.Y - 8), color);
        g.DrawText(label, new Vector2(screen.X + 8, screen.Y - 8), color);
    }
}
