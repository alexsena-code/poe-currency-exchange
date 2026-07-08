# CLAUDE.md — CurrencyExchange (plugin ExileApi, PoE1)

Plugin do Currency Exchange do Faustus. Reboot do projeto (jul/2026) — reescrito do zero
aproveitando o conhecimento de comportamento do jogo, não o código antigo.

## Premissa não-negociável: plugin = executor BURRO, inteligência na VPS

- O plugin **não decide mercado** (preço, quando cancelar, o que vender). Ele recebe comandos/planos
  já decididos e os **executa** in-game, reportando o estado observado. Toda análise/decisão
  (pricing, sanitização, balanceio de slots, seleção de itens) roda na **VPS**.
- 1 job por vez, avançado por frame (padrão `IBridgeAction`). Sem HTTP no plugin (ToS/rate-limit).

## Verdades do ExileApi/jogo (custaram caro — não redescobrir)

1. **Input só via primitivo multi-tick — NUNCA `Thread.Sleep`**: sleep congela a render thread do HUD
   inteiro. Clique/Ctrl+click/digitação avançam 1 fase por frame. Toda ação solta as teclas
   (`ReleaseAll`) no fim/falha — tecla presa no SO é bug recorrente.
2. **Ler memória tipada primeiro** (`CurrencyExchangePanel` etc. em `ExileCore.PoEMemory.Elements.Village`).
   Varredura de árvore só pro que não é tipado. **NUNCA cachear `Element` entre frames** — stale lê o
   valor ANTIGO, não null/0.
3. **Verify SEMPRE polla** (~2,5s): checar 1x cedo dá falso-negativo. Sucesso = mudança de estado
   observada NA FONTE.
4. **Ctrl precisa de GAP** entre down e o clique pra ser amostrado (down+up instantâneo = clique normal).
5. **Digitação**: conferir `FocusedInputElement` ANTES de cada tecla (senão vira hotkey no jogo);
   apóstrofo não é digitável layout-safe (buscar prefixo até o apóstrofo).
6. **Foco**: input exige o jogo em foreground (`GameWindow.IsForeground()`, NÃO `IsForeGroundCache`).
   Nunca injetar Alt pra focar (vaza pro app do operador).
7. **Ordem cancelada segue ocupando slot** até coletar; o contador "X/Y" só cai na coleta.
8. **Update do jogo/HUD quebra offsets** — ver checklist abaixo.

## Índice da API + changelog da API do HUD

`docs/hud_api/*.api.txt` é a superfície pública de `ExileCore.dll`/`GameOffsets.dll` (gerado por
`tools/hud_api_dump.sh`, via metadados — não carrega tipos). **Consulte-o antes de assumir que um
método existe** (a API não tem XML docs). Após update do HUD: rode o script e `git diff docs/hud_api`.

## Workflow

- **Build**: `$env:exapiPackage="<HUD>\ExileApi-Compiled"; dotnet build`. Verde e sem warnings NOVOS
  (warning é pista — CS0649 num campo de tecla já foi bug de tecla presa no passado).
- **Dev**: junction `Plugins/Source/CurrencyExchange` → este repo; o HUD recompila na inicialização
  (reiniciar o HUD após mudar código — o Loader compila no boot).
- **Deploy**: PluginUpdater clona/atualiza este repo no PC de jogo direto em `Plugins/Source`.
- **Changelog**: registrar cada marco em `CHANGELOG.md` (topo = mais recente).

### Checklist pós-update do jogo/HUD (offsets quebram)
1. `git diff docs/hud_api` após rerodar o dump — o que mudou na API?
2. Ler painel/contador/ordens: legível? (null = offsets do CX quebraram)
3. Smoke completo (open→select→place→cancel→collect) num item barato ANTES de religar automação.
4. Recriar a junction se o HUD foi re-clonado.

## Contexto do ecossistema

Faz parte do workspace **pathoftrade** (ver `../../../CLAUDE.md`). A VPS (`mahou-vps`, chave
`~/.ssh/pathoftrade_ed25519`) hospeda a inteligência. Infra CX antiga foi desmontada em 07/jul —
este é um recomeço limpo.
