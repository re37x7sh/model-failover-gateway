<template>
  <div class="logs-view">
    <!-- 日志工具栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <h2 class="section-title">实时请求与故障转移追踪</h2>
        <p class="section-subtitle">监控来自 VSCode 插件的每次请求链路，直观查看静默 Failover 切换过程与耗时。</p>
      </div>

      <div class="toolbar-actions">
        <div class="filter-group">
          <button 
            v-for="f in filters" 
            :key="f.id"
            :class="['filter-btn', { active: currentFilter === f.id }]"
            @click="currentFilter = f.id"
          >
            {{ f.label }}
          </button>
        </div>

        <button :class="['btn', autoRefresh ? 'btn-primary' : 'btn-secondary', 'btn-sm']" @click="toggleAutoRefresh">
          <span :class="['status-dot', autoRefresh ? 'active' : 'inactive']"></span>
          <span>{{ autoRefresh ? '自动刷新 (2s)' : '已暂停刷新' }}</span>
        </button>

        <button class="btn btn-secondary btn-sm" @click="$emit('refresh')">🔄 刷新</button>
        <button class="btn btn-secondary btn-sm" @click="openSettingsModal">⚙️ 保留策略</button>
        <button class="btn btn-danger btn-sm" @click="clearLogs">🗑️ 清空日志</button>
      </div>
    </div>

    <!-- 日志保留策略配置弹窗 -->
    <div v-if="showSettingsModal" class="modal-overlay" @click.self="showSettingsModal = false">
      <div class="modal-card">
        <div class="modal-header">
          <h3 class="modal-title">⚙️ 请求日志持久化与清理策略</h3>
          <button class="close-btn" @click="showSettingsModal = false">✕</button>
        </div>

        <div class="modal-body">
          <div class="persistence-tip">
            <span class="tip-icon">💾</span>
            <div class="tip-text">
              <strong>自动持久化存储</strong>：所有请求日志均已实时落盘至 <code>data/request_logs.json</code>，网关服务重启或电脑重启后数据依然完整保留！
            </div>
          </div>

          <div class="form-group">
            <div class="switch-row">
              <label class="form-label mb-0">是否开启自动清理过期日志</label>
              <label class="switch">
                <input type="checkbox" v-model="logSettings.autoCleanupEnabled">
                <span class="slider"></span>
              </label>
            </div>
            <p class="form-hint">开启后将按设定的天数与容量阈值定期自动淘汰最旧的历史记录。</p>
          </div>

          <div class="form-group" v-if="logSettings.autoCleanupEnabled">
            <label class="form-label">历史保留天数 (超过此天数自动清理)</label>
            <select v-model.number="logSettings.retentionDays" class="form-select">
              <option :value="1">保留最近 1 天</option>
              <option :value="3">保留最近 3 天</option>
              <option :value="7">保留最近 7 天 (默认推荐)</option>
              <option :value="14">保留最近 14 天</option>
              <option :value="30">保留最近 30 天</option>
              <option :value="0">永久保留 (不限天数，仅按最大条数限制)</option>
            </select>
          </div>

          <div class="form-group" v-if="logSettings.autoCleanupEnabled">
            <label class="form-label">最大记录条数 (超出自动丢弃最旧数据)</label>
            <select v-model.number="logSettings.maxCapacity" class="form-select">
              <option :value="500">最多保留 500 条</option>
              <option :value="1000">最多保留 1,000 条</option>
              <option :value="2000">最多保留 2,000 条 (默认)</option>
              <option :value="5000">最多保留 5,000 条</option>
              <option :value="10000">最多保留 10,000 条</option>
            </select>
          </div>
        </div>

        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showSettingsModal = false">取消</button>
          <button class="btn btn-primary" @click="saveSettings" :disabled="savingSettings">
            <span>{{ savingSettings ? '保存中...' : '💾 保存清理策略' }}</span>
          </button>
        </div>
      </div>
    </div>

    <!-- 日志列表 -->
    <div v-if="filteredLogs.length === 0" class="glass-card empty-card">
      <div class="empty-text">当前筛选条件下暂无请求日志</div>
    </div>

    <div v-else class="logs-table-container glass-card">
      <table class="logs-table">
        <thead>
          <tr>
            <th>时间</th>
            <th class="col-path">端点路径</th>
            <th class="col-model">请求模型</th>
            <th class="col-trail">故障转移链路 (Failover Trail)</th>
            <th class="col-final">最终处理渠道</th>
            <th>状态码</th>
            <th>总耗时</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="log in filteredLogs" :key="log.id">
            <tr :class="['log-row', { 'row-failover': log.isFailover, 'row-error': log.statusCode >= 400 }]">
              <td class="font-mono text-dim">{{ formatTime(log.timestamp) }}</td>
              <td class="col-path">
                <div class="endpoint-badge" :title="log.requestPath">
                  <span class="method-tag">{{ log.requestMethod }}</span>
                  <span class="path-text font-mono truncate-text">{{ log.requestPath }}</span>
                </div>
              </td>
              <td class="col-model" :title="log.model || '-'">
                <span class="code-tag truncate-text">{{ log.model || '-' }}</span>
              </td>
              <td class="col-trail" :title="getTrailTitle(log)">
                <div v-if="log.triedChannels && log.triedChannels.length > 1" class="failover-trail">
                  <template v-for="(ch, idx) in log.triedChannels" :key="idx">
                    <span :class="['trail-node', idx === log.triedChannels.length - 1 ? 'trail-success' : 'trail-fail']" :title="ch">
                      {{ ch }}
                    </span>
                    <span v-if="idx < log.triedChannels.length - 1" class="trail-arrow">➔</span>
                  </template>
                  <span class="badge badge-warning trail-badge">救急触发</span>
                </div>
                <div v-else-if="log.triedChannels && log.triedChannels.length === 1" class="single-trail">
                  <span class="text-muted truncate-text" :title="log.triedChannels[0]">{{ log.triedChannels[0] }}</span>
                </div>
                <div v-else>
                  <span class="text-dim">-</span>
                </div>
              </td>
              <td class="col-final" :title="log.finalChannel || '无成功响应'">
                <span :class="['final-channel-tag', 'truncate-text', log.finalChannel ? 'channel-active' : 'channel-none']">
                  {{ log.finalChannel || '无成功响应' }}
                </span>
              </td>
              <td>
                <span :class="['badge', log.statusCode >= 200 && log.statusCode < 400 ? 'badge-success' : 'badge-danger']">
                  {{ log.statusCode }}
                </span>
              </td>
              <td class="font-mono">{{ log.durationMs }}ms</td>
              <td>
                <button 
                  v-if="log.errorDetails" 
                  class="btn btn-secondary btn-sm" 
                  @click="toggleExpand(log.id)"
                >
                  {{ expandedRows[log.id] ? '收起详情' : '错误详情' }}
                </button>
              </td>
            </tr>

            <!-- 展开的错误详情 -->
            <tr v-if="expandedRows[log.id]" class="detail-row">
              <td colspan="8">
                <div class="error-detail-box">
                  <div class="detail-title">错误与重试排查信息：</div>
                  <pre class="detail-content">{{ log.errorDetails }}</pre>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, reactive } from 'vue';
