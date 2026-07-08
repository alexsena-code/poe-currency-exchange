using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using GameOffsets.Native;

namespace CurrencyExchange.World;

/// <summary>
/// Grid de caminhabilidade + pathfinding (reusa a lógica do Radar). Lê o grid PRONTO do ExileCore
/// (RawPathfindingData, walkable={1..5}), roda o Dijkstra (campo de distância a partir do alvo) em
/// BACKGROUND, e converte célula→tela. Uso: Ensure(gc) todo frame → EnsureField(alvo) até pronto → Path().
///
/// Melhorias vs projeto antigo (TerrainNav):
///  • usa Positioned.GridPosNum (GridX/GridY viraram [Obsolete] na API atual);
///  • Invalidate() explícito no AreaChange (o Plugin chama) — não depende só do hash, mata a race do _pf;
///  • a task de background captura o PathFinder LOCAL, então uma área nova nunca é contaminada por scan velho.
/// </summary>
public sealed class TerrainGrid
{
    private const float GridToWorldMultiplier = 250f / 23f;   // TileToWorld/TileToGrid (do Radar)

    private PathFinder _pf;
    private float[][] _height;
    private uint _areaHash;
    private Vector2i _fieldTarget;
    private Task _fieldTask;
    private bool _fieldReady;

    public bool Ready => _pf != null;

    /// <summary>Força reconstrução no próximo Ensure (chamar no AreaChange do plugin).</summary>
    public void Invalidate() { _pf = null; _fieldReady = false; _fieldTask = null; }

    /// <summary>(Re)constrói o PathFinder quando a área muda. Barato (o grid vem pronto do ExileCore).</summary>
    public void Ensure(GameController gc)
    {
        try
        {
            uint hash = 0; try { hash = (uint)gc.Area.CurrentArea.Hash; } catch { }
            if (_pf != null && hash == _areaHash) return;
            var grid = gc.IngameState.Data.RawPathfindingData;
            if (grid == null || grid.Length == 0) return;
            _height = gc.IngameState.Data.RawTerrainHeightData;
            _pf = new PathFinder(grid, new[] { 1, 2, 3, 4, 5 });
            _areaHash = hash; _fieldReady = false; _fieldTask = null;
        }
        catch { }
    }

    /// <summary>Posição em coords de GRID a partir de um Positioned (GridPosNum, não os GridX/GridY obsoletos).</summary>
    private static Vector2i ToGrid(Positioned p)
    {
        var g = p.GridPosNum;   // Vector2 (float)
        return new Vector2i((int)g.X, (int)g.Y);
    }

    public static Vector2i PlayerGrid(GameController gc)
    {
        try { return ToGrid(gc.IngameState.Data.LocalPlayer.GetComponent<Positioned>()); }
        catch { return new Vector2i(0, 0); }
    }

    public static bool EntityGrid(Entity ent, out Vector2i cell)
    {
        cell = default;
        try { var p = ent.GetComponent<Positioned>(); if (p == null) return false; cell = ToGrid(p); return true; }
        catch { return false; }
    }

    /// <summary>Garante que o CAMPO de distância pro alvo está pronto (Dijkstra em background 1x). true = dá pra Path().</summary>
    public bool EnsureField(Vector2i target)
    {
        if (_pf == null) return false;
        if (_fieldReady && _fieldTarget.Equals(target)) return true;
        if (_fieldTask != null && _fieldTarget.Equals(target))
        {
            if (_fieldTask.IsCompleted) { _fieldReady = _pf.HasField(target); return _fieldReady; }
            return false;
        }
        _fieldTarget = target; _fieldReady = false;
        var pf = _pf; var t = target;   // captura LOCAL: área nova troca _pf sem contaminar este scan
        _fieldTask = Task.Run(() => { try { foreach (var _ in pf.RunFirstScan(t, t)) { } } catch { } });
        return false;
    }

    /// <summary>Caminho (células) do start ao alvo do campo atual. null se não há rota/campo pronto.</summary>
    public List<Vector2i> Path(Vector2i start)
    {
        if (!_fieldReady || _pf == null) return null;
        try { return _pf.FindPath(start, _fieldTarget); } catch { return null; }
    }

    public bool Walkable(Vector2i cell) => _pf != null && _pf.IsPathable(cell);

    /// <summary>Snap pra célula CAMINHÁVEL mais próxima (espiral). NPC/alvo costuma ficar em célula não-walkable
    /// → o campo sai vazio e o path falha; snapar pro vizinho walkable conserta.</summary>
    public Vector2i SnapToWalkable(Vector2i cell)
    {
        if (_pf == null || _pf.IsPathable(cell)) return cell;
        for (int r = 1; r <= 60; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;   // só a borda do anel
                    var c = new Vector2i(cell.X + dx, cell.Y + dy);
                    if (_pf.IsPathable(c)) return c;
                }
        return cell;
    }

    /// <summary>O jogador está SE MOVENDO agora? (componente Pathfinding — anti-trava confiável). null = ilegível.</summary>
    public static bool? PlayerMoving(GameController gc)
    {
        try { return gc.IngameState.Data.LocalPlayer.GetComponent<Pathfinding>()?.IsMoving; }
        catch { return null; }
    }

    /// <summary>Converte célula do grid em coordenada de TELA (client-space) via WorldToScreen.</summary>
    public Vector2 CellToScreen(GameController gc, Vector2i cell)
    {
        float wz = 0;
        try { if (_height != null && cell.Y >= 0 && cell.Y < _height.Length && cell.X >= 0 && cell.X < _height[0].Length) wz = _height[cell.Y][cell.X]; } catch { }
        var world = new Vector3(cell.X * GridToWorldMultiplier, cell.Y * GridToWorldMultiplier, wz);
        return gc.IngameState.Camera.WorldToScreen(world);
    }
}
