<template>
  <div class="guide-view">
    <div class="toolbar">
      <div class="toolbar-left">
        <h2 class="section-title">客户端专属分组接入指引</h2>
        <p class="section-subtitle">
          配置不同的分组端点，即可实现 <strong>Claude 与 CodeX 渠道彻底隔离</strong>，各走各的专属渠道池，同时享受 100% 自动救急与多 Key 容灾！
        </p>
      </div>

      <button class="btn btn-primary" @click="$emit('openSettings')">
        <span>⚡ 一键自动接管所有配置 (免手动修改)</span>
      </button>
    </div>

    <div class="guides-grid">
      <!-- 1. VSCode Claude 插件 / Claude Code CLI (专属分组) -->
      <div class="glass-card guide-card">
        <div class="guide-header">
          <span class="guide-badge badge-claude">🎯 Claude 专属</span>
          <h3 class="guide-title">VSCode Claude 插件 / Claude Code</h3>
        </div>
        <p class="guide-desc">使用 <code>/claude</code> 专属端点，仅会路由到标记为 <strong>Claude 专用</strong> 或 <strong>通用</strong> 的渠道：</p>

        <div class="config-table">
          <div class="config-row">
            <span class="config-key">Base URL (推荐专属):</span>
            <div class="config-val-box">
              <span class="code-tag font-mono">http://127.0.0.1:5000/claude</span>
              <button class="btn btn-secondary btn-sm" @click="copyText('http://127.0.0.1:5000/claude', 'Claude 专属端点已复制')">复制</button>
            </div>
          </div>
          <div class="config-row">
            <span class="config-key">API Key:</span>
            <div class="config-val-box">
              <span class="code-tag font-mono">sk-local-proxy</span>
              <button class="btn btn-secondary btn-sm" @click="copyText('sk-local-proxy', 'Key 已复制')">复制</button>
            </div>
          </div>
        </div>

        <div class="code-block-container">
          <pre class="code-block font-mono"><code># Claude Code CLI (PowerShell 终端):
$env:ANTHROPIC_BASE_URL="http://127.0.0.1:5000/claude"
$env:ANTHROPIC_API_KEY="sk-local-proxy"
claude</code></pre>
          <button class="btn btn-secondary btn-sm copy-float-btn" @click="copyText(claudeCliCmd, 'PowerShell 命令已复制')">复制 CLI 命令</button>
        </div>
      </div>

      <!-- 2. CodeX 插件 (专属分组) -->
      <div class="glass-card guide-card">
        <div class="guide-header">
          <span class="guide-badge badge-codex">🎯 Codex 专属</span>
          <h3 class="guide-title">CodeX 插件</h3>
        </div>
        <p class="guide-desc">使用 <code>/codex</code> 专属端点，仅会路由到标记为 <strong>Codex 专用</strong> 或 <strong>通用</strong> 的渠道：</p>

        <div class="config-table">
          <div class="config-row">
            <span class="config-key">Base URL (专属端点):</span>
            <div class="config-val-box">
              <span class="code-tag font-mono">http://127.0.0.1:5000/codex/v1</span>
              <button class="btn btn-secondary btn-sm" @click="copyText('http://127.0.0.1:5000/codex/v1', 'Codex 专属端点已复制')">复制</button>
            </div>
          </div>
          <div class="config-row">
            <span class="config-key">API Key:</span>
            <div class="config-val-box">
              <span class="code-tag font-mono">sk-local-proxy</span>
              <button class="btn btn-secondary btn-sm" @click="copyText('sk-local-proxy', 'Key 已复制')">复制</button>
            </div>
          </div>
        </div>
        <div class="guide-note">
          💡 如果你配置了模型别名映射（如 <code>claude-3-7-sonnet => gpt-5.6-sol</code>），CodeX 发起请求时网关会自动完成模型重命名。
        </div>
      </div>

      <!-- 3. Continue / Cline 插件 -->
      <div class="glass-card guide-card">
        <div class="guide-header">
          <span class="guide-badge">扩展</span>
          <h3 class="guide-title">Continue 插件 (config.json)</h3>
        </div>
        <p class="guide-desc">在 <code>~/.continue/config.json</code> 中直接配置：</p>

        <div class="code-block-container">
          <pre class="code-block font-mono"><code>{
  "models": [
    {
      "title": "Claude 3.5 Sonnet (Failover Auto)",
      "provider": "anthropic",
      "model": "claude-3-5-sonnet-20241022",
      "apiBase": "http://127.0.0.1:5000/claude",
      "apiKey": "sk-local-proxy"
    }
  ]
}</code></pre>
          <button class="btn btn-secondary btn-sm copy-float-btn" @click="copyText(continueConfigJson, 'Continue 配置已复制')">
            复制 JSON
          </button>
        </div>
      </div>

      <!-- 4. 通用 / Cursor / OpenAI 兼容客户端 -->
      <div class="glass-card guide-card">
        <div class="guide-header">
          <span class="guide-badge">🌐 通用端点</span>
          <h3 class="guide-title">通用兼容端点 (所有渠道均可使用)</h3>
        </div>
        <p class="guide-desc">使用通用端点将根据优先级尝试所有<strong>通用 (All)</strong> 渠道：</p>

        <div class="config-table">
          <div class="config-row">
            <span class="config-key">通用 Base URL:</span>
            <div class="config-val-box">
              <span class="code-tag font-mono">http://127.0.0.1:5000/v1</span>
              <button class="btn btn-secondary btn-sm" @click="copyText('http://127.0.0.1:5000/v1', '通用 Base URL 已复制')">复制</button>
            </div>
          </div>
          <div class="config-row">
            <span class="config-key">API Key:</span>
            <div class="config-val-box">
              <span class="code-tag font-mono">sk-local-proxy</span>
              <button class="btn btn-secondary btn-sm" @click="copyText('sk-local-proxy', 'Key 已复制')">复制</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
