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
| **Windows** | 双击 `start.bat` | 双击 `stop.bat` | 后台静默启动（无黑框）+ 托盘常驻 + 自动调用默认浏览器打开控制台 |
| **macOS (苹果)** | 运行 `./start.sh` | 运行 `./stop.sh` | 后台静默启动 + 自动调用系统 `open` 打开控制台 |
| **Linux** | 运行 `./start.sh` | 运行 `./stop.sh` | 后台静默启动 + 自动调用 `xdg-open` 打开控制台 |

控制台地址：**`http://127.0.0.1:5000`**

<img width="1311" height="744" alt="image" src="https://github.com/user-attachments/assets/460ab90f-3e6e-4f54-9504-b42a0ba77957" />

<img width="1312" height="751" alt="image" src="https://github.com/user-attachments/assets/73d6b611-994c-426b-8477-9edfb29e145b" />

<img width="623" height="875" alt="image" src="https://github.com/user-attachments/assets/2b45b901-f3ea-48e7-b935-10dd97b91a20" />

<img width="1326" height="947" alt="image" src="https://github.com/user-attachments/assets/bc746907-608e-4b01-83c5-610515a4aa0c" />

---

## 🌟 核心特性与功能

### 1. ⚡ 跨平台客户端一键深度接管 (Zero-Config Takeover)
- **动态路径自适应**：自动识别 Windows (`%USERPROFILE%`, `%APPDATA%`)、macOS (`~/Library/Application Support/`, `~/.claude/`) 与 Linux 路径；
- **Claude Code CLI**：深度接管 `~/.claude/settings.json`（支持环境变量注入），秒回根路径探针；
- **CodeX CLI (ChatGPT)**：深度接管 `~/.codex/config.toml`，自动注入 `[model_providers]` 并支持自定义 Provider 名称；
- **VSCode 全局设置**：接管用户 `settings.json`，无缝兼容 JSONC（注释与尾随逗号）；
- **Continue 插件**：接管 `~/.continue/config.json`；
- **安全备份与一键还原**：接管前自动生成 `.bak` 备份，可随时一键原样还原。

### 2. 💎 多维度 Token 消耗统计看板 (Token Analytics)
- **全局 5 大指标**：累计总消耗 (Total Tokens)、Prompt (Input)、Completion (Output)、今日消耗 (Today)、请求总数；
- **🏷️ 渠道维度汇总**：直观展示各个渠道的 Token 吞吐与占比进度条，支持一键穿透查看 Key 明细；
- **🔑 渠道 + Key 细分维度**：精确归因到每个 API Key（安全脱敏），掌握多 Key 容灾中的主备 Key 实际消耗分布；
- **零延迟流式提取**：透明解析 Anthropic SSE `message_delta` 与 OpenAI `usage` 结构体，缺失时智能按传输字节保底估算；
- **磁盘物理持久化**：数据实时落盘至 `data/token_usage.json`，服务重启历史数据 100% 完整保留。

### 3. 💾 请求日志持久化与智能清理策略 (Log Persistence & Auto-Cleanup)
- **磁盘物理存储**：所有请求链路与 Failover 故障转移记录实时写入 `data/request_logs.json`；
- **灵活自动清理策略**：
  - **保留天数**：1 天 / 3 天 / 7 天（默认推荐）/ 14 天 / 30 天 / 永久保存；
  - **最大容量限制**：500 / 1,000 / 2,000 / 5,000 / 10,000 条（超出自动淘汰最旧数据）；
- **一键彻底清空**：支持随时手动一键擦除内存与磁盘上的历史请求日志。

### 4. 🌐 中英双语即时切换 (Bilingual i18n)
- 导航栏右上角支持 **`🇨🇳 中文` / `🇬🇧 EN`** 实时一键无缝切换；
- 仪表盘、渠道管理、Token 统计、实时日志、接入指引及一键接管弹窗全部支持双语；
- 语言偏好自动保存至本地缓存。

### 5. 🎯 客户端分组与路由隔离 (Group Isolation)
- 支持为渠道分配专属分组标签：
  - **Claude 专用** (`/claude`)：针对 Claude Code 优化，保留 Prompt Caching 头；
  - **Codex 专用** (`/codex`)：针对 OpenAI / Codex 格式优化；
  - **通用渠道** (`/` 或 `/v1`)：全兼容通用中转。

### 6. 🔄 欠费/限流自动静默救急 (Smart Failover)
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

## 🛠️ 技术栈

- **后端 (Backend)**：C# / .NET 10 / ASP.NET Core Minimal API（高并发、零内存拷贝管道直通）
- **前端 (Frontend)**：Vue 3 + Vite + 原生 CSS Design Tokens + 响应式 i18n
- **系统层 (System)**：Windows NotifyIcon 系统托盘、macOS/Linux POSIX 后台进程守护
