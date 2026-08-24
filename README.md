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

<img width="1312" height="751" alt="image" src="https://github.com/user-attachments/assets/73d6b611-994c-426b-8477-9edfb29e145b" />

<img width="623" height="875" alt="image" src="https://github.com/user-attachments/assets/2b45b901-f3ea-48e7-b935-10dd97b91a20" />

<img width="1326" height="947" alt="image" src="https://github.com/user-attachments/assets/bc746907-608e-4b01-83c5-610515a4aa0c" />

---

## 🌟 核心特性与功能

### 1. 🐾 Windows 原生置顶·贴边吸附收缩桌面宠物 (Desktop Pet)
- **零浏览器依赖**：双击 `start.bat` 启动网关后，Windows 桌面直接常驻透明置顶小萌宠，无需常驻打开浏览器；
- **全局永远置顶 (Always On Top)**：全屏 VSCode 编码、查阅文档或写论文时，萌宠永远漂浮在所有软件最上层；
- **智能贴边吸附与自动收缩 (Edge Auto-Hide)**：
  - 拖拽至屏幕**左边缘、右边缘或顶部边缘**时自动吸附；
  - 离开边缘后平滑收缩隐藏进屏幕边框，仅露出一截可爱的猫耳朵，绝不遮挡代码；
  - 鼠标悬停在边框时自动平滑滑出（Slide-out），鼠标移开后自动缩回；
- **与 AI 会话动作实时联动**：
  - **VSCode 正在生成时**：萌宠原地疯狂敲键盘，底座实时展示思考耗时秒数（`⚡ 12s`）；
  - **长任务生成完毕时**：萌宠跳跃欢呼、撒花彩带特效 ✨、头顶冒出完成气泡，并播放清脆提示音；
- **3 款精美形象一键切换**：🐱 赛博猫咪 / 🤖 灵动机器人 / 🐶 忠诚柴犬；
- **双重控制**：右键点击萌宠自身即可唤起快捷菜单，Windows 任务栏托盘亦支持一键开启/隐藏。

### 2. ⏱️ 智能长任务识别与主动完成提醒 (Smart Notification)
- **避免短请求打扰**：瞬时的代码小补全（< 设定秒数）自动保持静默；
- **长任务精准捕获**：当单次 AI 会话或代码生成耗时超过阈值（支持 **1s / 3s / 5s / 10s / 15s / 30s** 灵活调节，默认 **5 秒**）且正常结束时，触发主动系统级提醒；
- **清脆完成提示音 (Ding-Dong~)**：基于 Web Audio API 与系统音频原生合成清脆大调三和弦音效，无需看屏幕即可获知任务完成；
- **Windows 任务栏系统气泡**：桌面右下角弹出原生 Toast 提示（含模型名称、耗时与 Token 消耗）。

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
- **磁盘物理持久化**：数据实时落盘至 `data/token_usage.json`，服务重启历史数据 100% 完整保留。

### 6. 💾 请求日志持久化与智能清理策略 (Log Persistence & Auto-Cleanup)
- **磁盘物理存储**：所有请求链路与 Failover 故障转移记录实时写入 `data/request_logs.json`；
- **居中毛玻璃弹窗**：保留策略配置采用居中毛玻璃模态弹窗，操作清爽直观；
- **列宽自适应与悬浮 Tooltip**：长路径、复杂故障转移链路自动省略截断，鼠标悬浮即可查看完整信息；
- **灵活自动清理策略**：
  - **保留天数**：1 天 / 3 天 / 7 天（默认推荐）/ 14 天 / 30 天 / 永久保存；
  - **最大容量限制**：500 / 1,000 / 2,000 / 5,000 / 10,000 条（超出自动淘汰最旧数据）；
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
| **VSCode / Cursor / Continue** | `http://127.0.0.1:5000/v1` | 匹配通用或所有可用渠道 |

---

## 📄 开源许可证 / License

本项目采用 [MIT License](LICENSE) 开源许可证。
