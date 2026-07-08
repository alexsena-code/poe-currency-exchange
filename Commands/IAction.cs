namespace CurrencyExchange.Commands;

/// <summary>Estado de um passo de ação multi-tick (avança 1 fase por frame).</summary>
public enum ActionStatus { InProgress, Done, Failed }

/// <summary>
/// Unidade executável. Instantânea (retorna Done no 1º Advance) ou multi-tick (avança por frame).
/// O CommandRunner chama Advance() todo frame até != InProgress. Premissa: 1 ação por vez (executor burro).
/// </summary>
public interface IAction
{
    ActionStatus Advance();
    string Error { get; }   // preenchido quando Failed
    object Data { get; }    // payload opcional quando Done
}