const emit = defineEmits(['toast']);

const claudeCliCmd = `$env:ANTHROPIC_BASE_URL="http://127.0.0.1:5000/claude"\n$env:ANTHROPIC_API_KEY="sk-local-proxy"\nclaude`;

const continueConfigJson = `{\n  "title": "Claude 3.5 Sonnet (Failover Auto)",\n  "provider": "anthropic",\n  "model": "claude-3-5-sonnet-20241022",\n  "apiBase": "http://127.0.0.1:5000/claude",\n  "apiKey": "sk-local-proxy"\n}`;

async function copyText(text, successMsg) {
  try {
    await navigator.clipboard.writeText(text);
    emit('toast', successMsg, 'success');
  } catch (err) {
    emit('toast', '复制失败，请手动选择', 'error');
  }
}
</script>

<style scoped>
.guide-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.toolbar-left {
  margin-bottom: 8px;
}

.section-title {
  font-size: 18px;
  font-weight: 700;
}

.section-subtitle {
  font-size: 13px;
  color: var(--text-muted);
  margin-top: 4px;
}

.guides-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(480px, 1fr));
  gap: 20px;
}

@media (max-width: 600px) {
  .guides-grid {
    grid-template-columns: 1fr;
  }
}

.guide-card {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.guide-header {
  display: flex;
  align-items: center;
  gap: 10px;
}

.guide-badge {
  font-size: 11px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: var(--radius-full);
  border: 1px solid var(--border-subtle);
}

.badge-claude {
  background: rgba(99, 102, 241, 0.15);
  color: var(--accent-primary);
  border-color: var(--border-active);
}

.badge-codex {
  background: var(--warning-bg);
  color: var(--warning);
  border-color: rgba(245, 158, 11, 0.3);
}

.guide-title {
  font-size: 16px;
  font-weight: 600;
}

.guide-desc {
  font-size: 13px;
  color: var(--text-muted);
}

.config-table {
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: rgba(0, 0, 0, 0.2);
  padding: 14px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
}

.config-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.config-key {
  font-size: 13px;
  color: var(--text-muted);
  font-weight: 500;
}

.config-val-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.guide-note {
  font-size: 12px;
  color: var(--text-dim);
  line-height: 1.5;
}

.code-block-container {
  position: relative;
}

.code-block {
  background: rgba(0, 0, 0, 0.35);
  padding: 14px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
  font-size: 12px;
  color: #a5b4fc;
  overflow-x: auto;
  line-height: 1.6;
}

.copy-float-btn {
  position: absolute;
  top: 10px;
  right: 10px;
}
</style>
