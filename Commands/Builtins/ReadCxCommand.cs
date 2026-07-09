using CurrencyExchange.World;

namespace CurrencyExchange.Commands.Builtins;

/// <summary>`read_cx`: snapshot tipado do CurrencyExchangePanel (par, taxa, book dos 2 lados, ordens do
/// jogador com idade/fill/undercut). Sensing puro — a VPS decide em cima disso. Falha se o CX não está aberto.</summary>
public sealed class ReadCxCommand : IAction
{
    private readonly CommandContext _ctx;
    public string Error { get; private set; }
    public object Data { get; private set; }

    public ReadCxCommand(CommandContext ctx) { _ctx = ctx; }

    public ActionStatus Advance()
    {
        var snap = CxView.Snapshot(_ctx.Gc);
        if (snap == null) { Error = "CX não está aberto"; return ActionStatus.Failed; }
        Data = snap;
        return ActionStatus.Done;
    }
}
