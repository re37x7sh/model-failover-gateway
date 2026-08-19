<template>
  <div class="token-stats-view">
    <!-- 顶部 KPI 指标卡片 -->
    <div class="kpi-grid">
      <div class="kpi-card highlight-card">
        <div class="kpi-icon-box">💎</div>
        <div class="kpi-content">
          <div class="kpi-label">累计消耗 Total Tokens</div>
          <div class="kpi-value font-mono">{{ formatNumber(summary.totalTokens) }}</div>
          <div class="kpi-subtext">全渠道累计 Token 吞吐</div>
        </div>
      </div>

      <div class="kpi-card">
        <div class="kpi-icon-box input-icon">📥</div>
        <div class="kpi-content">
          <div class="kpi-label">Prompt 消耗 (Input)</div>
          <div class="kpi-value font-mono">{{ formatNumber(summary.promptTokens) }}</div>
          <div class="kpi-subtext">占总消耗 {{ getPercent(summary.promptTokens, summary.totalTokens) }}%</div>
        </div>
      </div>

      <div class="kpi-card">
        <div class="kpi-icon-box output-icon">📤</div>
        <div class="kpi-content">
          <div class="kpi-label">Completion 消耗 (Output)</div>
          <div class="kpi-value font-mono">{{ formatNumber(summary.completionTokens) }}</div>
          <div class="kpi-subtext">占总消耗 {{ getPercent(summary.completionTokens, summary.totalTokens) }}%</div>
        </div>
      </div>

      <div class="kpi-card">
        <div class="kpi-icon-box today-icon">📅</div>
        <div class="kpi-content">
          <div class="kpi-label">今日消耗 (Today)</div>
          <div class="kpi-value font-mono text-success">{{ formatNumber(summary.todayTokens) }}</div>
          <div class="kpi-subtext">今日 UTC 累计 Token</div>
        </div>
      </div>

      <div class="kpi-card">
        <div class="kpi-icon-box req-icon">⚡</div>
        <div class="kpi-content">
          <div class="kpi-label">统计记录请求数</div>
          <div class="kpi-value font-mono">{{ formatNumber(summary.totalRequests) }}</div>
          <div class="kpi-subtext">平均 {{ getAvgTokens() }} tokens/次</div>
        </div>
      </div>
    </div>

    <!-- 统计维度切换与工具栏 -->
    <div class="stats-panel card">
      <div class="panel-header">
        <div class="tabs-box">
          <button 
            :class="['dimension-tab', activeTab === 'channel' ? 'active' : '']"
            @click="activeTab = 'channel'"
          >
            🏷️ 渠道维度汇总 ({{ channelStats.length }})
          </button>
          <button 
            :class="['dimension-tab', activeTab === 'key' ? 'active' : '']"
            @click="activeTab = 'key'"
          >
            🔑 渠道 + Key 细分明细 ({{ filteredKeyStats.length }})
          </button>
        </div>

        <div class="panel-actions">
          <!-- 筛选渠道下拉框 -->
          <div class="filter-item" v-if="activeTab === 'key'">
            <label class="filter-label">筛选渠道:</label>
            <select v-model="selectedChannelFilter" class="form-select filter-select">
              <option value="">全部渠道 (All Channels)</option>
              <option v-for="ch in channelList" :key="ch.channelId" :value="ch.channelId">
                {{ ch.channelName }} ({{ getGroupLabel(ch.group) }})
              </option>
            </select>
          </div>

          <!-- 搜索 Key 关键词 -->
          <div class="search-box" v-if="activeTab === 'key'">
            <input 
              v-model="searchKeyQuery" 
              class="form-input search-input font-mono" 
              placeholder="搜索 Key 前后缀..." 
            />
          </div>

          <button class="btn btn-secondary btn-sm" @click="loadData" :disabled="loading">
            <span>🔄 刷新</span>
          </button>
          <button class="btn btn-danger-outline btn-sm" @click="confirmClear" :disabled="summary.totalTokens === 0">
            <span>🗑️ 清空统计</span>
          </button>
        </div>
      </div>

      <!-- 维度 1: 渠道维度汇总表格 -->
      <div v-if="activeTab === 'channel'" class="table-container">
        <table class="data-table">
          <thead>
            <tr>
              <th>渠道名称</th>
              <th>所属分组</th>
              <th>总 Token 消耗 (占比)</th>
              <th>Prompt (输入)</th>
              <th>Completion (输出)</th>
              <th>请求次数</th>
              <th>Key 数量</th>
              <th>最近活跃时间</th>
              <th style="text-align: right;">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="channelStats.length === 0">
              <td colspan="9" class="empty-cell">
                <div class="empty-box">
                  <span class="empty-icon">📊</span>
                  <span>暂无 Token 消耗数据。发送请求后将实时自动统计！</span>
                </div>
              </td>
            </tr>
            <tr v-for="item in channelStats" :key="item.channelId" class="data-row">
              <td class="font-bold channel-name-cell">
                <span>{{ item.channelName }}</span>
              </td>
              <td>
                <span :class="['badge', getGroupBadgeClass(item.group)]">
                  {{ getGroupLabel(item.group) }}
                </span>
              </td>
              <td>
                <div class="token-progress-cell">
                  <span class="font-mono font-bold">{{ formatNumber(item.totalTokens) }}</span>
                  <div class="mini-bar-bg">
                    <div 
                      class="mini-bar-fill" 
                      :style="{ width: `${getPercent(item.totalTokens, summary.totalTokens)}%` }"
                    ></div>
                  </div>
                  <span class="ratio-text">{{ getPercent(item.totalTokens, summary.totalTokens) }}%</span>
                </div>
              </td>
              <td class="font-mono text-muted">{{ formatNumber(item.promptTokens) }}</td>
              <td class="font-mono text-muted">{{ formatNumber(item.completionTokens) }}</td>
              <td class="font-mono font-bold">{{ formatNumber(item.requestCount) }}</td>
              <td>
                <span class="key-count-tag font-mono">🔑 {{ item.keyCount }} 个 Key</span>
              </td>
              <td class="text-muted font-mono" style="font-size: 12px;">
                {{ formatTime(item.lastUsed) }}
              </td>
              <td style="text-align: right;">
                <button class="btn btn-secondary btn-xs" @click="viewChannelKeys(item.channelId)">
                  查看 Key 明细 ➔
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 维度 2: 渠道 + Key 细分维度明细表格 -->
      <div v-if="activeTab === 'key'" class="table-container">
        <table class="data-table">
          <thead>
            <tr>
              <th>所属渠道</th>
              <th>分组</th>
              <th>API Key (脱敏标识)</th>
              <th>总 Token 消耗</th>
              <th>Prompt (输入)</th>
              <th>Completion (输出)</th>
              <th>请求次数</th>
              <th>最近活跃时间</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filteredKeyStats.length === 0">
              <td colspan="8" class="empty-cell">
                <div class="empty-box">
                  <span class="empty-icon">🔑</span>
                  <span>未找到匹配的 Key 消耗数据</span>
                </div>
              </td>
            </tr>
            <tr v-for="(item, idx) in filteredKeyStats" :key="idx" class="data-row">
              <td class="font-bold">{{ item.channelName }}</td>
              <td>
                <span :class="['badge', getGroupBadgeClass(item.group)]">
                  {{ getGroupLabel(item.group) }}
                </span>
              </td>
              <td>
                <div class="key-chip">
                  <span class="font-mono key-text">{{ item.maskedKey }}</span>
                  <button 
                    class="copy-mini-btn" 
                    @click="copyKey(item.maskedKey)" 
                    title="复制脱敏 Key 标识"
                  >
                    📋
                  </button>
                </div>
              </td>
              <td>
                <span class="font-mono font-bold token-total-val">{{ formatNumber(item.totalTokens) }}</span>
              </td>
              <td class="font-mono text-muted">{{ formatNumber(item.promptTokens) }}</td>
              <td class="font-mono text-muted">{{ formatNumber(item.completionTokens) }}</td>
              <td class="font-mono font-bold">{{ formatNumber(item.requestCount) }}</td>
              <td class="text-muted font-mono" style="font-size: 12px;">
                {{ formatTime(item.lastUsed) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { api } from '../api';

const emit = defineEmits(['toast']);

const activeTab = ref('channel');
const loading = ref(false);
const selectedChannelFilter = ref('');
const searchKeyQuery = ref('');

const summary = ref({
  totalTokens: 0,
  promptTokens: 0,
  completionTokens: 0,
  todayTokens: 0,
  totalRequests: 0
});

const channelStats = ref([]);
const keyStats = ref([]);
let pollTimer = null;

const channelList = computed(() => {
  return channelStats.value.map(c => ({
    channelId: c.channelId,
    channelName: c.channelName,
    group: c.group
  }));
});

const filteredKeyStats = computed(() => {
  let list = keyStats.value;
  if (selectedChannelFilter.value) {
    list = list.filter(k => k.channelId === selectedChannelFilter.value);
  }
  if (searchKeyQuery.value.trim()) {
    const q = searchKeyQuery.value.trim().toLowerCase();
    list = list.filter(k => 
      k.maskedKey.toLowerCase().includes(q) || 
      k.channelName.toLowerCase().includes(q)
    );
  }
  return list;
});

async function loadData() {
  loading.value = true;
  try {
    const [sumData, chData, keyData] = await Promise.all([
      api.getTokenSummary(),
      api.getChannelTokenStats(),
      api.getKeyTokenStats()
    ]);
    summary.value = sumData || summary.value;
    channelStats.value = chData || [];
    keyStats.value = keyData || [];
  } catch (err) {
    console.error('加载 Token 统计失败:', err);
  } finally {
    loading.value = false;
  }
}

function viewChannelKeys(channelId) {
  selectedChannelFilter.value = channelId;
  activeTab.value = 'key';
}

async function confirmClear() {
  if (confirm('确定要清空所有 Token 消耗历史记录吗？此操作无法撤销。')) {
    try {
      await api.clearTokenStats();
      emit('toast', '已清空所有 Token 统计记录', 'info');
      await loadData();
    } catch (err) {
      emit('toast', `清空失败: ${err.message}`, 'error');
    }
  }
}

async function copyKey(text) {
  try {
    await navigator.clipboard.writeText(text);
    emit('toast', `已复制: ${text}`, 'success');
  } catch {
    emit('toast', '复制失败', 'error');
  }
}

function formatNumber(num) {
  if (num === null || num === undefined) return '0';
  return Number(num).toLocaleString();
}

function getPercent(part, total) {
  if (!total || total === 0) return '0';
  return ((part / total) * 100).toFixed(1);
}

function getAvgTokens() {
  if (!summary.value.totalRequests || summary.value.totalRequests === 0) return '0';
  return Math.round(summary.value.totalTokens / summary.value.totalRequests).toLocaleString();
}

function getGroupLabel(group) {
  const g = (group || 'all').toLowerCase();
  if (g === 'claude') return 'Claude 专用';
  if (g === 'codex') return 'Codex 专用';
  return '通用渠道';
}

function getGroupBadgeClass(group) {
  const g = (group || 'all').toLowerCase();
  if (g === 'claude') return 'badge-claude';
  if (g === 'codex') return 'badge-codex';
  return 'badge-all';
}

function formatTime(isoStr) {
  if (!isoStr) return '-';
  const d = new Date(isoStr);
  return d.toLocaleString('zh-CN', { hour12: false });
}

onMounted(() => {
  loadData();
  // 5 秒自动平滑更新统计
  pollTimer = setInterval(loadData, 5000);
});

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer);
});
</script>

