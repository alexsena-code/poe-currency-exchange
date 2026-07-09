# ExileApi — API que USAMOS (índice curado)

Complementa o dump completo em `hud_api/*.api.txt`. Aqui fica só o que o plugin **usa de fato**,
com a razão e as pegadinhas. **Sempre consultar `hud_api/` antes de assumir que um método existe**
(a API não tem XML docs). Ao usar algo novo, anotar aqui.

## Mundo / posição / terreno
| Membro | Uso | Nota |
|---|---|---|
| `GameController.IngameState.Data.RawPathfindingData` (`int[][]`) | grid de caminhabilidade (walkable = {1..5}) | base do PathFinder |
| `...Data.RawTerrainHeightData` (`float[][]`) | altura por célula p/ WorldToScreen | |
| `...Data.AreaDimensions` (`Vector2i`) | dimensões do grid | |
| `...Data.LocalPlayer` (`Entity`) | o jogador | |
| `Positioned.GridPosNum` (`Vector2`) | posição em células | **usar este** — `GridX/GridY`/`GridPos` são `[Obsolete]` |
| `Pathfinding.IsMoving` (`bool`) | anti-trava (jogador andando?) | tem também `TargetMovePos`/`PathingNodes`/`WantMoveToPosition` |
| `Area.CurrentArea.Hash` (`uint`) | invalidar grid por área | |

## Câmera / render / projeção
| Membro | Uso | Nota |
|---|---|---|
| `IngameState.Camera.WorldToScreen(Vector3)` | mundo → tela (client-space) | somar offset da janela p/ tela real |
| `Render.PosNum` (`Vector3`) | posição do modelo | |
| `Render.Height` (`float`) | altura p/ compor o Z | |
| `Render.InteractCenterNum` (`Vector3`) | **ponto de INTERAÇÃO** do NPC | melhor alvo p/ clicar/abrir que PosNum |

## Entidades
| Membro | Uso | Nota |
|---|---|---|
| `GameController.Entities` | varredura | filtrar por `Path` (substring) |
| `Entity.Path` / `IsValid` | identidade estável | resolver por Path a cada frame, **nunca cachear Entity** |
| `Entity.DistancePlayer` (`float`) | distância ao jogador | **em unidades de GRID** (Euclidiana das células) |
| `Entity.GetComponent<T>()` / `TryGetComponent` | Positioned/Render/Pathfinding | |

## UI
| Membro | Uso | Nota |
|---|---|---|
| `IngameState.IngameUi` (`IngameUIElements`) | raiz da UI | |
| `IngameUi.CurrencyExchangePanel` (`...Village.CurrencyExchangePanel`) | verificar CX aberto | `Element.IsVisible`; tem `OrderElements` |
| `Element.GetClientRect()` / `Text` / `IsVisible` / `Children` | achar/clicar elementos, menu do NPC | podar ramos invisíveis na busca |

## Input (estático `ExileCore.Input`)
`SetCursorPos(Vector2)`, `KeyDown/KeyUp(Keys)`, `LeftDown/LeftUp`, `RightDown/RightUp`, `Click(MouseButtons)`,
`KeyPressRelease(Keys)`. Encapsulado em `Input/UiInput` (multi-tick, sem Thread.Sleep).

## Desenho (`Graphics`)
`DrawText(string, Vector2, Color)` é o **não-obsoleto**. `DrawFrame/DrawBox/DrawLine` são `[Obsolete]`
(e `DrawFrame` nem aceita `RectangleF`) → evitar; marcar pontos com `DrawText`.

## Currency Exchange (leitura do painel) — `ExileCore.PoEMemory.Elements.Village`
| Membro | Uso | Nota |
|---|---|---|
| `CurrencyExchangePanel.OfferedItemType` / `WantedItemType` (`BaseItemType`) | par selecionado (I Have / I Want) | `.BaseName` |
| `.MarketRateGet` / `.MarketRateGive` (`Int16`) | taxa de mercado do par | |
| `.WantedItemStock` / `.OfferedItemStock` (`List<CurrencyExchangeStock>`) | **o BOOK** dos 2 lados | |
| `.Orders` (`List<PlacedCurrencyExchangeOrder>`) | ordens do jogador | `Count` = contador (não há prop de "X/Y") |
| `.OrderElements` | cards da UI (geometria) | pro clique de cancel/collect (futuro) |
| `.CurrencyPicker`, `.WantedItemCountInput`, `.OfferedItemCountInput`, `.RatioElement` | postar ordem (futuro) | |
| `CurrencyExchangeStock` → `Give` / `Get` / `ListedCount` (int) | 1 rung do book (dá Give → recebe Get, N listados) | preço = Give/Get ou Get/Give conforme o lado |
| `PlacedCurrencyExchangeOrder` → `Offered/WantedItemType`, `Offered/WantedItemRatioPart`, `OfferedItemStackSize`, `OriginalOfferedItemStackSize` | par, ratio a:b, fill (orig-atual) | |
| ... `IsCompleted` / `IsCanceled` | status | ordem fechada segue ocupando slot até coletar |
| ... **`CreationDate`** (`DateTimeOffset`) | **idade da ordem NATIVA** | antes rastreávamos firstSeen à mão |
| ... **`CompetingOfferedItemRatioPart` / `CompetingWantedItemRatioPart`** | **preço competindo** → undercut de graça | |
| ... `PlayerOrderId`, `GoldCost`, `*ItemHash` | id/custo | |

## Chat (futuro `/hideout`)
`IngameUi ... ChatPanel.ChatInputElement` — digitar com foco verificado (padrão do campo de busca).

## Ciclo de vida do plugin / rodar FORA do jogo
O ExileCore pula `Tick`/`Render` de plugin quando não está no jogo:
`if (!GameController.InGame && !plugin.Force) continue;`. Pra rodar **desde a tela de login**
(nosso caso — sensor durante o login), setar **`Force = true`** (prop de `IPlugin`/`BaseSettingsPlugin`)
no `Initialise`. Flag OFICIAL do framework → resiliente a update (sem fork do ExileCore).
`Initialise` roda no boot do HUD (compila plugins no boot), não é gated por InGame.

## Obsoletos a EVITAR (já mordemos)
`Positioned.GridX/GridY/GridPos`, `Graphics.DrawFrame/DrawBox/DrawLine`, `Camera.SetCursorPositionSmooth`,
`Render.Pos/Bounds`.
