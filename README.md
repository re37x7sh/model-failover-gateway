# ⚡ Model Failover Gateway (智能大模型透明代理与故障转移网关)

<div align="center">

[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-blue.svg)](#-跨平台快速启动)
[![Language](https://img.shields.io/badge/Language-C%23%2010%20%7C%20Vue%203-green.svg)](#)
[![i18n](https://img.shields.io/badge/i18n-🇨🇳%20中文%20%7C%20🇬🇧%20English-orange.svg)](#-中英双语支持)
[![License](https://img.shields.io/badge/License-MIT-purple.svg)](#)

**专为 Claude Code、CodeX (ChatGPT)、Continue、Cline、Cursor 等 AI 编程工具打造的本地高可用智能代理与故障转移网关。**

[简体中文](#-核心特性与功能) | [English](#-key-features--capabilities)

</div>

---

## 🚀 跨平台快速启动 / Quick Start

| 操作系统 (OS) | 启动服务 (Start) | 停止服务 (Stop) | 行为说明 |
| :--- | :--- | :--- | :--- |
| **Windows** | 双击 `start.bat` | 双击 `stop.bat` | 后台静默启动（无黑框）+ 托盘常驻 + 原生桌面萌宠 + 自动调用默认浏览器打开控制台 |
| **macOS (苹果)** | 运行 `./start.sh` | 运行 `./stop.sh` | 后台静默启动 + 自动调用系统 `open` 打开控制台 |
| **Linux** | 运行 `./start.sh` | 运行 `./stop.sh` | 后台静默启动 + 自动调用 `xdg-open` 打开控制台 |

控制台地址：**`http://127.0.0.1:5000`**

<img width="1311" height="744" alt="image" src="https://github.com/user-attachments/assets/460ab90f-3e6e-4f54-9504-b42a0ba77957" />

<img width="1429" height="831" alt="image" src="https://github.com/user-attachments/assets/f8617ca9-6450-4f7b-8105-e83050a2e60a" />

<img width="622" height="1001" alt="image" src="https://github.com/user-attachments/assets/e842c70d-5ea2-402f-b9bd-5fd41863dc94" />

<img width="1326" height="947" alt="image" src="https://github.com/user-attachments/assets/bc746907-608e-4b01-83c5-610515a4aa0c" />

---

## 🌟 核心特性与功能

### 1. 🐾 经典 Bongo Cat 敲键盘猫桌面挂件 (Desktop Pet)
- **纯正经典 Bongo 造型**：高对比度纯净漫画风，圆润软萌纯白猫头、两颗黑曜石豆豆眼、粉嫩腮红与猫耳；
- **魔性交替拍打打字 (Bongo Typing)**：当 VSCode / Claude Code 发起 AI 会话时，两只肉肉的白色小猫爪**高频交替飞速拍打键盘（啪嗒啪嗒敲击）**，配合键盘彩色键帽闪烁；
- **Agent 工具态与完成欢呼**：
  - 中间工具检索/修改时：右爪举起小放大镜 🔍 专注执行；
  - 任务彻底完成时：两只小爪子高高举起欢呼跳跃、彩带粒子四射绽放 🎉；
- **全局置顶与智能贴边收缩 (Edge Auto-Hide)**：
  - 永远漂浮在所有软件之上（TopMost）；
  - 拖至屏幕边缘自动吸附并收缩隐藏进边框，仅探出一对可爱猫耳，鼠标悬停平滑滑出。

### 2. 🧠 Agent 多轮工作流智能理解与防抖结算 (Agent Session State Machine)
- **精准识别 `stop_reason: tool_use`（杜绝中间步骤误报）**：
  - 精确解析 Claude `tool_use` / `end_turn` 与 OpenAI `tool_calls` / `stop`；
  - 大模型在中间调用 `grep_search` / `write_to_file` 工具时，**严格静默，不发完成弹窗、不响铃**，小猫保持专注工作；
- **多轮任务链耗时聚合 (Session Aggregation)**：
  - 将 Agent 连续的 5~20 步工具调用合并为一个完整的 Task Session；
  - 底座持续累计整个任务真实总耗时（如 `⚡ 03:33 (第 4 步)`）与累计 Tokens；
- **2.5 秒防抖确认窗口**：
  - 只有在模型全部输出完毕、VSCode 真正等待用户输入且静默 2.5 秒后，才正式触发一次**总揽完成提醒**（`🎉 共 5 步，总耗时 3m 33s`）+ 清脆 "Ding-Dong~" 完成音！

### 3. 🔔 渠道异常告警与可手动关闭通知中心 (Alert Management)
- **异常智能捕获**：实时监控渠道 402（余额不足/欠费）、429（频率超限/限流）、5xx（服务商宕机）及网络中断超时；
- **30 秒防抖合并**：短时间同渠道连续异常自动累加频次（如 `发生 3 次`），防止弹窗刷屏；
- **双重可视化通知**：
  - **页面顶部高亮告警横幅**：自动滑入异常警告条，支持单条关闭与全部忽略；
  - **导航栏 🔔 告警通知中心**：带红色呼吸红点与数量角标，点击展开下拉告警面板逐条查看与清理；
- **Windows 托盘气泡菜单**：支持在 Windows 托盘右键菜单中一键开启/彻底静音异常气泡。

### 4. ⚡ 跨平台客户端一键深度接管 (Zero-Config Takeover)
- **动态路径自适应**：自动识别 Windows (`%USERPROFILE%`, `%APPDATA%`)、macOS (`~/Library/Application Support/`, `~/.claude/`) 与 Linux 路径；
- **Claude Code CLI**：深度接管 `~/.claude/settings.json`（支持环境变量注入），秒回根路径探针；
- **CodeX CLI (ChatGPT)**：深度接管 `~/.codex/config.toml`，自动注入 `[model_providers]` 并支持自定义 Provider 名称；
- **VSCode 全局设置**：接管用户 `settings.json`，无缝兼容 JSONC（注释与尾随逗号）；
- **Continue 插件**：接管 `~/.continue/config.json`；
- **安全备份与一键还原**：接管前自动生成 `.bak` 备份，可随时一键原样还原。

### 5. 💎 多维度 Token 消耗统计看板 (Token Analytics)
- **全局 5 大指标**：累计总消耗 (Total Tokens)、Prompt (Input)、Completion (Output)、今日消耗 (Today)、请求总数；
- **🏷️ 渠道维度汇总**：直观展示各个渠道的 Token 吞吐与占比进度条，支持一键穿透查看 Key 明细；
- **🔑 渠道 + Key 细分维度**：精确归因到每个 API Key（安全脱敏），掌握多 Key 容灾中的主备 Key 实际消耗分布；
- **零延迟流式提取**：透明解析 Anthropic SSE `message_delta` 与 OpenAI `usage` 结构体，缺失时智能按传输字节保底估算；
- **便携持久化与优雅停机**：基于 exe 绝对物理路径锚定 `data/token_usage.json`，多启动入口路径自动自愈，关机与进程退出瞬间同步强制落盘，历史数据 100% 永不丢失。

### 6. 💾 请求日志全套分页检索与智能保留策略 (Log Pagination & Persistence)
- **大容量全套分页**：支持切换 **每页 20 / 50 / 100 / 200 条**（自动记忆选项），配备数字页码、上一页/下一页及快速跳页；
- **全字段实时搜索过滤**：支持通过搜索框实时模糊匹配路径、模型名称、处理渠道、错误原因及状态码；
- **磁盘物理便携存储**：所有请求链路与 Failover 故障转移记录实时写入 `data/request_logs.json`；
- **居中毛玻璃弹窗**：保留策略配置采用居中毛玻璃模态弹窗，操作清爽直观；
- **列宽自适应与悬浮 Tooltip**：长路径、复杂故障转移链路自动省略截断，鼠标悬浮即可查看完整信息；
- **灵活自动清理策略**：
  - **保留天数**：1 天 / 3 天 / 7 天（默认推荐）/ 14 天 / 30 天 / 永久保存；
  - **最大容量限制**：500 / 1,000 / 2,000 / 5,000 / 10,000 / 20,000 条（超出自动淘汰最旧数据）；
- **一键彻底清空**：支持随时手动一键擦除内存与磁盘上的历史请求日志。

### 7. 🌐 中英双语即时切换 (Bilingual i18n)
- 导航栏右上角支持 **`🇨🇳 中文` / `🇬🇧 EN`** 实时一键无缝切换；
- 仪表盘、渠道管理、Token 统计、实时日志、接入指引、告警中心及一键接管弹窗全部支持双语；
- 语言偏好自动保存至本地缓存。

### 8. 🎯 客户端分组与路由隔离 (Group Isolation)
- 支持为渠道分配专属分组标签：
  - **Claude 专用** (`/claude`)：针对 Claude Code 优化，保留 Prompt Caching 头；
  - **Codex 专用** (`/codex`)：针对 OpenAI / Codex 格式优化；
  - **通用渠道** (`/` 或 `/v1`)：全兼容通用中转。

### 9. 🔄 欠费/限流自动静默救急 (Smart Failover)
- **渠道内多 Key 容灾**：支持单渠道多行录入 API Key，遇到 402/429/欠费 时优先在同渠道备用 Key 间无感切换；
- **渠道间级联故障转移**：当前渠道耗尽或不可用时，单次请求内自动顺序尝试下一优先级渠道；
- **透明模型别名映射 (Model Alias)**：支持配置映射（如 `claude-3-7-sonnet => gpt-5.6-sol`），请求发往上游前自动改写。

### 10. 🎭 自定义请求头与 Codex 客户端伪装 / 动态提取 (Custom Headers & Codex Spoofing)
- **突破专用客户端限制**：允许任意 AI 工具（如 Claude Code、Cline、Cursor、Continue、Python SDK 等）无缝调用仅限 Codex/Copilot 才能访问的上游限制渠道；
- **内置官方级一键伪装预设**：
  - **🎭 模拟 Codex 专享头**：自动注入 `User-Agent: GithubCopilot/...`、`Editor-Version: vscode/...`、`Openai-Organization: github-copilot`、`Openai-Intent: conversation-panel` 等全套特征头；
  - **🏢 OpenAI 组织与项目头**：注入 `OpenAI-Organization` / `OpenAI-Project`；
  - **⚡ Anthropic 缓存头**：注入 `anthropic-version` 与 `anthropic-beta`；
- **强大的动态变量插值引擎**：
  - `{header:X-Name}`：从客户端原始请求中动态提取指定请求头；
  - `{header:X-Name:-default}`：提取请求头，未提供时回退到默认值；
  - `{uuid}`：每次请求动态生成唯一的 32 位 GUID（防止 RequestId 重复拦截）；
  - `{apiKey}`、`{model}`、`{client_ip}`、`{timestamp}`：插值当前渠道 Key、目标模型、客户端 IP 及时间戳；
- **📥 实时请求头嗅探与一键提取**：在「实时日志」中可直观查看任意真实客户端发送的原始请求头，并支持一键 `[ 📋 提取并复制为渠道请求头配置 ]`，免除手动抓包。

### 11. 📑 渠道一键即时克隆 (One-Click Channel Clone)
- **零摩擦一键克隆**：在渠道卡片操作栏点击 **`📑 克隆渠道`**，系统在后台 100% 完整克隆该渠道的全部配置（Base URL、多行 Key 列表、模型别名映射、自定义请求头与伪装、分组与支持模型）；
- **即点即落盘生效**：无需手动弹窗再次确认，自动生成 `原名称 (副本)` 并保存落盘，列表即刻出现新卡片，极速便捷。

### 12. 📌 渠道卡片直观显示本地调用 Base URL 与分组路由
- **卡片高亮直显**：每个渠道卡片顶部用紫色高亮框直观展示该渠道对应的 **`👉 本地调用 Base URL`**（如 `http://127.0.0.1:5000/copilot/v1`），并提供一键复制按钮，彻底告别配置困惑；
- **弹窗实时联动**：在新建/编辑渠道时，随着选择或输入分组，实时预览在 VSCode / Cursor 中需要填写的本地接入地址；
- **上游与本地清晰区分**：明确区分「上游服务 Base URL」（服务商接口）与「本地调用 Base URL」（客户端填写），指引一目了然。

---

## 📂 辅助工具脚本 (`scripts/` 目录)

为保持根目录清爽，高级辅助脚本已统一归纳至 [`scripts/`](file:///d:/个人资料/Paperwriting/model-failover-gateway/scripts) 目录下：

| 脚本文件 | 功能说明 |
| :--- | :--- |
| `创建桌面快捷方式.bat` | 在 Windows 桌面上生成「Model Failover Gateway」带图标的快捷方式 |
| `一键设置开机自启.bat` | 将网关加入 Windows 开机启动项，开机后自动在托盘静默运行 |
| `一键取消开机自启.bat` | 取消 Windows 开机自启动 |
| `一键打包绿色独立版.bat` | 将前端与 .NET 编译为完全独立的 `dist/` 免安装绿色文件夹（开箱即用） |

---

## 🌐 端点路由速查 (Endpoint Guide)

| 适用客户端 | 推荐配置 Base URL | 说明 |
| :--- | :--- | :--- |
| **Claude Code** | `http://127.0.0.1:5000/claude` | 优先匹配 Claude 专用渠道，支持探针与 Cache 头 |
| **CodeX (ChatGPT)** | `http://127.0.0.1:5000/codex/v1` | 优先匹配 Codex 专用渠道，适配 Responses 协议 |
| **VSCode / Cursor / Continue** | `http://127.0.0.1:5000/v1` | 匹配通用 (All) 或所有可用渠道 |
| **自定义专属分组 (如 Copilot)** | `http://127.0.0.1:5000/{分组名称}/v1` | 例如 `http://127.0.0.1:5000/copilot/v1`，精准路由到指定分组渠道 |

---

## 📄 开源许可证 / License

本项目采用 [MIT License](LICENSE) 开源许可证。
