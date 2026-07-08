# Changelog — CurrencyExchange

Log corrido do plugin (o "changelog provisório" do projeto). Uma entrada por sessão/marco;
o mais recente no topo. Mudanças da API do HUD são rastreadas à parte via `git diff docs/hud_api`.

## [não lançado]

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
