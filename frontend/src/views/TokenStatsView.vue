<template>
  <div class="token-stats-view">
    <!-- 顶部 KPI 指标卡片 (包含费用换算) -->
    <div class="kpi-grid">
      <div class="kpi-card highlight-card">
        <div class="kpi-icon-box">💎</div>
        <div class="kpi-content">
          <div class="kpi-label">累计消耗 Total Tokens</div>
          <div class="kpi-value font-mono">{{ formatNumber(summary.totalTokens) }}</div>
          <div class="kpi-subtext">全渠道累计 Token 吞吐</div>
        </div>
      </div>

      <div class="kpi-card cost-card">
        <div class="kpi-icon-box cost-icon">💰</div>
        <div class="kpi-content">
          <div class="kpi-label">预估总支出 (Total Cost)</div>
          <div class="kpi-value font-mono text-cost">¥{{ formatCurrency(summary.totalCostCny) }}</div>
          <div class="kpi-subtext font-mono">${{ formatCurrency(summary.totalCostUsd) }} USD (参考汇率 7.25)</div>
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
          <div class="kpi-subtext">今日预估 ¥{{ formatCurrency(summary.todayCostCny) }} (${{ formatCurrency(summary.todayCostUsd) }})</div>
        </div>
      </div>

      <div class="kpi-card cache-card">
        <div class="kpi-icon-box cache-icon">⚡</div>
        <div class="kpi-content">
          <div class="kpi-label">Prompt Cache 缓存节约</div>
          <div class="kpi-value font-mono text-warning">¥{{ formatCurrency(summary.totalSavedCostCny || 0) }}</div>
          <div class="kpi-subtext">命中率 {{ summary.cacheHitRate || 0 }}% (省 ${{ formatCurrency(summary.totalSavedCostUsd || 0) }})</div>
        </div>
      </div>

      <div class="kpi-card">
        <div class="kpi-icon-box req-icon">📈</div>
        <div class="kpi-content">
          <div class="kpi-label">统计记录请求数</div>
          <div class="kpi-value font-mono">{{ formatNumber(summary.totalRequests) }}</div>
          <div class="kpi-subtext">平均 {{ getAvgTokens() }} tokens/次</div>
        </div>
      </div>
    </div>

    <!-- 趋势图与模型分布看板 -->
    <div class="analytics-grid">
      <!-- 每日趋势图表 -->
      <div class="glass-card chart-card">
        <div class="chart-header">
          <div class="chart-title-box">
            <span class="chart-icon">📈</span>
            <span class="chart-title">Token 消耗与费用趋势</span>
          </div>
          <div class="chart-actions">
            <div class="range-selector">
              <button 
                v-for="d in [7, 14, 30]" 
                :key="d" 
                :class="['range-btn', { active: trendDays === d }]"
                @click="changeTrendDays(d)"
              >
                {{ d }}天
              </button>
            </div>
          </div>
        </div>

        <!-- 纯 CSS / SVG 响应式柱状图 -->
        <div class="chart-body">
          <div v-if="dailyStats.length === 0" class="chart-empty">暂无趋势数据</div>
          <div v-else class="bars-container">
            <div 
              v-for="(day, idx) in dailyStats" 
              :key="idx" 
              class="bar-col"
              @mouseenter="hoveredDay = day"
              @mouseleave="hoveredDay = null"
            >
              <div class="bar-track">
                <div 
                  class="bar-fill" 
                  :style="{ height: getBarHeight(day.totalTokens) }"
                  :class="{ 'has-data': day.totalTokens > 0 }"
                ></div>
              </div>
              <div class="bar-label font-mono">{{ formatDayLabel(day.date) }}</div>
            </div>
          </div>

          <!-- 悬浮数据卡片 Tooltip -->
          <div v-if="hoveredDay" class="chart-tooltip glass-card font-mono">
            <div class="tooltip-date">📅 {{ hoveredDay.date }}</div>
            <div class="tooltip-row">
              <span>总 Token:</span>
              <strong class="text-primary">{{ formatNumber(hoveredDay.totalTokens) }}</strong>
            </div>
            <div class="tooltip-row text-dim">
              <span>Prompt / Comp:</span>
              <span>{{ formatNumber(hoveredDay.promptTokens) }} / {{ formatNumber(hoveredDay.completionTokens) }}</span>
            </div>
            <div class="tooltip-row">
              <span>预估费用:</span>
              <strong class="text-cost">¥{{ formatCurrency(hoveredDay.costCny) }} (${{ formatCurrency(hoveredDay.costUsd) }})</strong>
            </div>
            <div class="tooltip-row text-dim">
              <span>请求次数:</span>
              <span>{{ hoveredDay.requestCount }} 次</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 模型消耗占比分布 -->
      <div class="glass-card models-card">
        <div class="card-header-simple">
          <div class="chart-title-box">
            <span class="chart-icon">🍩</span>
            <span class="chart-title">模型消耗分布 (Top Models)</span>
          </div>
        </div>

        <div class="models-list">
          <div v-if="modelStats.length === 0" class="chart-empty">暂无模型数据</div>
          <div v-else v-for="(m, idx) in modelStats.slice(0, 6)" :key="idx" class="model-item">
            <div class="model-meta">
              <span class="model-name font-mono truncate-text" :title="m.model">{{ m.model }}</span>
              <span class="model-tokens font-mono">{{ formatNumber(m.totalTokens) }} ({{ m.percentage }}%)</span>
            </div>
            <div class="model-bar-track">
              <div 
                class="model-bar-fill" 
                :style="{ width: `${m.percentage}%`, background: getModelGradient(idx) }"
              ></div>
            </div>
            <div class="model-cost font-mono text-dim">
              预估: ¥{{ formatCurrency(m.costCny) }} (${{ formatCurrency(m.costUsd) }}) · {{ m.requestCount }} 次请求
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 统计维度切换与工具栏 -->
    <div class="stats-panel card glass-card">
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
          <button class="btn btn-secondary btn-sm" @click="exportCsv" :disabled="summary.totalTokens === 0">
            <span>📥 导出 CSV 账单</span>
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
              <th>预估费用 (¥ / $)</th>
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
              <td colspan="10" class="empty-cell">
                <div class="empty-box">
                  <span class="empty-icon">📊</span>
                  <span>暂无 Token 消耗数据。发送请求后将实时自动统计！</span>
                </div>
              </td>
            </tr>
            <tr v-for="item in channelStats" :key="item.channelId" class="data-row">
              <td class="col-channel">
                <div class="channel-name-box">
                  <span class="channel-dot"></span>
                  <span class="channel-text font-bold">{{ item.channelName }}</span>
                </div>
              </td>
              <td>
                <span :class="['group-badge', getGroupBadgeClass(item.group)]">
                  {{ getGroupLabel(item.group) }}
                </span>
              </td>
              <td>
                <div class="token-progress-cell">
                  <div class="token-val font-mono">
                    <strong>{{ formatNumber(item.totalTokens) }}</strong>
                    <span class="token-pct text-dim">({{ getPercent(item.totalTokens, summary.totalTokens) }}%)</span>
                  </div>
                  <div class="progress-track">
                    <div class="progress-bar" :style="{ width: `${getPercent(item.totalTokens, summary.totalTokens)}%` }"></div>
                  </div>
                </div>
              </td>
              <td class="font-mono">
                <div class="cost-cell">
                  <span class="cost-cny">¥{{ formatCurrency(item.costCny) }}</span>
                  <span class="cost-usd text-dim">${{ formatCurrency(item.costUsd) }}</span>
                </div>
              </td>
              <td class="font-mono text-dim">{{ formatNumber(item.promptTokens) }}</td>
              <td class="font-mono text-dim">{{ formatNumber(item.completionTokens) }}</td>
              <td class="font-mono">{{ formatNumber(item.requestCount) }} 次</td>
              <td>
                <span class="badge badge-subtle font-mono">{{ item.keyCount }} 个 Key</span>
              </td>
              <td class="font-mono text-dim text-sm">{{ formatTime(item.lastUsed) }}</td>
              <td style="text-align: right;">
                <button class="btn btn-xs btn-primary-outline" @click="viewChannelKeys(item.channelId)">
                  查看 Key 明细 ➔
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 维度 2: 渠道 + Key 细分明细表格 -->
      <div v-if="activeTab === 'key'" class="table-container">
        <table class="data-table">
          <thead>
            <tr>
              <th>所属渠道</th>
              <th>API Key (脱敏)</th>
              <th>所属分组</th>
              <th>总 Token 消耗</th>
              <th>预估费用 (¥ / $)</th>
              <th>Prompt (输入)</th>
              <th>Completion (输出)</th>
              <th>调用次数</th>
              <th>最近活跃时间</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filteredKeyStats.length === 0">
              <td colspan="9" class="empty-cell">
                <div class="empty-box">
                  <span class="empty-icon">🔍</span>
                  <span>未找到匹配的 Key 消耗数据</span>
                </div>
              </td>
            </tr>
            <tr v-for="(item, idx) in filteredKeyStats" :key="idx" class="data-row">
              <td class="font-bold">{{ item.channelName }}</td>
              <td>
                <div class="key-badge font-mono" @click="copyKey(item.maskedKey)" title="点击复制脱敏 Key">
                  <span>🔑 {{ item.maskedKey }}</span>
                  <span class="copy-icon">📋</span>
                </div>
              </td>
              <td>
                <span :class="['group-badge', getGroupBadgeClass(item.group)]">
                  {{ getGroupLabel(item.group) }}
                </span>
              </td>
              <td>
                <div class="token-val font-mono">
                  <strong>{{ formatNumber(item.totalTokens) }}</strong>
                  <span class="token-pct text-dim">({{ getPercent(item.totalTokens, summary.totalTokens) }}%)</span>
                </div>
              </td>
              <td class="font-mono">
                <div class="cost-cell">
                  <span class="cost-cny">¥{{ formatCurrency(item.costCny) }}</span>
                  <span class="cost-usd text-dim">${{ formatCurrency(item.costUsd) }}</span>
                </div>
              </td>
              <td class="font-mono text-dim">{{ formatNumber(item.promptTokens) }}</td>
              <td class="font-mono text-dim">{{ formatNumber(item.completionTokens) }}</td>
              <td class="font-mono">{{ formatNumber(item.requestCount) }} 次</td>
              <td class="font-mono text-dim text-sm">{{ formatTime(item.lastUsed) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { api } from '../api';

const emit = defineEmits(['toast']);

const loading = ref(false);
const activeTab = ref('channel');
const selectedChannelFilter = ref('');
const searchKeyQuery = ref('');
const trendDays = ref(7);
const hoveredDay = ref(null);

const summary = ref({
  totalTokens: 0,
  promptTokens: 0,
  completionTokens: 0,
  totalCacheReadTokens: 0,
  totalCacheCreationTokens: 0,
  todayTokens: 0,
  totalRequests: 0,
  totalCostUsd: 0,
  totalCostCny: 0,
  todayCostUsd: 0,
  todayCostCny: 0,
  totalSavedCostUsd: 0,
  totalSavedCostCny: 0,
  cacheHitRate: 0
});

const channelStats = ref([]);
const keyStats = ref([]);
const dailyStats = ref([]);
const modelStats = ref([]);

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
    const [sumData, chData, keyData, dailyData, modData] = await Promise.all([
      api.getTokenSummary(),
      api.getChannelTokenStats(),
      api.getKeyTokenStats(),
      api.getDailyTokenStats(trendDays.value),
      api.getModelTokenStats()
    ]);
    summary.value = sumData || summary.value;
    channelStats.value = chData || [];
    keyStats.value = keyData || [];
    dailyStats.value = dailyData || [];
    modelStats.value = modData || [];
  } catch (err) {
    console.error('加载 Token 统计失败:', err);
  } finally {
    loading.value = false;
  }
}

