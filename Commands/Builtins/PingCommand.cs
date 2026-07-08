namespace CurrencyExchange.Commands.Builtins;

/// <summary>Comando trivial: prova que o loop transporte→fila→runner→resultado funciona. Instantâneo.</summary>
public sealed class PingCommand : IAction
{
    public string Error { get; private set; }
    public object Data { get; private set; }

    public ActionStatus Advance()
    {
        Data = "pong";
        return ActionStatus.Done;
    }
}
