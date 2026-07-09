# Changelog — CurrencyExchange

Log corrido do plugin (o "changelog provisório" do projeto). Uma entrada por sessão/marco;
o mais recente no topo. Mudanças da API do HUD são rastreadas à parte via `git diff docs/hud_api`.

## [não lançado]

### 2026-07-08 — movimento + interação (goto / open_cx) + disciplina de docs
- `Navigation/Mover`: anda até entidade por pathfinding via UiInput (SEM Thread.Sleep — o PathNavigator antigo tinha); anti-trava com Pathfinding.IsMoving; snap do alvo pra célula walkable; gate anti-engasgo.
- `Interaction/NpcInteractor`: compõe o Mover + Ctrl+click no `Render.InteractCenterNum` (abre CX direto) + fallback menu por texto; verify pola `CurrencyExchangePanel.IsVisible`.
- Comandos novos: `goto <path>` (default Faustus), `open_cx`. `CommandContext.MoveKey` (default R — operador precisa bindar "Move Only").
- Docs: `docs/exileapi-usage.md` (API curada que usamos) + `docs/modules.md` (inventário de wrappers, anti-duplicação); disciplina registrada no CLAUDE.md.


### 2026-07-08 — sistema de comandos (fila + runner + HTTP local + console ImGui)
- Núcleo transporte-agnóstico: `Commands/` (IAction, CommandContext, CommandRegistry, CommandRunner, LogBus, CommandRequest/Result). Fila thread-safe; render thread drena 1 cmd/frame (executor burro); solta teclas de risco entre comandos.
- Transporte `Control/HttpControl`: HttpListener em 127.0.0.1:8760 (POST /cmd, GET /commands, GET /logs); enfileira e awaita o resultado (completado pela render thread). É o seam do futuro agente da VPS.
- `Ui/DevConsole` (ImGui): input + botões + painel de log vivo — testar comandos em tempo real olhando o jogo (mesmo runner do HTTP, zero marshaling).
- Comandos de teste: `ping` (pong) e `status` (snapshot do World). Settings: HttpEnable/HttpPort/DevConsoleShow/SmokeOverlay.
- ✅ **Validado in-game** (HTTP via curl): `ping→pong`, `status`→ gridReady=true, player=(610,536), Faustus found dist=12.2 célula(620,543); log em ordem. World + comandos + HTTP OK no jogo.


### 2026-07-08 — core de movimentação (Input + World)
- `Input/UiInput` (multi-tick, zero Thread.Sleep) + `Input/Humanizer` portados e limpos (sem acoplar ao rate-limit do CX).
- `World/PathFinder` + `World/BinaryHeap` portados do Radar (Dijkstra por campo de distância, comprovado).
- `World/TerrainGrid` (ex-TerrainNav) melhorado: `GridPosNum` (GridX/GridY viraram obsoletos), `Invalidate()` no AreaChange (mata a race do _pf).
- `World/EntityFinder` novo: resolução por Path centralizada (antes duplicada em 2 lugares); nunca cacheia Entity entre frames.
- Build verde 0/0. Falta: Navigation (Mover/ChatCommand), Interaction (NpcInteractor), wire no Plugin.cs, launcher.


### 2026-07-08 — bootstrap do repo
- Repo do plugin criado do zero (reboot do projeto) a partir do template `exApiPlugin` (net8.0-windows).
- Clone do `ExileApi-Compiled` (net10) como HUD host; ligação por junction em dev.
- `tools/hud_api_dump.sh` portado do projeto antigo → baseline em `docs/hud_api/` (ExileCore: 732 tipos; GameOffsets: 140).
- Premissa fixada: plugin = executor burro; inteligência de mercado na VPS.