async function changeTrendDays(days) {
  trendDays.value = days;
  try {
    dailyStats.value = await api.getDailyTokenStats(days) || [];
  } catch (err) {
    console.error('获取趋势数据失败:', err);
  }
}

function getBarHeight(tokens) {
  if (!tokens || tokens <= 0 || dailyStats.value.length === 0) return '4px';
  const maxTokens = Math.max(...dailyStats.value.map(d => d.totalTokens), 1);
  const pct = Math.max(6, Math.min(100, Math.round((tokens / maxTokens) * 100)));
  return `${pct}%`;
}

function formatDayLabel(dateStr) {
  if (!dateStr) return '';
  const parts = dateStr.split('-');
  if (parts.length === 3) {
    return `${parts[1]}/${parts[2]}`;
  }
  return dateStr;
}

const GRADIENTS = [
  'linear-gradient(90deg, #6366f1, #8b5cf6)',
  'linear-gradient(90deg, #3b82f6, #06b6d4)',
  'linear-gradient(90deg, #10b981, #34d399)',
  'linear-gradient(90deg, #f59e0b, #fbbf24)',
  'linear-gradient(90deg, #ec4899, #f43f5e)',
  'linear-gradient(90deg, #8b5cf6, #d946ef)'
];

function getModelGradient(idx) {
  return GRADIENTS[idx % GRADIENTS.length];
}

