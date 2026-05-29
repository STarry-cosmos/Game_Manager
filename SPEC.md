\# 游戏启动器开发规格文档



> 目标读者：AI 编程助手（如 Claude Code、Codex）  

> 用途：在 Windows 平台上独立开发一个本地游戏启动器，记录游戏时长，类似 Steam 库。



\---



\## 1. 项目概述

构建一个 \*\*Windows 原生桌面应用\*\*，用于管理本地安装的游戏，启动游戏并自动记录每次游玩的时长，提供游戏库网格或列表视图。



\*\*核心目标\*\*  

\- 添加、编辑、删除游戏条目（名称、可执行文件路径、封面图片等）。  

\- 一键启动游戏，在后台监控进程存活。  

\- 精确记录总游戏时长，处理异常退出、启动器崩溃等边界情况。  

\- 美观、流畅的现代 UI。



\---



\## 2. 技术栈与环境

\- \*\*操作系统\*\*：仅支持 Windows 10/11

\- \*\*语言与框架\*\*：C# (.NET 8 或 .NET Framework 4.8) + WPF

\- \*\*UI 模式\*\*：MVVM（使用 CommunityToolkit.Mvvm 简化）

\- \*\*数据库\*\*：SQLite，通过 `System.Data.SQLite` 或 `Microsoft.Data.Sqlite` 访问

\- \*\*ORM\*\*：可选 EF Core 或直接使用 ADO.NET（推荐轻量级直接 SQL，减少依赖）

\- \*\*进程管理\*\*：`System.Diagnostics.Process`

\- \*\*打包与分发\*\*：MSIX 或 ClickOnce（项目后期考虑）



\---



\## 3. 功能需求细节



\### 3.1 游戏库管理

\- \*\*添加游戏\*\*：用户选择 exe 文件，自动提取文件名（去除扩展名）作为默认游戏名，提取 exe 图标作为封面占位。

\- \*\*编辑游戏\*\*：修改名称、路径、封面图片（支持手动选取 jpg/png）。封面路径存储为本地文件路径或相对路径。

\- \*\*删除游戏\*\*：确认后移除数据库记录及相关封面文件。

\- \*\*展示视图\*\*：默认以网格形式显示游戏封面，支持名称、时长排序，以及搜索过滤。

\- \*\*状态指示\*\*：游戏正在运行时，封面卡片上显示“运行中”遮罩或实时计时器。



\### 3.2 游戏启动与进程监控

\- \*\*启动流程\*\*

&#x20; 1. 依据数据库存储的完整可执行文件路径启动进程。

&#x20; 2. 设置 `Process.EnableRaisingEvents = true`，注册 `Exited` 事件。

&#x20; 3. 记录会话开始时间 `SessionStartTime = DateTime.UtcNow`（统一使用 UTC 避免时区问题）。

&#x20; 4. 将游戏状态更新为“运行中”，在 UI 上反馈。

\- \*\*重复启动保护\*\*：若该游戏已有正在运行的进程（记录 PID），则禁止再次启动，并询问是否将焦点切换到运行中的游戏。

\- \*\*退出处理\*\*

&#x20; - 正常退出：`Exited` 事件触发时，计算本次会话时长 `Elapsed = DateTime.UtcNow - SessionStartTime`。

&#x20; - 启动器退出但游戏仍在运行：在启动器的 `Application.Exit` 或窗口关闭时，遍历所有标记为“运行中”的游戏，检查对应进程是否存活，若存活则结束本次会话并记录时长，或将状态标记为“可能未正常关闭”，下次启动时进行校验。

\- \*\*心跳机制\*\*：为防止启动器崩溃或突然关机导致整段时长丢失，在游戏运行期间每隔 30 秒将当前累积的会话时长写入数据库的 `CurrentSessionTime` 字段。最终结算时使用 `CurrentSessionTime` 作为基础增量，避免重复计数。



\### 3.3 游戏时间记录与统计

\- \*\*数据库字段\*\*

&#x20; - `TotalPlayTime` (TimeSpan 或 long 表示秒/ticks)：全局累计时长。

&#x20; - `CurrentSessionTime` (TimeSpan)：当前会话已保存的安全时长。

\- \*\*时长更新逻辑\*\*

&#x20; 1. 游戏启动时，`CurrentSessionTime` 清零。

&#x20; 2. 心跳定时器每隔 30 秒将 `(DateTime.UtcNow - SessionStartTime)` 更新到 `CurrentSessionTime`。

&#x20; 3. 进程退出时，最终会话时长 = `DateTime.UtcNow - SessionStartTime`；更新 `TotalPlayTime += (最终会话时长 - CurrentSessionTime)`，避免已心跳存入的部分被重复添加。

&#x20; 4. 将最终会话时长追加到历史记录表（可选）。

\- \*\*统计页面\*\*：提供简单的图表或列表，显示“本周游戏时长”“总时长排名”等。



\### 3.4 用户界面设计要点

\- 主窗口采用无边框或自定义标题栏，最大化内容区域。

\- 使用 `ItemsControl` 或 `ListBox` 绑定 `GameViewModel` 集合，ItemTemplate 包含封面图片、游戏名、时长信息。

\- 封面图片加载使用异步方式，避免卡顿，可加入缓存。

\- 右键上下文菜单提供编辑、删除、启动、打开文件位置等选项。