import { api } from '../api';

const props = defineProps({
  logs: {
    type: Array,
    required: true
  }
});

const emit = defineEmits(['refresh', 'toast']);

const autoRefresh = ref(true);
let refreshTimer = null;

const currentFilter = ref('all');
const filters = [
  { id: 'all', label: '全部' },
  { id: 'failover', label: '⚡ 仅故障转移' },
  { id: 'error', label: '❌ 仅错误 (4xx/5xx)' },
  { id: 'success', label: '✅ 仅成功' }
];

const expandedRows = reactive({});

function toggleExpand(id) {
  expandedRows[id] = !expandedRows[id];
}

const filteredLogs = computed(() => {
  if (currentFilter.value === 'failover') {
    return props.logs.filter(l => l.isFailover);
  }
  if (currentFilter.value === 'error') {
    return props.logs.filter(l => l.statusCode >= 400);
  }
  if (currentFilter.value === 'success') {
    return props.logs.filter(l => l.statusCode >= 200 && l.statusCode < 400);
  }
  return props.logs;
});

function formatTime(isoString) {
  if (!isoString) return '';
  const date = new Date(isoString);
  return date.toLocaleTimeString('zh-CN', { hour12: false });
}

function toggleAutoRefresh() {
  autoRefresh.value = !autoRefresh.value;
  if (autoRefresh.value) {
    startTimer();
  } else {
    stopTimer();
  }
}

function startTimer() {
  stopTimer();
  refreshTimer = setInterval(() => {
    emit('refresh');
  }, 2000);
}

function stopTimer() {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
}

function getTrailTitle(log) {
  if (log.triedChannels && log.triedChannels.length > 0) {
    return log.triedChannels.join(' ➔ ');
  }
  return '-';
}

const showSettingsModal = ref(false);
const savingSettings = ref(false);
const logSettings = reactive({
  retentionDays: 7,
  maxCapacity: 2000,
  autoCleanupEnabled: true
});

