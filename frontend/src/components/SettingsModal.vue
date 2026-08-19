<template>
  <div v-if="modelValue" class="modal-overlay" @click.self="closeModal">
    <div class="glass-card modal-container">
      <div class="modal-header">
        <div class="modal-title-row">
          <span class="modal-icon">⚙️</span>
          <h3 class="modal-title">系统管理与一键接管中心</h3>
        </div>
        <button class="close-btn" @click="closeModal">✕</button>
      </div>

      <!-- Tab 切换 -->
      <div class="modal-tabs">
        <button 
          :class="['modal-tab-btn', { active: activeTab === 'injection' }]" 
          @click="activeTab = 'injection'"
        >
          <span>⚡ 客户端一键接管 & 端口</span>
        </button>
        <button 
          :class="['modal-tab-btn', { active: activeTab === 'syslogs' }]" 
          @click="switchToLogsTab"
        >
          <span>💻 系统级运行日志</span>
          <span v-if="sysLogs.length > 0" class="tab-badge">{{ sysLogs.length }}</span>
        </button>
      </div>

      <div class="modal-body">
        <!-- ==================== Tab 1: 一键接管与端口设置 ==================== -->
        <div v-if="activeTab === 'injection'" class="tab-content">
          <!-- 快捷全局批量操作 -->
          <div class="global-actions-bar">
            <div class="bar-left">
              <span class="bar-title">一键全局托管</span>
              <span class="bar-desc">自动将本地所有 AI 编程插件端点固定指向本地网关</span>
            </div>
            <div class="bar-right">
              <button class="btn btn-primary btn-sm" @click="handleInject('all')" :disabled="loading">
                <span>⚡ 一键接管所有客户端</span>
              </button>
              <button class="btn btn-secondary btn-sm" @click="handleRestore('all')" :disabled="loading">
                <span>🔄 一键全部安全还原</span>
              </button>
            </div>
          </div>

          <!-- 各客户端状态卡片列表 -->
          <div class="client-cards-grid">
            <!-- 1. Claude Code CLI -->
            <div class="client-card">
              <div class="client-card-header">
                <div class="client-title-box">
                  <span class="client-name">Claude Code (CLI 终端 / 插件)</span>
                  <span :class="['badge', sysStatus.claudeCode?.isInjected ? 'badge-success' : 'badge-muted']">
                    {{ sysStatus.claudeCode?.isInjected ? '🟢 已接管' : '⚪ 未接管' }}
                  </span>
                </div>
                <div class="client-actions">
                  <button 
                    class="btn btn-primary btn-xs" 
                    @click="handleInject('claude')" 
                    :disabled="loading"
                  >
                    接管
                  </button>
                  <button 
                    class="btn btn-secondary btn-xs" 
                    @click="handleRestore('claude')" 
                    :disabled="loading || !sysStatus.claudeCode?.hasBackup"
                    title="从备份还原原始配置"
                  >
                    还原
                  </button>
                </div>
              </div>
              <p class="client-desc">自动接管 <code>~/.claude/settings.json</code> 并注入 Windows 用户环境变量 <code>ANTHROPIC_BASE_URL</code>。</p>
              <div class="client-details-row">
                <span class="detail-text">{{ sysStatus.claudeCode?.details }}</span>
                <span v-if="sysStatus.claudeCode?.hasBackup" class="backup-tag">已备份原配置</span>
              </div>
            </div>

            <!-- 2. CodeX / ChatGPT 插件 & 环境变量 -->
            <div class="client-card">
              <div class="client-card-header">
                <div class="client-title-box">
                  <span class="client-name">CodeX / ChatGPT (CLI / 插件 / 环境)</span>
                  <span :class="['badge', sysStatus.codex?.isInjected ? 'badge-success' : 'badge-muted']">
                    {{ sysStatus.codex?.isInjected ? '🟢 已接管' : '⚪ 未接管' }}
                  </span>
                </div>
                <div class="client-actions">
                  <button 
                    class="btn btn-primary btn-xs" 
                    @click="handleInject('codex')" 
                    :disabled="loading"
                  >
                    接管
                  </button>
                  <button 
                    class="btn btn-secondary btn-xs" 
                    @click="handleRestore('codex')" 
                    :disabled="loading || !sysStatus.codex?.hasBackup"
                    title="从备份还原原始配置"
                  >
                    还原
                  </button>
                </div>
              </div>
              <p class="client-desc">自动接管 <code>~/.codex/config.toml</code>、VSCode 插件设置与 Windows 环境变量。</p>
              
              <!-- 可配置 model_provider 对应名称 -->
              <div class="provider-custom-row">
                <span class="provider-label">TOML Provider 名称:</span>
                <input 
                  v-model="codexProviderName" 
                  class="form-input font-mono provider-input" 
                  placeholder="默认 gateway"
                  title="写入 ~/.codex/config.toml 的 model_provider 名称 (例如: gateway / super / custom)"
                />
              </div>

              <div class="client-details-row">
                <span class="detail-text">{{ sysStatus.codex?.details }}</span>
                <span v-if="sysStatus.codex?.hasBackup" class="backup-tag">已备份原配置</span>
              </div>
            </div>

            <!-- 3. VSCode 全局设置 -->
            <div class="client-card">
              <div class="client-card-header">
                <div class="client-title-box">
                  <span class="client-name">VSCode 全局设置 (settings.json)</span>
                  <span :class="['badge', sysStatus.vscode?.isInjected ? 'badge-success' : 'badge-muted']">
                    {{ sysStatus.vscode?.isInjected ? '🟢 已接管' : '⚪ 未接管' }}
                  </span>
                </div>
                <div class="client-actions">
                  <button 
                    class="btn btn-primary btn-xs" 
                    @click="handleInject('vscode')" 
                    :disabled="loading"
                  >
                    接管
                  </button>
                  <button 
                    class="btn btn-secondary btn-xs" 
                    @click="handleRestore('vscode')" 
                    :disabled="loading || !sysStatus.vscode?.hasBackup"
                  >
                    还原
                  </button>
                </div>
              </div>
              <p class="client-desc">自动在 <code>Code\User\settings.json</code> 中注入 Claude 与 CodeX 本地端点。</p>
              <div class="client-details-row">
                <span class="detail-text">{{ sysStatus.vscode?.details }}</span>
                <span v-if="sysStatus.vscode?.hasBackup" class="backup-tag">已备份原配置</span>
              </div>
            </div>

            <!-- 3. Continue 插件 -->
            <div class="client-card">
              <div class="client-card-header">
                <div class="client-title-box">
                  <span class="client-name">Continue 扩展 (~/.continue/config.json)</span>
                  <span :class="['badge', sysStatus.continue?.isInjected ? 'badge-success' : 'badge-muted']">
                    {{ sysStatus.continue?.isInjected ? '🟢 已接管' : '⚪ 未接管' }}
                  </span>
                </div>
                <div class="client-actions">
                  <button 
                    class="btn btn-primary btn-xs" 
                    @click="handleInject('continue')" 
                    :disabled="loading"
                  >
                    接管
                  </button>
                  <button 
                    class="btn btn-secondary btn-xs" 
                    @click="handleRestore('continue')" 
                    :disabled="loading || !sysStatus.continue?.hasBackup"
                  >
                    还原
                  </button>
                </div>
              </div>
              <p class="client-desc">自动修改 <code>config.json</code> 中的 models 列表，将 <code>apiBase</code> 指向本地网关。</p>
              <div class="client-details-row">
                <span class="detail-text">{{ sysStatus.continue?.details }}</span>
                <span v-if="sysStatus.continue?.hasBackup" class="backup-tag">已备份原配置</span>
              </div>
            </div>
          </div>

          <!-- 自定义端口设置区 -->
          <div class="port-config-section">
            <h4 class="sub-section-title">🌐 网关监听代理端口</h4>
            <div class="port-form-row">
              <div class="port-input-wrapper">
                <span class="port-prefix font-mono">http://127.0.0.1 :</span>
                <input 
                  v-model.number="customPort" 
                  type="number" 
                  class="form-input font-mono port-input" 
                  min="1024" 
                  max="65535" 
                  placeholder="5000"
                />
              </div>
              <button class="btn btn-primary btn-sm" @click="savePort" :disabled="savingPort || customPort === sysStatus.port">
                <span v-if="savingPort">保存中...</span>
                <span v-else>💾 保存新端口</span>
              </button>
            </div>
            <p class="port-tip">
              💡 提示：当前运行端口为 <strong>{{ sysStatus.port }}</strong>。修改端口并保存后，请重启网关（退出托盘后重新运行）以生效新端口。
            </p>
          </div>
        </div>

        <!-- ==================== Tab 2: 系统级运行日志 ==================== -->
        <div v-else-if="activeTab === 'syslogs'" class="tab-content logs-tab-content">
          <!-- 日志操作工具栏 -->
          <div class="logs-toolbar">
            <div class="logs-filter-group">
              <span class="filter-label">级别:</span>
              <div class="filter-pills">
                <button 
                  v-for="lvl in logLevels" 
                  :key="lvl"
                  :class="['pill-btn', { active: selectedLevel === lvl }]"
                  @click="selectedLevel = lvl; fetchSysLogs()"
                >
                  {{ lvl }}
                </button>
              </div>
            </div>

            <div class="logs-toolbar-actions">
              <label class="auto-refresh-label" title="开启后每 2 秒自动获取最新日志">
                <input type="checkbox" v-model="autoRefreshLogs" @change="toggleAutoRefresh" />
                <span>实时刷新</span>
              </label>
              <button class="btn btn-secondary btn-xs" @click="fetchSysLogs" :disabled="fetchingLogs">
                <span>🔄 刷新</span>
              </button>
              <button class="btn btn-secondary btn-xs" @click="copyAllLogs">
                <span>📋 复制全部</span>
              </button>
              <button class="btn btn-danger btn-xs" @click="clearAllLogs">
                <span>🗑️ 清空</span>
              </button>
            </div>
          </div>

          <!-- 终端控制台视窗 -->
          <div class="terminal-container font-mono" ref="terminalRef">
            <div v-if="sysLogs.length === 0" class="terminal-empty">
              > 暂无系统级日志记录...
            </div>
            <div 
              v-for="log in sysLogs" 
              :key="log.id" 
              :class="['terminal-line', `line-${log.level.toLowerCase()}`]"
            >
              <span class="term-time">[{{ formatTime(log.timestamp) }}]</span>
              <span :class="['term-badge', `badge-${log.level.toLowerCase()}`]">[{{ log.level }}]</span>
              <span class="term-category" v-if="log.category">[{{ log.category }}]</span>
              <span class="term-msg">{{ log.message }}</span>
              <div v-if="log.exception" class="term-exception">
                {{ log.exception }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, onUnmounted, watch } from 'vue';
