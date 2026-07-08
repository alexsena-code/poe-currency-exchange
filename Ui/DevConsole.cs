using System;
using System.Linq;
using ImGuiNET;
using CurrencyExchange.Commands;
using Vector2 = System.Numerics.Vector2;

namespace CurrencyExchange.Ui;

/// <summary>
/// Console de dev in-HUD (ImGui): dispara comandos em TEMPO REAL olhando o jogo. Roda NA render thread
/// (enfileira no mesmo runner do HTTP — zero marshaling). Input de texto (Enter executa), botões pros
/// comandos comuns e painel com as últimas linhas do LogBus. Fire-and-forget: o resultado aparece no log.
/// </summary>
public sealed class DevConsole
{
    private string _input = "";

    public void Draw(CommandRunner runner, CommandRegistry reg, LogBus log, bool httpUp, int port)
    {
        bool open = ImGui.Begin("CX Dev Console");
        if (open)
        {
            ImGui.Text($"job: {runner.CurrentName ?? "-"}   fila: {runner.Pending}   " +
                       $"http: {(httpUp ? $"127.0.0.1:{port}" : "off")}");
            ImGui.Separator();

            // botões dos comandos registrados (linha compacta)
            int i = 0;
            foreach (var name in reg.Names)
            {
                if (i++ > 0) ImGui.SameLine();
                if (ImGui.Button(name)) Enqueue(runner, name);
            }

            // input livre: "nome arg1 arg2"  (Enter executa)
            ImGui.Separator();
            if (ImGui.InputText("cmd", ref _input, 128, ImGuiInputTextFlags.EnterReturnsTrue))
            { Submit(runner); }
            ImGui.SameLine();
            if (ImGui.Button("run")) Submit(runner);

            // log (últimas linhas)
            ImGui.Separator();
            foreach (var line in log.Snapshot().Reverse().Take(18).Reverse())
                ImGui.TextUnformatted(line);
        }
        ImGui.End();
    }

    private void Submit(CommandRunner runner)
    {
        var line = _input?.Trim();
        _input = "";
        if (string.IsNullOrWhiteSpace(line)) return;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Enqueue(runner, parts[0], parts.Skip(1).ToArray());
    }

    private static void Enqueue(CommandRunner runner, string name, string[] args = null)
        => runner.Enqueue(new CommandRequest { Name = name, Args = args ?? Array.Empty<string>() });
}