\- 设置选项：开机自启、最小化到托盘、封面图片来源（本地/SteamGridDB API 刮削）。



\---



\## 4. 数据库设计

\### 表：Games

| 列名 | 类型 | 说明 |

|------|------|------|

| Id | INTEGER PRIMARY KEY AUTOINCREMENT | 唯一标识 |

| Name | TEXT NOT NULL | 游戏名称 |

| ExecutablePath | TEXT NOT NULL | 可执行文件完整路径 |

| CoverImagePath | TEXT | 封面图片路径（可为空） |

| TotalPlayTime | INTEGER NOT NULL DEFAULT 0 | 总时长（以秒为单位存储） |

| CurrentSessionTime | INTEGER NOT NULL DEFAULT 0 | 当前会话已存时长（秒） |

| LastPlayed | TEXT | 上次游玩时间 ISO 8601 格式 |

| IsRunning | INTEGER NOT NULL DEFAULT 0 | 运行状态（0 未运行，1 运行中） |

| ProcessId | INTEGER | 当前运行的进程 PID（可为空） |

| CreatedAt | TEXT NOT NULL | 添加时间 |



\### 表：PlaySessions（可选，用于历史记录）

| 列名 | 类型 | 说明 |

|------|------|------|

| Id | INTEGER PRIMARY KEY AUTOINCREMENT |

| GameId | INTEGER | 关联 Games 表 |

| StartTime | TEXT | 会话开始 UTC 时间 |

| EndTime | TEXT | 会话结束 UTC 时间 |

| Duration | INTEGER | 该会话时长（秒） |



\---



\## 5. 核心类与架构建议

采用 MVVM 模式，以下为关键类：



```

Models/

&#x20;   GameModel.cs          // 数据实体，包含 INotifyPropertyChanged 或可观察属性

ViewModels/

&#x20;   MainViewModel.cs      // 游戏列表，添加/编辑/删除命令

&#x20;   GameItemViewModel.cs  // 单个游戏的展示逻辑（状态、实时时长等）

&#x20;   SettingsViewModel.cs

Services/

&#x20;   GameLibraryService.cs // CRUD 操作，数据库读写

&#x20;   ProcessMonitorService.cs // 启动、监控进程，心跳逻辑，时长结算

&#x20;   IconExtractorService.cs  // 从 exe 提取图标

Views/

&#x20;   MainWindow.xaml

&#x20;   GameCard.xaml          // 单个游戏封面的用户控件

&#x20;   StatisticsView.xaml

```



\*\*依赖注入\*\*：使用 `Microsoft.Extensions.DependencyInjection` 或简单的手动注入，便于测试和解耦。



\---



\## 6. 关键实现细节与边缘情况处理



\### 6.1 进程监控可靠性

\- `Process.Exited` 事件存在限制：如果启动器异常终止，事件可能不触发。因此应用关闭时必须主动结算所有“运行中”的游戏。在 `App.xaml.cs` 的 `OnExit` 或 `MainWindow.Closing` 事件中调用 `ProcessMonitorService.Shutdown()`。

\- 启动时检查数据库中有 `IsRunning = 1` 的记录，通过 PID 判断进程是否依旧存在。若不存在，则将上次会话标记为异常结束，并将 `CurrentSessionTime` 累加到 `TotalPlayTime`（因为心跳已保存了部分时长），同时更新 `LastPlayed` 为当前时间。



\### 6.2 游戏可执行文件变动

\- 每次启动前检查 `File.Exists(ExecutablePath)`，若不存在则提示用户，并允许重新定位或删除该游戏条目。



\### 6.3 线程安全

\- 涉及 UI 更新的定时器必须使用 `DispatcherTimer`。

\- 数据库写入操作应考虑并发，使用 `lock` 或异步队列，避免多个线程同时写 SQLite（SQLite 默认单写线程，多线程并发写易引发异常）。



\### 6.4 图标提取

\- 使用 `System.Drawing.Icon.ExtractAssociatedIcon(filePath)` 获取图标，转成 `BitmapSource` 供 WPF 显示。注意释放 `System.Drawing.Icon` 资源。



\---



\## 7. 开发步骤建议（可与 AI 对话逐阶段实现）

1\. \*\*项目初始化\*\*：创建 WPF 项目，配置 SQLite 连接，生成 Games 表。

2\. \*\*游戏库基础 CRUD\*\*：实现添加、删除、编辑、列表展示，不含启动功能。

3\. \*\*进程监控核心\*\*：实现 `ProcessMonitorService` 的启动、心跳、退出结算。

4\. \*\*UI 集成\*\*：将启动按钮、状态显示、实时计时器与 ViewModel 挂钩。

5\. \*\*边缘处理\*\*：关闭清理、异常恢复、重复启动保护。

6\. \*\*统计与美化\*\*：增加统计页面、网格视图美化、图标缓存。

7\. \*\*打包与测试\*\*：手动测试崩溃恢复，制作安装包。



\---



\## 8. 代码风格与规范

\- 使用 C# 现代语法（表达式体成员、null 条件运算符等）。

\- 命名遵循 PascalCase（公共成员），camelCase（局部变量）。

\- 异步方法以 `Async` 结尾。

\- 对数据库操作使用 `using` 确保连接释放。

\- 所有生成的代码应包含简洁的注释，解释非显而易见的逻辑（如时长结算公式）。



\---