import { api } from '../api';

const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['update:modelValue', 'toast', 'refresh']);

const activeTab = ref('injection');
const loading = ref(false);
const savingPort = ref(false);
const fetchingLogs = ref(false);
const autoRefreshLogs = ref(true);

const sysStatus = reactive({
  port: 5000,
  claudeCode: {},
  vscode: {},
  continue: {}
});

const customPort = ref(5000);
const codexProviderName = ref('gateway');
const sysLogs = ref([]);
const selectedLevel = ref('ALL');
const logLevels = ['ALL', 'INFO', 'WARN', 'ERROR', 'DEBUG'];
const terminalRef = ref(null);
let refreshTimer = null;

function closeModal() {
  emit('update:modelValue', false);
}

async function loadSystemStatus() {
  try {
    const data = await api.getSystemStatus();
    Object.assign(sysStatus, data);
    customPort.value = data.port || 5000;
  } catch (err) {
    console.error('获取系统状态失败:', err);
  }
}

async function handleInject(target) {
  loading.value = true;
  try {
    const group = target === 'codex' ? 'codex' : 'claude';
    const pName = codexProviderName.value ? codexProviderName.value.trim() : 'gateway';
    await api.injectConfig(target, customPort.value, group, pName);
    emit('toast', `成功接管 [${target === 'all' ? '所有客户端' : target}] 配置！`, 'success');
    await loadSystemStatus();
  } catch (err) {
    emit('toast', `接管配置失败: ${err.message}`, 'error');
  } finally {
    loading.value = false;
  }
}