async function openSettingsModal() {
  try {
    const res = await api.getLogSettings();
    if (res) {
      Object.assign(logSettings, res);
    }
  } catch (err) {
    console.error('加载日志清理策略失败:', err);
  }
  showSettingsModal.value = true;
}

async function saveSettings() {
  savingSettings.value = true;
  try {
    await api.saveLogSettings(logSettings);
    emit('toast', '日志保留与清理策略已成功保存！', 'success');
    showSettingsModal.value = false;
    emit('refresh');
  } catch (err) {
    emit('toast', `保存失败: ${err.message}`, 'error');
  } finally {
    savingSettings.value = false;
  }
}

onMounted(() => {
  if (autoRefresh.value) {
    startTimer();
  }
});

onUnmounted(() => {
  stopTimer();
});

async function clearLogs() {
  if (confirm('确定要清空所有代理请求日志吗？磁盘持久化文件也将被完全清空。')) {
    try {
      await api.clearLogs();
      emit('toast', '请求日志与磁盘文件已成功清空', 'info');
      emit('refresh');
    } catch (err) {
      emit('toast', `清空失败: ${err.message}`, 'error');
    }
  }
}
</script>

<style scoped>
.logs-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  flex-wrap: wrap;
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

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.filter-group {
  display: flex;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  padding: 2px;
}

.filter-btn {
  padding: 4px 10px;
  font-size: 12px;
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  border-radius: var(--radius-sm);
  transition: all 0.15s;
}

.filter-btn.active {
  background: var(--accent-primary);
  color: #fff;
  font-weight: 600;
}

.empty-card {
  padding: 40px;
  text-align: center;
  color: var(--text-muted);
}

.logs-table-container {
  overflow-x: auto;
  padding: 8px;
}

.logs-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  text-align: left;
}

.logs-table th {
  padding: 12px 14px;
  font-weight: 600;
  color: var(--text-muted);
  border-bottom: 1px solid var(--border-subtle);
  white-space: nowrap;
}

.logs-table td {
  padding: 12px 14px;
  border-bottom: 1px solid var(--border-subtle);
  white-space: nowrap;
}

.log-row:hover {
  background: rgba(255, 255, 255, 0.02);
}

.row-failover {
  background: rgba(245, 158, 11, 0.04);
}

.endpoint-badge {
  display: flex;
  align-items: center;
  gap: 6px;
}

.method-tag {
  font-size: 11px;
  font-weight: 700;
  color: var(--accent-primary);
  font-family: var(--font-mono);
}

.path-text {
  color: var(--text-main);
}

.col-trail {
  max-width: 220px;
  min-width: 140px;
}

.col-final {
  max-width: 180px;
  min-width: 120px;
}

.col-model {
  max-width: 160px;
  min-width: 100px;
}

.col-path {
  max-width: 220px;
  min-width: 130px;
}

.truncate-text {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
}

.single-trail {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: block;
}

.failover-trail {
  display: flex;
  align-items: center;
  gap: 4px;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.trail-node {
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 500;
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: inline-block;
  vertical-align: middle;
}

.trail-fail {
  background: var(--danger-bg);
  color: var(--danger);
  text-decoration: line-through;
}

.trail-success {
  background: var(--success-bg);
  color: var(--success);
}

.trail-arrow {
  color: var(--warning);
  font-size: 10px;
}

.final-channel-tag {
  font-weight: 500;
}

.channel-active {
  color: var(--text-main);
}

.channel-none {
  color: var(--danger);
}

.detail-row td {
  padding: 0;
  background: rgba(0, 0, 0, 0.2);
}

.error-detail-box {
  padding: 14px 20px;
  font-size: 12px;
}

.detail-title {
  color: var(--danger);
  font-weight: 600;
  margin-bottom: 6px;
}

.detail-content {
  font-family: var(--font-mono);
  color: var(--text-muted);
  white-space: pre-wrap;
  word-break: break-all;
  background: rgba(0, 0, 0, 0.3);
  padding: 10px;
  border-radius: var(--radius-sm);
  border: 1px solid var(--border-subtle);
}

.persistence-tip {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  background: rgba(137, 180, 250, 0.1);
  border: 1px solid rgba(137, 180, 250, 0.25);
  border-radius: var(--radius-md);
  padding: 12px 14px;
  margin-bottom: 20px;
}

.tip-icon {
  font-size: 22px;
  line-height: 1;
}

.tip-text {
  font-size: 13px;
  color: var(--text-main);
  line-height: 1.5;
}

.tip-text code {
  background: rgba(0, 0, 0, 0.3);
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 12px;
  color: #89b4fa;
}

.switch-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}

.mb-0 {
  margin-bottom: 0 !important;
}
</style>
