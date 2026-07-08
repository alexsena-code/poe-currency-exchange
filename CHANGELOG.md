# Changelog — CurrencyExchange

Log corrido do plugin (o "changelog provisório" do projeto). Uma entrada por sessão/marco;
o mais recente no topo. Mudanças da API do HUD são rastreadas à parte via `git diff docs/hud_api`.

## [não lançado]

### 2026-07-08 — bootstrap do repo
- Repo do plugin criado do zero (reboot do projeto) a partir do template `exApiPlugin` (net8.0-windows).
- Clone do `ExileApi-Compiled` (net10) como HUD host; ligação por junction em dev.
- `tools/hud_api_dump.sh` portado do projeto antigo → baseline em `docs/hud_api/` (ExileCore: 732 tipos; GameOffsets: 140).
- Premissa fixada: plugin = executor burro; inteligência de mercado na VPS.
