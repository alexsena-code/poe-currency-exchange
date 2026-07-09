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

## Chat (futuro `/hideout`)
`IngameUi ... ChatPanel.ChatInputElement` — digitar com foco verificado (padrão do campo de busca).

## Obsoletos a EVITAR (já mordemos)
`Positioned.GridX/GridY/GridPos`, `Graphics.DrawFrame/DrawBox/DrawLine`, `Camera.SetCursorPositionSmooth`,
`Render.Pos/Bounds`.
