# CurrencyExchange

Plugin ExileApi (PoE1) para o Currency Exchange do Faustus. **Executor burro**: lê o painel,
executa jobs de UI (abrir CX, selecionar moeda, postar/cancelar/coletar ordem) e reporta estado.
Toda a **inteligência de mercado roda na VPS** — o plugin não decide preço nem estratégia.

## Instalação (via PluginUpdater)

No HUD, adicione este repositório na lista do **PluginUpdater**; ele clona/atualiza em
`Plugins/Source/CurrencyExchange` e o HUD compila na inicialização.

## Build / desenvolvimento

- Requer .NET SDK 10 e a env var `exapiPackage` apontando pra pasta do HUD:
  ```powershell
  $env:exapiPackage="<...>\ExileApi-Compiled"; dotnet build
  ```
- Em dev, o fonte é ligado ao HUD por uma **junction** em `Plugins/Source/CurrencyExchange`
  (o HUD recompila na inicialização — reinicie o HUD após mudar o código).

## Índice da API do HUD / changelog da API

`docs/hud_api/*.api.txt` = dump determinístico da API pública de `ExileCore.dll`/`GameOffsets.dll`
(gerado por `tools/hud_api_dump.sh`). A cada update do HUD, rode o script de novo e faça
`git diff docs/hud_api` — as linhas +/- são o **changelog da API** (offsets/tipos que mudaram).