<style scoped>
.token-stats-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* 顶部 5 列 KPI 卡片网格 */
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.kpi-card {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 16px;
  display: flex;
  align-items: center;
  gap: 14px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  transition: transform 0.2s, border-color 0.2s;
}

.kpi-card:hover {
  transform: translateY(-2px);
  border-color: var(--border-focus);
}

.highlight-card {
  background: linear-gradient(135deg, rgba(137, 180, 250, 0.12), rgba(203, 166, 247, 0.08));
  border-color: rgba(137, 180, 250, 0.35);
}

.kpi-icon-box {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.06);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  flex-shrink: 0;
}

.input-icon { background: rgba(137, 180, 250, 0.15); color: #89b4fa; }
.output-icon { background: rgba(203, 166, 247, 0.15); color: #cba6f7; }
.today-icon { background: rgba(166, 227, 161, 0.15); color: #a6e3a1; }
.req-icon { background: rgba(249, 226, 175, 0.15); color: #f9e2af; }

.kpi-content {
  display: flex;
  flex-direction: column;
  gap: 2px;
  overflow: hidden;
}

.kpi-label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.kpi-value {
  font-size: 20px;
  font-weight: 800;
  color: var(--text-main);
  letter-spacing: -0.5px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.kpi-subtext {
  font-size: 11px;
  color: var(--text-dim);
}

/* 主面板容器 */
.stats-panel {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.panel-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-subtle);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}

.tabs-box {
  display: flex;
  background: rgba(0, 0, 0, 0.25);
  padding: 4px;
  border-radius: var(--radius-md);
  gap: 4px;
}

.dimension-tab {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 600;
  padding: 6px 14px;
  border-radius: var(--radius-sm);
  cursor: pointer;
  transition: all 0.2s;
}

.dimension-tab.active {
  background: var(--accent-primary);
  color: #fff;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.25);
}

.panel-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.filter-item {
  display: flex;
  align-items: center;
  gap: 6px;
}

.filter-label {
  font-size: 12px;
  color: var(--text-muted);
}

.filter-select {
  height: 32px;
  font-size: 12px;
  padding: 4px 10px;
  background: var(--bg-hover);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
}

.search-input {
  height: 32px;
  font-size: 12px;
  padding: 4px 10px;
  width: 170px;
}

/* 数据表格 */
.table-container {
  overflow-x: auto;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  text-align: left;
}

.data-table th {
  background: rgba(255, 255, 255, 0.02);
  color: var(--text-muted);
  font-weight: 600;
  padding: 12px 18px;
  border-bottom: 1px solid var(--border-subtle);
}

.data-table td {
  padding: 14px 18px;
  border-bottom: 1px solid var(--border-subtle);
  vertical-align: middle;
}

.data-row:hover {
  background: rgba(255, 255, 255, 0.025);
}

.channel-name-cell {
  color: var(--text-main);
  font-size: 14px;
}

/* Token 进度柱状指示条 */
.token-progress-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.mini-bar-bg {
  width: 70px;
  height: 6px;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 3px;
  overflow: hidden;
}

.mini-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #89b4fa, #cba6f7);
  border-radius: 3px;
  transition: width 0.3s;
}

.ratio-text {
  font-size: 11px;
  color: var(--text-dim);
  font-family: var(--font-mono);
  min-width: 38px;
}

.key-count-tag {
  font-size: 11px;
  background: rgba(255, 255, 255, 0.06);
  padding: 3px 8px;
  border-radius: 4px;
  color: var(--text-muted);
}

.key-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-subtle);
  padding: 3px 8px;
  border-radius: 4px;
}

.key-text {
  font-size: 12px;
  color: #f9e2af;
}

.copy-mini-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 12px;
  padding: 0 2px;
  opacity: 0.7;
}

.copy-mini-btn:hover {
  opacity: 1;
}

.token-total-val {
  color: #89b4fa;
  font-size: 14px;
}

/* 分组 Badge */
.badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}

.badge-claude {
  background: rgba(235, 160, 172, 0.18);
  color: #eba0ac;
  border: 1px solid rgba(235, 160, 172, 0.3);
}

.badge-codex {
  background: rgba(166, 227, 161, 0.18);
  color: #a6e3a1;
  border: 1px solid rgba(166, 227, 161, 0.3);
}

.badge-all {
  background: rgba(137, 180, 250, 0.18);
  color: #89b4fa;
  border: 1px solid rgba(137, 180, 250, 0.3);
}

.empty-cell {
  text-align: center;
  padding: 40px !important;
}

.empty-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  color: var(--text-dim);
  font-size: 14px;
}

.empty-icon {
  font-size: 32px;
}

.btn-xs {
  padding: 3px 8px;
  font-size: 11px;
}

.btn-danger-outline {
  background: transparent;
  border: 1px solid rgba(243, 139, 168, 0.4);
  color: #f38ba8;
}

.btn-danger-outline:hover:not(:disabled) {
  background: rgba(243, 139, 168, 0.15);
}
</style>
