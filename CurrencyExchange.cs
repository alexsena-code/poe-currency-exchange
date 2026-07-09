using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using CurrencyExchange.World;
using CurrencyExchange.Commands;
using CurrencyExchange.Commands.Builtins;
using CurrencyExchange.Control;
using CurrencyExchange.Navigation;
using CurrencyExchange.Interaction;
using CurrencyExchange.Ui;
using CurrencyExchange.Debug;

namespace CurrencyExchange;

/// <summary>
/// Composition root FINO. Só instancia/fia os módulos e delega — nenhuma lógica de negócio aqui.
/// Premissa: executor burro; inteligência na VPS. O sistema de comandos (fila + runner) é o coração;
/// os transportes (console ImGui, HTTP local, futuro agente da VPS) só ENFILEIRAM no mesmo runner.
/// </summary>
public class CurrencyExchange : BaseSettingsPlugin<CurrencyExchangeSettings>
{
    private readonly TerrainGrid _grid = new();
    private EntityFinder _finder;
    private LogBus _log;
    private CommandRegistry _registry;
    private CommandRunner _runner;
    private HttpControl _http;
    private DevConsole _console;
    private SmokeOverlay _smoke;

    public override bool Initialise()
    {
        _finder = new EntityFinder(GameController);
        _log = new LogBus(m => LogMessage($"[CX] {m}", 3));
        _smoke = new SmokeOverlay();
        _console = new DevConsole();

        var ctx = new CommandContext
        {
            Gc = GameController, Grid = _grid, Finder = _finder, Log = m => _log.Add(m),
        };
        _registry = new CommandRegistry();
        _registry.Register("ping", (_, _) => new PingCommand());
        _registry.Register("status", (c, _) => new StatusCommand(c));
        // goto <path>: anda até a entidade (default Faustus). open_cx: anda + Ctrl+click abre o CX (fallback menu).
        _registry.Register("goto", (c, args) => new Mover(c, args.Length > 0 ? args[0] : "Faustus"));
        _registry.Register("open_cx", (c, _) => new NpcInteractor(c, "Faustus", () => CxOpen(c.Gc), "Currency Exchange"));
        _registry.Register("read_cx", (c, _) => new ReadCxCommand(c));   // snapshot tipado do painel (sensing)

        _runner = new CommandRunner(_registry, ctx, _log);

        if (Settings.HttpEnable)
        {
            _http = new HttpControl(Settings.HttpPort.Value, _runner, _registry, _log, m => _log.Add(m));
            _http.Start();
        }
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _grid.Invalidate();   // offsets do grid mudam por área — força reconstrução (mata a race do _pf)
    }

    public override void Render()
    {
        if (!Settings.Enable) return;

        _runner.Advance();   // avança 1 comando/frame na render thread (executor burro) — vale mesmo fora de zona

        if (Settings.DevConsoleShow)
            _console.Draw(_runner, _registry, _log, _http?.Running ?? false, Settings.HttpPort.Value);

        bool inGame = false;
        try { inGame = GameController?.IngameState?.InGame == true; } catch { }
        if (inGame && Settings.SmokeOverlay)
            _smoke.Draw(GameController, Graphics, _grid, _finder, (m, t) => LogMessage(m, t));
    }

    /// <summary>CX aberto? (fonte tipada). Usado pelo verify do open_cx.</summary>
    private static bool CxOpen(GameController gc)
    {
        try { var p = gc?.IngameState?.IngameUi?.CurrencyExchangePanel; return p != null && p.IsVisible; }
        catch { return false; }
    }

    public override void OnClose()
    {
        _http?.Dispose();
    }
}
