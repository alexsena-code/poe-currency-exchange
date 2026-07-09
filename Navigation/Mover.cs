using System;
using System.Numerics;
using System.Windows.Forms;
using ExileCore;
using GameOffsets.Native;
using CurrencyExchange.Commands;
using CurrencyExchange.Input;
using CurrencyExchange.World;

namespace CurrencyExchange.Navigation;

/// <summary>
/// Anda até uma entidade (por substring do Path) por PATHFINDING (TerrainGrid+PathFinder), não por clique cego.
/// Melhorias vs o PathNavigator antigo: clique via UiInput multi-tick (ZERO Thread.Sleep — o antigo tinha!);
/// anti-trava com Pathfinding.IsMoving; SNAP do alvo pra célula walkable (NPC fica em célula não-caminhável);
/// gate anti-engasgo (não re-clica enquanto anda e progride). Para em `arriveCells` (quem chama interage).
/// </summary>
public sealed class Mover : IAction
{
    private readonly CommandContext _ctx;
    private readonly string _path;
    private readonly int _arriveCells;
    private readonly int _lookahead;
    private readonly Keys _moveKey;
    private readonly UiInput _in;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    private long _nextAt, _deadline, _lastProgressAt, _sinceClick;
    private int _clicks, _bestD = int.MaxValue, _dPrev = int.MaxValue;
    private bool _targetSet, _clicking;
    private Vector2i _target;
    private ActionStatus _state = ActionStatus.InProgress;

    public string Error { get; private set; }
    public object Data { get; private set; }

    public Mover(CommandContext ctx, string entityPath, int arriveCells = 10, int lookahead = 18)
    { _ctx = ctx; _path = entityPath; _arriveCells = arriveCells; _lookahead = lookahead; _moveKey = ctx.MoveKey; _in = new UiInput(ctx.Gc); }

    public ActionStatus Advance()
    {
        if (_state != ActionStatus.InProgress) return _state;
        if (_clock.ElapsedMilliseconds < _nextAt) return ActionStatus.InProgress;
        var gc = _ctx.Gc;

        // clique de caminhada em andamento (multi-tick): avança até terminar, então pausa humanizada
        if (_clicking)
        {
            var r = _in.ModClickHereStep(_moveKey);
            if (r == StepResult.Working) return ActionStatus.InProgress;
            _clicking = false; _clicks++; _sinceClick = _clock.ElapsedMilliseconds;
            Delay(Humanizer.Delay(280, 460));
            return ActionStatus.InProgress;
        }

        _ctx.Grid.Ensure(gc);
        if (!_ctx.Grid.Ready) return Fail("grid de terreno indisponível");

        var ent = _ctx.Finder.FindNearestByPath(_path);
        if (ent == null) return Fail($"entidade não encontrada (path contém '{_path}')");
        if (!TerrainGrid.EntityGrid(ent, out var raw)) return Fail("alvo sem Positioned (grid)");
        if (!_targetSet) { _target = _ctx.Grid.SnapToWalkable(raw); _targetSet = true; }

        var player = TerrainGrid.PlayerGrid(gc);
        var now = _clock.ElapsedMilliseconds;
        if (_deadline == 0) { _deadline = now + 30000; _lastProgressAt = now; }

        int dCells = Math.Max(Math.Abs(player.X - _target.X), Math.Abs(player.Y - _target.Y));
        if (dCells <= _arriveCells)
        {
            ReleaseMove();
            Data = new { arrived = true, cells = dCells, clicks = _clicks };
            _ctx.Log?.Invoke($"[mover] chegou perto de '{_path}' ({dCells} células, {_clicks} cliques)");
            _state = ActionStatus.Done; return _state;
        }

        if (dCells < _bestD) { _bestD = dCells; _lastProgressAt = now; }
        if (now - _lastProgressAt > 5000) { ReleaseMove(); return Fail($"preso — sem progresso há 5s (dist {dCells})"); }
        if (now >= _deadline) { ReleaseMove(); return Fail($"não chegou em 30s (dist {dCells})"); }
        if (_clicks >= 45) { ReleaseMove(); return Fail($"cap de cliques (dist {dCells})"); }

        if (!_ctx.Grid.EnsureField(_target)) { Delay(120); return ActionStatus.InProgress; }   // Dijkstra em background

        // waypoint À FRENTE da rota (on-screen), fallback = alvo direto
        var path = _ctx.Grid.Path(player);
        var wpCell = (path != null && path.Count > 0) ? path[Math.Min(_lookahead, path.Count - 1)] : _target;
        var wp = _ctx.Grid.CellToScreen(gc, wpCell);

        // gate anti-engasgo: já andando E progredindo (ou clicou há pouco) → deixa andar, não re-clica
        var moving = TerrainGrid.PlayerMoving(gc);
        bool progressing = dCells < _dPrev; _dPrev = dCells;
        if (moving == true && (progressing || now - _sinceClick < 700)) { Delay(160); return ActionStatus.InProgress; }

        // (parado, ou andando sem progredir) → posiciona o cursor no waypoint (clampado) e inicia o move-click
        if (!SetCursorClamped(wp)) { Delay(120); return ActionStatus.InProgress; }
        _clicking = true;
        return ActionStatus.InProgress;
    }

    private bool SetCursorClamped(Vector2 screen)
    {
        try
        {
            var win = _ctx.Gc.Window.GetWindowRectangle();
            const float mgn = 18f;
            float x = Math.Clamp(screen.X, mgn, Math.Max(mgn, win.Width - mgn));
            float y = Math.Clamp(screen.Y, mgn, Math.Max(mgn, win.Height - mgn));
            x = Humanizer.Jitter(x, 3); y = Humanizer.Jitter(y, 3);
            ExileCore.Input.SetCursorPos(new Vector2(x + win.X, y + win.Y));
            return true;
        }
        catch { return false; }
    }

    private void ReleaseMove() { _in.ReleaseAll(); }

    private ActionStatus Fail(string why)
    { Error = why; _state = ActionStatus.Failed; _in.ReleaseAll(); _ctx.Log?.Invoke($"[mover] FALHA: {why}"); return _state; }

    private void Delay(int ms) => _nextAt = _clock.ElapsedMilliseconds + ms;
}
