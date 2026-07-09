# Changelog — CurrencyExchange

Log corrido do plugin (o "changelog provisório" do projeto). Uma entrada por sessão/marco;
o mais recente no topo. Mudanças da API do HUD são rastreadas à parte via `git diff docs/hud_api`.

## [não lançado]

### 2026-07-09 — leitor de estado por memória (cxstate) + correção do Force
- **`launcher/cxstate`** (C#): lê `TheGame.InGame`/loading/areaHash REUSANDO o `ExileCore.dll` — constrói `Memory` (attach+pattern-scan, ctor por reflection) + `TheGame` com `GameController=null`, contornando a trava `CurrentArea is null`. Lê o estado ANTES de entrar no jogo (login/char-select), sem admin, sem fork, offsets auto-sincronizados com o HUD. Validado in-game: char-select→inGame:false, in-game→inGame:true (single-shot ~0,5s; `state.wait_in_game` polla).
- **Correção**: o `Force=true` NÃO faz o plugin rodar no login (o gate real é o `GameController..ctor` exigindo CurrentArea — o plugin nem carrega antes de um personagem). Docs corrigidos; Force fica só p/ rodar em menus depois de carregado.
- Launcher: descartado o dumper de offsets (caminho de reimplementar memória) — o cxstate reusa o ExileCore.


### 2026-07-09 — plugin roda desde o login (Force) + sensor game_state
- **`Force = true`** no Initialise: o ExileCore pula Tick/Render fora do jogo (`if (!InGame && !plugin.Force) continue;`); com Force o plugin roda DESDE A TELA DE LOGIN → HTTP + runner ativos no login, sem leitor de memória nem fork do ExileCore (flag oficial, resiliente a update). Doc em exileapi-usage.md.
- Comando `game_state` (inGame/área): sensor pro launcher pollar e saber quando logou/entrou — substitui timing cego.
- Launcher: descartado o caminho de leitor de memória nativo (dumper de offsets removido) — o Force resolve.


### 2026-07-08 — leitura do CX (read_cx)
- `World/CxView`: leitura tipada do CurrencyExchangePanel — par, taxa de mercado, BOOK dos 2 lados (CurrencyExchangeStock Give/Get/ListedCount), e ordens do jogador com **idade nativa (CreationDate)**, fill (orig-atual) e **preço competindo (Competing*RatioPart → undercut)**. Sensing puro.
- Comando `read_cx` (snapshot p/ a VPS decidir). Docs curadas atualizadas (usage/modules).


### 2026-07-08 — movimento + interação (goto / open_cx) + disciplina de docs
- `Navigation/Mover`: anda até entidade por pathfinding via UiInput (SEM Thread.Sleep — o PathNavigator antigo tinha); anti-trava com Pathfinding.IsMoving; snap do alvo pra célula walkable; gate anti-engasgo.
- `Interaction/NpcInteractor`: compõe o Mover + Ctrl+click no `Render.InteractCenterNum` (abre CX direto) + fallback menu por texto; verify pola `CurrencyExchangePanel.IsVisible`.
- Comandos novos: `goto <path>` (default Faustus), `open_cx`. `CommandContext.MoveKey` (default R — operador precisa bindar "Move Only").
- Docs: `docs/exileapi-usage.md` (API curada que usamos) + `docs/modules.md` (inventário de wrappers, anti-duplicação); disciplina registrada no CLAUDE.md.
- ✅ **Validado in-game** (HTTP): `goto Faustus` andou (player (610,536)→(614,542), 9 cliques, chegou a 6 células); `open_cx` abriu o CX (1 Ctrl+click, verify por CurrencyExchangePanel.IsVisible). Movimento + interação REAIS funcionando.


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