function viewChannelKeys(channelId) {
  selectedChannelFilter.value = channelId;
  activeTab.value = 'key';
}

function exportCsv() {
  const url = api.getExportCsvUrl();
  window.open(url, '_blank');
  emit('toast', '📥 正在下载 Token 消耗 CSV 账单文件...', 'info');
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

function formatCurrency(val) {
  if (val === null || val === undefined || isNaN(val)) return '0.00';
  return Number(val).toFixed(2);
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
});
</script>

<style scoped>
.token-stats-view {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

/* 顶部 KPI 卡片网格 */
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px;
}

.kpi-card {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 16px;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-sm);
  transition: transform 0.2s ease, border-color 0.2s ease;
}

.kpi-card:hover {
  transform: translateY(-2px);
  border-color: var(--primary);
}

.highlight-card {
  border-color: rgba(99, 102, 241, 0.4);
  background: linear-gradient(135deg, var(--bg-surface) 0%, rgba(99, 102, 241, 0.08) 100%);
}

.cost-card {
  border-color: rgba(245, 158, 11, 0.4);
  background: linear-gradient(135deg, var(--bg-surface) 0%, rgba(245, 158, 11, 0.08) 100%);
}

.kpi-icon-box {
  width: 44px;
  height: 44px;
  border-radius: var(--radius-sm);
  background: rgba(99, 102, 241, 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  flex-shrink: 0;
}

.cost-icon { background: rgba(245, 158, 11, 0.15); }
.input-icon { background: rgba(59, 130, 246, 0.15); }
.output-icon { background: rgba(16, 185, 129, 0.15); }
.today-icon { background: rgba(236, 72, 153, 0.15); }
.req-icon { background: rgba(139, 92, 246, 0.15); }

.kpi-content {
  overflow: hidden;
}

.kpi-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
}

