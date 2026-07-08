using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace CurrencyExchange;

public class CurrencyExchangeSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    // --- Sistema de comandos (dev / seam do agente da VPS) ---
    public ToggleNode DevConsoleShow { get; set; } = new ToggleNode(true);   // console ImGui in-HUD
    public ToggleNode HttpEnable { get; set; } = new ToggleNode(true);       // canal HTTP localhost
    public RangeNode<int> HttpPort { get; set; } = new RangeNode<int>(8760, 1024, 65535);

    // --- Debug ---
    public ToggleNode SmokeOverlay { get; set; } = new ToggleNode(false);    // overlay read-only do World
}
