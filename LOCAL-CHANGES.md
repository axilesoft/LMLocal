# LM Local 本地改动说明

本文记录本地分支 `local` 相对上游（truehot/LMLocal v3.2.8, 603c4ef）的改动：自动补全的“上一次光标行”上下文，以及本地 Git 工作流。上游代码本身未改。

---

## 1. 功能：自动补全携带上一次光标位置的行内容

### 目标

FIM 自动补全原先只发送光标前 300 字符 / 后 400 字符。当用户从别处（常见是另一个文件）跳到当前编辑点时，模型看不到刚才正在阅读的那行代码。现在会额外注入一行“最近访问行”的注释上下文。

### 行为

- 记录时机：
  - 光标在同一文件内跨行移动时，记录旧行（视图必须获得焦点）。
  - 视图失去聚合焦点（切文件、切窗口、点其它面板）时，记录当前行。
  - 空白行不记录；每行最长 200 字符，超出截断并追加 `…`。
- 内部保留最近 8 次访问，但每个请求只注入 **一条**：从新到旧选第一条满足以下条件的记录：
  - 不是当前行；
  - 内容长度 ≥ 3；
  - 内容没有已经出现在当前 prefix / suffix 里（避免重复）。
- 显示位置：同文件只写行号（`line 42`），跨文件写文件名 + 行号（`Utils.cs:42`）。
- 注释语法按**当前文件**扩展名选择：默认 `//`；`.py/.rb/.sh/.ps1/.yaml` 等用 `#`；`.sql/.lua/.hs` 用 `--`；`.bat/.cmd` 用 `REM`；`.xml/.xaml/.html/.csproj` 用 `<!-- -->`；`.css/.scss/.less` 用 `/* */`。
- 注入位置：FIM prefix 内部、原始 prefix 之前（FIM 模型只感知 prefix/suffix）。无 FIM token 的模型同样生效。
- 缓存键包含该上下文，上下文变化不会命中旧建议。
- 旧 ghost-text 后端和 native（VS Inline Suggestions）后端都生效；WebView 侧调用不受影响。

### Prompt 示例

```text
// recently visited Utils.cs:42: var joint = CreateJoint(bodyA, bodyB);
    ApplyForce(<|fim_suffix|>...
```

以上内容位于 `<|fim_prefix|>` 与 `<|fim_suffix|>` 之间。

### 改动文件

| 文件 | 作用 |
| --- | --- |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/RecentCaretContext.cs` | 新增。访问历史记录、去重、注释格式化（class @13，Record @39，BuildAnnotation @86） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/SuggestionTextViewCreationListener.cs` | 订阅光标移动与失焦事件（@82、@93） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/SuggestionTagger.cs` | 生成上下文并传入请求（@530），公共参数构造（@619、@633），缓存键（@769） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/InlineSuggestionSource.cs` | native 后端传参（@201） |
| `LMLocal/Application/Autocompletions/AutocompletionsService.cs` | 把上下文拼进 FIM prefix（@82） |
| `LMLocal/Core/Models/CompletionParameters.cs` | 新增 `RecentLineContext` 属性（@18） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/CompletionCache.cs` | 缓存键加入上下文（@138） |
| `LMLocal/LMLocal.csproj` | 注册新增源文件 |
| `LMLocal.Tests.Unit/Infrastructure/InlineCompletion/RecentCaretContextTests.cs` | 新增 10 个格式化/去重/回退测试 |
| `LMLocal.Tests.Unit/Services/AutocompletionsServiceTests.cs` | 新增 2 个 prompt 拼装测试（@248 起） |

### 可调参数

- `RecentCaretContext.MaxVisits = 8`：历史深度。
- `MaxLineLength = 200`：单行注入上限。
- `MinLineLength = 3`：过短行（如 `}`）不注入。
- 注释语法表：`RecentCaretContext.CommentTokens` @138。

### 验证

- 主工程：`MSBuild.exe LMLocal/LMLocal.csproj -p:Configuration=Release -p:Platform=x64`
- 单元测试：3050 项全部通过（VS2026 vstest + NUnit 适配器）。
- 未在真实 VS 编辑器内做 UI 手感验证；建议实测一次跨文件切换后的补全质量，若模型被注释带偏，可改为仅跨文件注入。

---

## 2. 本地 Git 工作流（不入上游）

- 分支模型：
  - `main`：上游只读镜像，跟踪 `origin/main`。
  - `local`：本地改动分支，日常提交都在这里。
- 远端：
  - `origin` = `https://github.com/truehot/LMLocal`，仅 fetch；push URL 设为 `no_push`，防止误推官方仓库。
  - `backup` = `D:/OProj/LMLocal-backup.git`（本地裸仓库，备份用）。
- 配置：`pull.ff only`，拉取只允许快进。
- 脚本：
  - `commit-push.bat`：在 `local` 上 `git add -A` + commit（默认消息带时间戳，可传入第一个参数作为消息）+ 推送到 `backup`。在其它分支上会拒绝执行。
  - `update.bat`：要求工作区干净；fetch 上游 → `git update-ref refs/heads/main origin/main`（不切分支）→ `git rebase main` → 强制更新 `backup/local`。若 main 有本地提交或 rebase 冲突会中止并提示。
- 日常顺序：`commit-push.bat` → `update.bat`。
- 注意：`update.bat` 不能改用 `git switch main`。cmd 逐行读取正在运行的 bat，切分支会把该文件从工作区删掉，脚本后半段会报“找不到指定的文件”。当前实现用 `update-ref` 规避。

---

## 3. 已知限制

- 只注入一条行内容，没有停留时长、符号名、编辑历史等更丰富的导航上下文。
- 行号基于离开时的快照；之后文件被修改不会回溯修正（内容本身已快照）。
- 注释注入依赖模型理解能力，极小概率影响生成风格；可通过扩展名映射或“仅跨文件注入”收敛。
- `update.bat` 的 rebase 会重写 `local` 历史，因此备份推送使用 `--force-with-lease`。
