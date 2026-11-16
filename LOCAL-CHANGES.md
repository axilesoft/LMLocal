# LM Local 本地改动说明

基于上游 v3.2.8 的自有 fork（axilesoft/LMLocal），不再跟进上游。

## 1. 自动补全

- 后端：legacy ghost text（默认）/ native VS Inline Suggestions，配置在 `autocompletions.json`。
- FIM 识别：`qwen` / `codegemma`（含别名含 qwen）用 Qwen FIM 标记；`starcoder` / `refact` / `stable-code` 用 StarCoder 标记；`deepseek` 用 DeepSeek 标记；其余按普通补全。
- Kestrel 词表里有 `<|fim_*|>` 但实测未做 FIM 训练（手工 FIM、`/infill` 都会重复整段函数），因此按普通补全。
- 最近光标行：记录跨文件最近一次离开的光标行，作为注释注入；FIM 时放在 `<|fim_prefix|>` 之外。`includeRecentLineContext=false` 可关闭（本机开启）。
- 窗口：prefix 600 字符（对齐行首）、suffix 600；`max_tokens` 256；`temperature` 默认 **0.2**（配置可改，0 = 每次结果相同）。
- 手动触发（Alt+'）绕过缓存，每按一次重新请求。
- 快捷键：
  - `Alt+'`：立即触发一次
  - `Ctrl+Right` / `Ctrl+Alt+Right`：接受下一个词
  - `Ctrl+Down` / `Ctrl+Alt+Down`：接受下一行
  - `Tab`：接受全部；`Esc`：取消
  - 没有建议时 Ctrl 键保持 VS 原生导航
- 光标在行中：手动建议可以显示（ghost 用编辑背景覆盖尾部文字），只接受单行，避免拆行。

## 2. 调试

每次真实请求追加到 `R:\LM LOCAL.log`：

- 模型、`max_tokens` / `temp` / `stop`
- 完整 prompt（含 FIM 标记）
- 原始 response
- `manual:` 轨迹（触发路径、缓存绕过、丢弃原因、是否显示）

不记录 API key；写失败静默忽略；单测中禁用。

## 3. 配置（`%LOCALAPPDATA%\LMLocalChat\autocompletions.json`）

| 键 | 说明 |
| --- | --- |
| `enabled` | 总开关 |
| `providerId` / `providerType` | 选中的 provider（端口由 `providers.json` 的 `customBaseUrl` 决定） |
| `modelId` | 模型名；含 qwen 等关键字才会启用 FIM |
| `debounceDelayMs` | 打字触发延迟 |
| `backend` | `legacy` / `native` |
| `singleLinePreview` | native 后端只显示首行 |
| `includeRecentLineContext` | 是否注入最近光标行 |
| `temperature` | 采样温度，默认 0.2 |

## 4. 构建与部署

- `build-deploy.bat`：VS 关闭后编译 Release x64，将 `LMLocal.dll/.pdb` 和 `Resources`（JS/CSS）覆盖到所有安装实例（VS2022、VS2026、VS2026 Exp）。
- 改了 VSCT/命令/键位后：`build-deploy.bat updateconfig`，执行 `devenv /updateconfiguration` 重新注册。
- 注意：Marketplace 的自动更新可能覆盖本地构建，必要时提高 VSIX 版本或关闭该扩展的自动更新。

## 5. Git

- `origin` = `https://github.com/axilesoft/LMLocal`（main 唯一主线），不再跟踪上游。
- `backup` = `D:/OProj/LMLocal-backup.git`（本地裸仓库）。
- `commit-push.bat`：提交 → push origin → push backup。
- `update.bat`：从 origin 快进并同步 backup（多机用）。

## 6. 主要文件

- 补全逻辑：`LMLocal/Infrastructure/Autocompletions/InlineCompletion/`
- 请求拼装/模型识别：`AutocompletionsService.cs`
- 配置模型：`Core/Models/AutocompletionsConfig.cs`
- 手动触发命令：`Commands/TriggerFimCommand.cs`
- 调试日志：`Infrastructure/Autocompletions/AutocompleteDebugLog.cs`
- 设置弹窗（JS）：`Resources/js/dialogs/autocompletions.dialog.js`

## 7. 已知限制

- 手动触发手动建议只对 legacy 后端生效；native 后端不做拦截。
- 行中 ghost 预览会暂时遮住光标后的文字；接受后恢复。
- Kestrel 不支持 FIM，要 FIM 请用 qwen/codegemma 等模型。