async function handleRestore(target) {
  loading.value = true;
  try {
    await api.restoreConfig(target);
    emit('toast', `已成功从备份还原 [${target === 'all' ? '所有客户端' : target}] 配置`, 'info');
    await loadSystemStatus();
  } catch (err) {
    emit('toast', `还原配置失败: ${err.message}`, 'error');
  } finally {
    loading.value = false;
  }
}

async function savePort() {
  if (customPort.value < 1024 || customPort.value > 65535) {
    emit('toast', '端口号必须在 1024 - 65535 之间', 'warning');
    return;
  }
  savingPort.value = true;
  try {
    await api.setPort(customPort.value);
    emit('toast', `端口配置已更新为 ${customPort.value}，请重启网关生效`, 'success');
    await loadSystemStatus();
  } catch (err) {
    emit('toast', `保存端口失败: ${err.message}`, 'error');
  } finally {
    savingPort.value = false;
  }
}

async function fetchSysLogs() {
  fetchingLogs.value = true;
  try {
    const logs = await api.getSystemLogs(200, selectedLevel.value);
    sysLogs.value = logs;
  } catch (err) {
    console.error('获取系统日志失败:', err);
  } finally {
    fetchingLogs.value = false;
  }
}

async function clearAllLogs() {
  try {
    await api.clearSystemLogs();
    sysLogs.value = [];
    emit('toast', '系统级日志已清空', 'info');
  } catch (err) {
    emit('toast', `清空日志失败: ${err.message}`, 'error');
  }
}

