namespace CurrencyExchange.Commands.Builtins;

/// <summary>
/// `game_state`: estado de alto nível do jogo lido da memória (inGame, área, hideout/town).
/// É o SENSOR que o launcher polla pra orquestrar sem timing cego: sabe quando logou (inGame=true)
/// e onde está, em vez de torcer pro sleep bater. Instantâneo, sensing puro.
/// </summary>
public sealed class GameStateCommand : IAction
{
    private readonly CommandContext _ctx;
    public string Error { get; private set; }
    public object Data { get; private set; }

    public GameStateCommand(CommandContext ctx) { _ctx = ctx; }

    public ActionStatus Advance()
    {
        var gc = _ctx.Gc;
        bool inGame = false;
        string area = null; bool hideout = false, town = false;
        try { inGame = gc?.IngameState?.InGame == true; } catch { }
        try
        {
            var a = gc?.Area?.CurrentArea;
            if (a != null) { area = a.Name; hideout = a.IsHideout; town = a.IsTown; }
        }
        catch { }
        Data = new { inGame, area, hideout, town };
        return ActionStatus.Done;
    }
}
