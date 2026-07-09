# Módulos / wrappers do plugin (inventário)

**Checar aqui ANTES de escrever um helper novo** — se já existe um clique/foco/varredura/pathfinding,
reusar. Atualizar ao adicionar/mudar um módulo. (API do HUD que usamos: `exileapi-usage.md`.)

## Input/ — primitivos de entrada
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `UiInput` | ÚNICO input multi-tick (sem Thread.Sleep) | `ClickStep(el,right,jitter,settle)`, `CtrlClickStep(el,right,jitter)`, `ModClickHereStep(mod,right)`, `ClickAtStep(point,right,settle)`, `TypeStep(text, ref pos, delay)`, `ClearField()`, `ReleaseAll()`, static `ReleaseRiskKeys()` · enum `StepResult{Working,Done,Failed}` |
| `Humanizer` (static) | delays/jitter humanizados | `Delay(min,max)`, `Jitter(center,span)`, `PointIn(rect,inset)` |

## World/ — sensing (leitura pura, sem input)
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `EntityFinder` | achar entidade por Path (nunca cacheia) | `FindByPath(needle)`, `FindNearestByPath(needle)` |
| `TerrainGrid` | grid walkable + pathfinding facade | `Ensure(gc)`, `Invalidate()`, `Ready`, static `PlayerGrid(gc)`, static `EntityGrid(ent,out cell)`, `EnsureField(target)`, `Path(start)`, `Walkable(cell)`, `SnapToWalkable(cell)`, static `PlayerMoving(gc)`, `CellToScreen(gc,cell)` |
| `PathFinder` | Dijkstra por campo de distância (do Radar) | `RunFirstScan`, `HasField`, `FindPath`, `IsPathable` |
| `BinaryHeap<K,V>` | min-heap do Dijkstra | `Add`, `TryRemoveTop` |
| `CxView` (static) | leitura tipada do CurrencyExchangePanel (par, taxa, book 2 lados, ordens c/ idade/fill/undercut) | `Panel(gc)`, `IsOpen(gc)`, `Snapshot(gc)`, `ReadOrders(panel)` |

## Commands/ — núcleo do executor (transporte-agnóstico)
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `IAction` / `ActionStatus` | unidade executável multi-tick | `Advance():ActionStatus`, `Error`, `Data` |
| `CommandContext` | bundle de serviços p/ ações | `Gc`, `Grid`, `Finder`, `Log`, `MoveKey` |
| `CommandResult` / `CommandRequest` | mensagens | result: `Ok/Message/Data`; request: `Id/Name/Args/Tcs` |
| `LogBus` | log circular thread-safe | `Add(line)`, `Snapshot()`, `Tail(n)` |
| `CommandRegistry` | nome→fábrica de IAction | `Register(name,factory)`, `TryCreate(...)`, `Names` |
| `CommandRunner` | fila + drena 1/frame (executor burro) | `Enqueue(req)`, `Advance()`, `CurrentName`, `Pending` |
| `Builtins/PingCommand` | teste do loop | `ping` → "pong" |
| `Builtins/StatusCommand` | snapshot do World | `status` → gridReady/player/moving/faustus |
| `Builtins/ReadCxCommand` | snapshot tipado do CX (via CxView) | `read_cx` → pair/marketRate/book/myOrders |

## Navigation/ — mover-se
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `Mover` | anda até entidade (Path) por pathfinding; para em `arriveCells` | `Mover(ctx, entityPath, arriveCells=10, lookahead=18)` : IAction |
| `ChatCommand` | *(planejado)* `/hideout` via ChatInputElement | — |

## Interaction/ — interagir com NPC
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `NpcInteractor` | anda até NPC (compõe Mover) + abre (Ctrl+click InteractCenterNum) + fallback menu por texto; verify pola fonte tipada | `NpcInteractor(ctx, entityPath, isOpen:Func<bool>, menuText=null, arriveCells=10)` : IAction |

## Control/ — transporte de comandos
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `HttpControl` | canal local 127.0.0.1 (seam do agente VPS) | `Start()`, `Dispose()`, `Running` · rotas `POST /cmd`, `GET /commands`, `GET /logs` |

## Ui/ — dev
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `DevConsole` | console ImGui in-HUD (mesmo runner do HTTP) | `Draw(runner, reg, log, httpUp, port)` |

## Debug/
| Tipo | Responsabilidade | Membros públicos |
|---|---|---|
| `SmokeOverlay` | overlay read-only do World (opcional) | `Draw(gc, g, grid, finder, log)` |

## Raiz
| Tipo | Responsabilidade |
|---|---|
| `CurrencyExchange` (Plugin) | composition root fino: fia módulos, registra comandos, Render→runner.Advance()+console |
| `CurrencyExchangeSettings` | Enable, DevConsoleShow, HttpEnable, HttpPort, SmokeOverlay |
