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
- 注释语法按**当前文件**扩展名选择：默认 `//`；`.py/.rb/.sh/.ps1/.yaml` 等用 `#`；`.sql/.lua/.hs` 用 `--`；`.bat/.cmd` 用 `REM`；`.xml/.xaml/.html/.csproj` 用 `<!-- -->`；`.css/.scss/.less` 用 `/* */`。C++ 项目（`.cpp/.h/.hpp/.c/.cc/.cxx/.inl`）全部走默认 `//`，该映射不影响 C++，只是为了让其它语言的注释也语法合法。
- 注入位置：FIM prefix 内部、原始 prefix 之前（FIM 模型只感知 prefix/suffix）。无 FIM token 的模型同样生效。
- 缓存键包含该上下文，上下文变化不会命中旧建议。
- 旧 ghost-text 后端和 native（VS Inline Suggestions）后端都生效；WebView 侧调用不受影响。

### Prompt 示例

```text
// recently visited Utils.cs:42: var joint = CreateJoint(bodyA, bodyB);
    ApplyForce(<|fim_suffix|>...
```

以上内容位于 `<|fim_prefix|>` 与 `<|fim_suffix|>` 之间。

### 修复：自动补全 provider 选择未持久化

原因：弹窗只在点击模型卡片时把 `providerId`/`providerType` 写回内存里的配置；仅在下拉框切换 provider 后保存，不会带上新 provider，重启后仍是旧值。另一个隐患是打开选择视图时若已保存的 provider 匹配不到，下拉会回退到第一项但配置不同步。

修复：[autocompletions.dialog.js](LMLocal/Resources/js/dialogs/autocompletions.dialog.js) 中 provider 下拉 `change` 时立即更新配置；`_populateProviderSelect` 匹配完成后同步选中项。同时 `build-deploy.bat` 现在会一并同步 `Resources`（JS/CSS），否则纯 UI 修复不会进入已安装版本。

### 调试：查看发给 LLM 的 prompt（R:\LM LOCAL.log）

每次实际发送 FIM 请求后，会把以下内容追加写入 `R:\LM LOCAL.log`（UTF-8、跨请求加锁，写入失败静默忽略，不影响补全）：

- 时间与模型 ID；
- `----- prompt -----`：最终 prompt（含 FIM 标记和注入的 `recently visited` 注释）；
- `----- response -----`：模型原始返回（未裁剪、未后处理）。

说明：命中缓存的补全不会发网络请求，所以不会记录；不记录 API key。该输出不依赖 `InternalLogger`，Release 安装版同样有效。

### 快捷键（与 VS 内置 Inline Suggestions 对齐）

- `Alt+'`：立即请求一次 FIM 建议，跳过 debounce，且允许光标后面已有文本（只接受首行时会自动裁剪重叠）。仅旧 ghost-text 后端生效；native 后端下不做处理。
- `Ctrl+Right`：只接受建议中下一个空白分隔块（含前导空格/换行），剩余部分继续以 ghost text 显示。没有建议时仍是 VS 原生的“下一个词”导航。
- `Ctrl+Down`：接受建议的下一行（单行建议等于全部接受）。没有建议时仍是原生滚动行为。

实现方式：`Alt+'` 是 VSCT 命令 + 键盘绑定；`Ctrl+Right`/`Ctrl+Down` 通过 `IOleCommandTarget` 过滤器只拦截 `WORDNEXT`/`SCROLLDN`，有建议时消费、无建议时透传。

### 改动文件

| 文件 | 作用 |
| --- | --- |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/RecentCaretContext.cs` | 新增。访问历史记录、去重、注释格式化（class @13，Record @39，BuildAnnotation @86） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/SuggestionTextViewCreationListener.cs` | 订阅光标移动与失焦事件（@82、@93） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/SuggestionTagger.cs` | 生成上下文并传入请求（@530），接受建议/单词/行、手动触发（@294 起） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/SuggestionCommandFilter.cs` | 新增。拦截 Ctrl+Right / Ctrl+Down，接受下一词/下一行 |
| `LMLocal/Commands/TriggerFimCommand.cs` | 新增。Alt+' 命令，立即请求一次建议 |
| `LMLocal/LMLocalPackage.vsct` | 新增 `Trigger FIM completion` 命令与 Alt+' 键位 |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/SuggestionTextViewCreationListener.cs` | 挂命令过滤器、维护当前焦点视图（@60 起） |
| `LMLocal/Infrastructure/Autocompletions/InlineCompletion/InlineSuggestionSource.cs` | native 后端传参（@201） |
| `LMLocal/Application/Autocompletions/AutocompletionsService.cs` | 把上下文拼进 FIM prefix（@82），并把每次请求/响应写入 `R:\LM LOCAL.log` |
| `LMLocal/Infrastructure/Autocompletions/AutocompleteDebugLog.cs` | 新增。向 `R:\LM LOCAL.log` 追加 prompt 与原始响应 |
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

## 2. Git 工作流（自有 fork）

- 仓库：`origin` = `https://github.com/axilesoft/LMLocal`（公开 fork）；不再跟踪上游 truehot/LMLocal。
- 分支：`main` 是唯一主线，所有改动都在 main；旧的 `local` 已合并并删除。
- 备份：`backup` = `D:/OProj/LMLocal-backup.git`（本地裸仓库）。
- 配置：`pull.ff only`，拉取只允许快进。
- 脚本：
  - `commit-push.bat`：在 `main` 上 `git add -A` + commit（默认消息带时间戳，可传入第一个参数作为消息）→ 推 `origin/main` → 推 `backup/main`。在其它分支上会拒绝执行。
  - `update.bat`：要求工作区干净；fetch `origin` → `git merge --ff-only origin/main` → 同步 `backup/main`。用于多机拉取。
  - `build-deploy.bat`：用带 VS SDK 工作负载的 MSBuild 编译 Release x64，然后把 `LMLocal.dll` / `LMLocal.pdb` 和 `Resources`（WebView 的 JS/CSS）覆盖到本机所有已安装实例（VS2022、VS2026、VS2026 Exp）。检测到 `devenv.exe` 在运行或构建失败时中止。
- 日常顺序：改代码 → `build-deploy.bat` 验证；提交推送用 `commit-push.bat`。
- 注意：`build-deploy.bat` 检测 VS 进程必须用 `%SystemRoot%\System32\findstr.exe`，不能写裸 `find`（Git 环境的 GNU find 会抢先解析并吞掉输出，导致检测失效）。

---

## 3. 已知限制

- 只注入一条行内容，没有停留时长、符号名、编辑历史等更丰富的导航上下文。
- 手动触发（Alt+'）只在 legacy ghost-text 后端生效；native 后端不支持。
- 行号基于离开时的快照；之后文件被修改不会回溯修正（内容本身已快照）。
- 注释注入依赖模型理解能力，极小概率影响生成风格；可通过扩展名映射或“仅跨文件注入”收敛。
