using System;
using System.Windows.Forms;
using ExileCore;
using CurrencyExchange.World;

namespace CurrencyExchange.Commands;

/// <summary>
/// Serviços que uma ação recebe pra executar. Bundle único — evita passar o GameController cru + N deps
/// por construtor em toda ação (fonte de god-wiring no projeto antigo). Cresce conforme novos módulos entram
/// (UiInput, Navigation, etc.).
/// </summary>
public sealed class CommandContext
{
    public GameController Gc { get; init; }
    public TerrainGrid Grid { get; init; }
    public EntityFinder Finder { get; init; }
    public Action<string> Log { get; init; }

    /// <summary>Tecla "Move Only" do jogo (andar sem atacar/interagir com o caminho). O operador precisa
    /// bindar essa tecla em Options → Input → Move Only. Default R.</summary>
    public Keys MoveKey { get; init; } = Keys.R;
}