.kpi-value {
  font-size: 20px;
  font-weight: 700;
  color: var(--text-main);
  margin: 2px 0;
}

.text-cost {
  color: #f59e0b;
}

.kpi-subtext {
  font-size: 11px;
  color: var(--text-dim);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 趋势图与模型分布 */
.analytics-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 14px;
}

@media (max-width: 900px) {
  .analytics-grid {
    grid-template-columns: 1fr;
  }
}

.chart-card, .models-card {
  padding: 16px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
  background: var(--bg-surface);
}

.chart-header, .card-header-simple {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.chart-title-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.chart-icon {
  font-size: 16px;
}

.chart-title {
  font-size: 14px;
  font-weight: 700;
  color: var(--text-main);
}

.range-selector {
  display: flex;
  background: var(--bg-surface-2);
  border-radius: var(--radius-sm);
  padding: 2px;
  gap: 2px;
}

.range-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 11px;
  padding: 4px 8px;
  border-radius: var(--radius-sm);
  cursor: pointer;
}

.range-btn.active {
  background: var(--primary);
  color: #fff;
  font-weight: 600;
}

.chart-body {
  position: relative;
  height: 180px;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
}

.bars-container {
  display: flex;
  align-items: flex-end;
  height: 150px;
  gap: 8px;
  padding-bottom: 6px;
}

.bar-col {
  flex: 1;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-end;
  cursor: pointer;
}

.bar-track {
  width: 100%;
  max-width: 28px;
  height: 100%;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 4px 4px 0 0;
  display: flex;
  align-items: flex-end;
}

