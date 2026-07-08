using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using CurrencyExchange.World;
using CurrencyExchange.Debug;

namespace CurrencyExchange;

/// <summary>
/// Composition root FINO do plugin. Só instancia e fia os módulos (World/Navigation/Interaction) e
/// delega — nenhuma lógica de negócio mora aqui. Premissa: executor burro; inteligência na VPS.
///
/// Estágio atual: SMOKE do sensing (World) via overlay read-only. Sem input ainda.
/// </summary>
public class CurrencyExchange : BaseSettingsPlugin<CurrencyExchangeSettings>
{
    private readonly TerrainGrid _grid = new();
    private EntityFinder _finder;
    private SmokeOverlay _smoke;

    public override bool Initialise()
    {
        _finder = new EntityFinder(GameController);
        _smoke = new SmokeOverlay();
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _grid.Invalidate();   // offsets do grid mudam por área — força reconstrução (mata a race do _pf)
    }

    public override void Render()
    {
        if (!Settings.Enable) return;
        bool inGame = false;
        try { inGame = GameController?.IngameState?.InGame == true; } catch { }
        if (!inGame) return;

        _smoke.Draw(GameController, Graphics, _grid, _finder, (m, t) => LogMessage(m, t));
    }
}