async function copyAllLogs() {
  if (sysLogs.value.length === 0) return;
  const text = sysLogs.value.map(l => 
    `[${formatTime(l.timestamp)}] [${l.level}] [${l.category}] ${l.message} ${l.exception || ''}`
  ).join('\n');

  try {
    await navigator.clipboard.writeText(text);
    emit('toast', '系统日志已全部复制到剪贴板', 'success');
  } catch (err) {
    emit('toast', '复制失败', 'error');
  }
}

function switchToLogsTab() {
  activeTab.value = 'syslogs';
  fetchSysLogs();
  toggleAutoRefresh();
}

function toggleAutoRefresh() {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
  if (autoRefreshLogs.value && activeTab.value === 'syslogs') {
    refreshTimer = setInterval(() => {
      fetchSysLogs();
    }, 2000);
  }
}

function formatTime(timeStr) {
  if (!timeStr) return '';
  const d = new Date(timeStr);
  return d.toTimeString().split(' ')[0] + '.' + String(d.getMilliseconds()).padStart(3, '0');
}

watch(() => props.modelValue, (val) => {
  if (val) {
    loadSystemStatus();
    if (activeTab.value === 'syslogs') {
      fetchSysLogs();
      toggleAutoRefresh();
    }
  } else {
    if (refreshTimer) {
      clearInterval(refreshTimer);
      refreshTimer = null;
    }
  }
});

onMounted(() => {
  if (props.modelValue) {
    loadSystemStatus();
  }
});

onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
  }
});
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(8px);
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

.modal-container {
  width: 100%;
  max-width: 780px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  padding: 24px;
  background: var(--bg-surface);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--border-subtle);
  animation: modal-scale 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes modal-scale {
  from { opacity: 0; transform: scale(0.96); }
  to { opacity: 1; transform: scale(1); }
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.modal-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.modal-icon {
  font-size: 20px;
}

.modal-title {
  font-size: 18px;
  font-weight: 700;
}

.close-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 16px;
  cursor: pointer;
}

.modal-tabs {
  display: flex;
  gap: 8px;
  border-bottom: 1px solid var(--border-subtle);
  padding-bottom: 12px;
  margin-bottom: 20px;
}

.modal-tab-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
}

.modal-tab-btn.active {
  background: rgba(99, 102, 241, 0.15);
  border-color: var(--border-active);
  color: var(--accent-primary);
}

.tab-badge {
  background: rgba(99, 102, 241, 0.3);
  color: var(--text-main);
  padding: 1px 6px;
  border-radius: var(--radius-full);
  font-size: 11px;
}

.modal-body {
  overflow-y: auto;
  flex: 1;
}