.bar-fill {
  width: 100%;
  background: rgba(99, 102, 241, 0.3);
  border-radius: 4px 4px 0 0;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.bar-col:hover .bar-fill {
  background: linear-gradient(180deg, #818cf8 0%, #6366f1 100%);
  box-shadow: 0 0 10px rgba(99, 102, 241, 0.5);
}

.bar-fill.has-data {
  background: linear-gradient(180deg, #6366f1 0%, #4f46e5 100%);
}

.bar-label {
  font-size: 10px;
  color: var(--text-dim);
  margin-top: 6px;
}

.chart-tooltip {
  position: absolute;
  top: 0;
  right: 0;
  background: rgba(15, 23, 42, 0.9);
  border: 1px solid var(--primary);
  border-radius: var(--radius-sm);
  padding: 8px 12px;
  font-size: 11px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  pointer-events: none;
  z-index: 10;
  box-shadow: var(--shadow-md);
}

.tooltip-date {
  font-weight: 700;
  color: #fff;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  padding-bottom: 4px;
  margin-bottom: 2px;
}

.tooltip-row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.chart-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--text-dim);
  font-size: 12px;
}

/* 模型列表 */
.models-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.model-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.model-meta {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
}

.model-name {
  color: var(--text-main);
  font-weight: 600;
  max-width: 140px;
}

.model-tokens {
  color: var(--text-muted);
}

.model-bar-track {
  width: 100%;
  height: 6px;
  background: var(--bg-surface-2);
  border-radius: 3px;
  overflow: hidden;
}

.model-bar-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.4s ease;
}

.model-cost {
  font-size: 10px;
}

/* 面板与表格 */
.stats-panel {
  padding: 16px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
  background: var(--bg-surface);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 16px;
}

.tabs-box {
  display: flex;
  background: var(--bg-surface-2);
  border-radius: var(--radius-sm);
  padding: 2px;
  gap: 2px;
}

.dimension-tab {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 13px;
  padding: 6px 14px;
  border-radius: var(--radius-sm);
  cursor: pointer;
  transition: all 0.2s ease;
}

.dimension-tab.active {
  background: var(--primary);
  color: #fff;
  font-weight: 600;
}

.panel-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
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

.filter-select, .search-input {
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
  padding: 6px 10px;
  font-size: 12px;
  outline: none;
}

.search-input {
  width: 160px;
}

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
  padding: 10px 12px;
  background: rgba(0, 0, 0, 0.15);
  color: var(--text-muted);
  font-weight: 600;
  border-bottom: 1px solid var(--border-subtle);
  white-space: nowrap;
}

.data-table td {
  padding: 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  vertical-align: middle;
}

.data-row:hover {
  background: rgba(255, 255, 255, 0.02);
}

.channel-name-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.channel-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--primary);
}

.group-badge {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 3px;
}

.badge-claude { background: rgba(99, 102, 241, 0.15); color: #818cf8; }
.badge-codex { background: rgba(245, 158, 11, 0.15); color: #fbbf24; }
.badge-all { background: rgba(148, 163, 184, 0.15); color: #94a3b8; }

.token-progress-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 140px;
}

.progress-track {
  width: 100%;
  height: 4px;
  background: var(--bg-surface-2);
  border-radius: 2px;
  overflow: hidden;
}

.progress-bar {
  height: 100%;
  background: var(--primary);
  border-radius: 2px;
}

.cost-cell {
  display: flex;
  flex-direction: column;
}

.cost-cny {
  color: #f59e0b;
  font-weight: 600;
}

.cost-usd {
  font-size: 11px;
}

.key-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: var(--bg-surface-2);
  padding: 4px 8px;
  border-radius: var(--radius-sm);
  cursor: pointer;
}

.key-badge:hover {
  background: rgba(99, 102, 241, 0.15);
}

.empty-cell {
  padding: 40px;
  text-align: center;
}

.empty-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  color: var(--text-dim);
}

.empty-icon {
  font-size: 24px;
}

.btn-danger-outline {
  background: transparent;
  border: 1px solid rgba(239, 68, 68, 0.4);
  color: #f87171;
}

.btn-danger-outline:hover:not(:disabled) {
  background: rgba(239, 68, 68, 0.15);
  border-color: #f87171;
}

.btn-primary-outline {
  background: transparent;
  border: 1px solid var(--primary);
  color: var(--primary);
}

.btn-primary-outline:hover {
  background: var(--primary);
  color: #fff;
}
</style>