.tab-content {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* 全局操作栏 */
.global-actions-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.12), rgba(168, 85, 247, 0.08));
  border: 1px solid var(--border-active);
  border-radius: var(--radius-md);
  flex-wrap: wrap;
  gap: 12px;
}

.bar-title {
  font-size: 15px;
  font-weight: 700;
  color: var(--text-main);
  display: block;
}

.bar-desc {
  font-size: 12px;
  color: var(--text-dim);
  margin-top: 2px;
}

.bar-right {
  display: flex;
  gap: 10px;
}

/* 客户端卡片网格 */
.client-cards-grid {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.client-card {
  padding: 16px;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.client-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.client-title-box {
  display: flex;
  align-items: center;
  gap: 10px;
}

.client-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.client-actions {
  display: flex;
  gap: 8px;
}

.client-desc {
  font-size: 12px;
  color: var(--text-muted);
  margin: 0;
}

.client-details-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 11px;
  color: var(--text-dim);
}

.backup-tag {
  color: var(--accent-primary);
  font-size: 11px;
}

/* 端口设置区 */
.port-config-section {
  padding: 16px;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.sub-section-title {
  font-size: 14px;
  font-weight: 600;
  margin: 0;
}

.port-form-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.port-input-wrapper {
  display: flex;
  align-items: center;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  padding: 2px 8px;
}

.port-prefix {
  font-size: 13px;
  color: var(--text-muted);
}

.port-input {
  width: 90px;
  background: transparent;
  border: none;
  outline: none;
  font-size: 14px;
  font-weight: 700;
  color: var(--accent-primary);
}

.port-tip {
  font-size: 12px;
  color: var(--text-dim);
  margin: 0;
  line-height: 1.5;
}

/* Tab 2: 系统级日志面板 */
.logs-tab-content {
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: 520px;
}

.logs-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.logs-filter-group {
  display: flex;
  align-items: center;
  gap: 8px;
}

.filter-label {
  font-size: 12px;
  color: var(--text-muted);
}

.filter-pills {
  display: flex;
  gap: 4px;
  background: rgba(0, 0, 0, 0.25);
  padding: 2px;
  border-radius: 4px;
}

.pill-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 11px;
  padding: 3px 8px;
  border-radius: 3px;
  cursor: pointer;
  font-family: var(--font-mono);
}

.pill-btn.active {
  background: var(--accent-primary);
  color: #fff;
  font-weight: 700;
}

.logs-toolbar-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.auto-refresh-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
  cursor: pointer;
}

/* 终端控制台视窗 */
.terminal-container {
  flex: 1;
  background: #11111b;
  border: 1px solid #313244;
  border-radius: var(--radius-md);
  padding: 14px;
  overflow-y: auto;
  font-size: 12px;
  line-height: 1.6;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.terminal-empty {
  color: #6c7086;
  padding: 20px;
  text-align: center;
}

.terminal-line {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  word-break: break-all;
}

.term-time {
  color: #6c7086;
  flex-shrink: 0;
}

.term-badge {
  font-weight: 700;
  flex-shrink: 0;
}

.badge-info { color: #89b4fa; }
.badge-warn { color: #f9e2af; }
.badge-error { color: #f38ba8; }
.badge-fatal { color: #eba0ac; }
.badge-debug { color: #cdd6f4; }

.term-category {
  color: #a6adc8;
  flex-shrink: 0;
}

.term-msg {
  color: #cdd6f4;
  flex: 1;
}

.line-error .term-msg { color: #f38ba8; }
.line-warn .term-msg { color: #f9e2af; }

.term-exception {
  width: 100%;
  color: #f38ba8;
  background: rgba(243, 139, 168, 0.1);
  padding: 6px 10px;
  border-radius: 4px;
  margin-top: 4px;
  font-size: 11px;
  white-space: pre-wrap;
}

.btn-xs {
  padding: 3px 8px;
  font-size: 11px;
}

.provider-custom-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 8px 0 6px;
  padding: 6px 10px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  border: 1px dashed var(--border-subtle);
}

.provider-label {
  font-size: 12px;
  color: var(--text-muted);
  white-space: nowrap;
}

.provider-input {
  height: 26px;
  font-size: 12px;
  padding: 2px 8px;
  max-width: 140px;
}
</style>
