<template>
  <div class="logs-view">
    <!-- 日志工具栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <h2 class="section-title">实时请求与故障转移追踪</h2>
        <p class="section-subtitle">监控来自 VSCode / 客户端的每次请求链路，支持多维度过滤、关键字搜索与大容量分页查看。</p>
      </div>

      <div class="toolbar-actions">
        <!-- 状态过滤器 -->
        <div class="filter-group">
          <button 
            v-for="f in filters" 
            :key="f.id"
            :class="['filter-btn', { active: currentFilter === f.id }]"
            @click="onFilterChange(f.id)"
          >
            {{ f.label }}
          </button>
        </div>

        <!-- 关键词搜索框 -->
        <div class="search-box">
          <span class="search-icon">🔍</span>
          <input 
            v-model="searchKeyword" 
            @input="onSearchInput" 
            class="search-input font-mono" 
            placeholder="搜索路径/模型/渠道/错误..."
          />
          <button v-if="searchKeyword" class="search-clear" @click="clearSearch">✕</button>
        </div>

        <button :class="['btn', autoRefresh ? 'btn-primary' : 'btn-secondary', 'btn-sm']" @click="toggleAutoRefresh">
          <span :class="['status-dot', autoRefresh ? 'active' : 'inactive']"></span>
          <span>{{ autoRefresh ? '自动刷新 (2s)' : '已暂停刷新' }}</span>
        </button>

        <button class="btn btn-secondary btn-sm" @click="handleManualRefresh">🔄 刷新</button>
        <button class="btn btn-secondary btn-sm" @click="openSettingsModal">⚙️ 保留策略</button>
        <button class="btn btn-danger btn-sm" @click="clearLogs">🗑️ 清空日志</button>
      </div>
    </div>

    <!-- 日志保留策略配置弹窗 Modal -->
    <div v-if="showSettingsModal" class="modal-overlay" @click.self="showSettingsModal = false">
      <div class="glass-card modal-container">
        <div class="modal-header">
          <h3 class="modal-title">⚙️ 请求日志持久化与清理策略</h3>
          <button class="close-btn" @click="showSettingsModal = false">✕</button>
        </div>

        <div class="modal-body">
          <div class="persistence-tip">
            <span class="tip-icon">💾</span>
            <div class="tip-text">
              <strong>便携持久化存储</strong>：所有请求日志均已实时落盘至 <code>data/request_logs.json</code>，网关服务重启或电脑重启后数据依然完整保留！
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
              <option :value="20000">最多保留 20,000 条</option>
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

    <!-- 日志列表展示 -->
    <div v-if="loading && pagedLogs.length === 0" class="glass-card loading-card">
      <div class="loading-spinner"></div>
      <div class="loading-text">正在加载请求日志...</div>
    </div>

    <div v-else-if="pagedLogs.length === 0" class="glass-card empty-card">
      <div class="empty-text">
        <span v-if="searchKeyword">未找到匹配 "{{ searchKeyword }}" 的请求记录</span>
        <span v-else>当前筛选条件下暂无请求日志</span>
      </div>
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
          <template v-for="log in pagedLogs" :key="log.id">
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
                  v-if="log.errorDetails || (log.requestHeaders && Object.keys(log.requestHeaders).length > 0)" 
                  class="btn btn-secondary btn-sm" 
                  @click="toggleExpand(log.id)"
                >
                  {{ expandedRows[log.id] ? '收起详情' : '详情 / 头' }}
                </button>
                <span v-else class="text-dim">-</span>
              </td>
            </tr>

            <!-- 展开的详情折叠行 -->
            <tr v-if="expandedRows[log.id]" class="detail-row">
              <td colspan="8">
                <div class="detail-content">
                  <!-- 错误信息详情 -->
                  <div v-if="log.errorDetails" class="error-detail-box">
                    <div class="error-title">🚨 错误/重试详细追踪:</div>
                    <pre class="error-text font-mono">{{ log.errorDetails }}</pre>
                  </div>

                  <!-- 嗅探到的客户端请求头 -->
                  <div v-if="log.requestHeaders && Object.keys(log.requestHeaders).length > 0" class="headers-sniff-box">
                    <div class="headers-header">
                      <span class="headers-title">🕹️ 客户端请求头 (嗅探提取结果):</span>
                      <button class="btn btn-primary btn-xs" @click="copyExtractedHeaders(log.requestHeaders)">
                        📋 提取并复制为渠道请求头配置
                      </button>
                    </div>
                    <div class="headers-grid font-mono">
                      <div v-for="(val, key) in log.requestHeaders" :key="key" class="header-item">
                        <span class="header-key">{{ key }}:</span>
                        <span class="header-val" :title="val">{{ val }}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <!-- 底部现代化分页器 -->
    <div class="pagination-bar glass-card">
      <div class="pagination-left">
        <span class="pagination-info">
          共 <strong class="text-primary">{{ totalCount }}</strong> 条记录，
          第 <strong>{{ currentPage }}</strong> / <strong>{{ totalPages }}</strong> 页
        </span>
        <div class="page-size-selector">
          <span class="size-label">每页:</span>
          <select :value="pageSize" @change="onPageSizeChange(Number($event.target.value))" class="size-select">
            <option :value="20">20 条</option>
            <option :value="50">50 条</option>
            <option :value="100">100 条</option>
            <option :value="200">200 条</option>
          </select>
        </div>
      </div>

      <div class="pagination-controls">
        <button 
          class="page-btn page-nav-btn" 
          :disabled="currentPage <= 1" 
          @click="goToPage(1)"
          title="首页"
        >
          «
        </button>
        <button 
          class="page-btn page-nav-btn" 
          :disabled="currentPage <= 1" 
          @click="goToPage(currentPage - 1)"
          title="上一页"
        >
          ‹
        </button>

        <template v-for="(p, idx) in visiblePages" :key="idx">
          <span v-if="p === '...'" class="page-ellipsis">...</span>
          <button 
            v-else 
            :class="['page-btn', { active: p === currentPage }]" 
            @click="goToPage(p)"
          >
            {{ p }}
          </button>
        </template>

        <button 
          class="page-btn page-nav-btn" 
          :disabled="currentPage >= totalPages" 
          @click="goToPage(currentPage + 1)"
          title="下一页"
        >
          ›
        </button>
        <button 
          class="page-btn page-nav-btn" 
          :disabled="currentPage >= totalPages" 
          @click="goToPage(totalPages)"
          title="末页"
        >
          »
        </button>

        <!-- 快速跳转 -->
        <div class="page-jump">
          <span class="jump-label">跳至</span>
          <input 
            v-model="jumpPageInput" 
            @keyup.enter="handleJumpPage" 
            type="number" 
            min="1" 
            :max="totalPages" 
            class="jump-input font-mono" 
            placeholder="页码"
          />
          <button class="btn btn-secondary btn-xs jump-btn" @click="handleJumpPage">Go</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, reactive } from 'vue';
import { api } from '../api';

const props = defineProps({
  logs: {
    type: Array,
    default: () => []
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

const searchKeyword = ref('');
const currentPage = ref(1);
const pageSize = ref(Number(localStorage.getItem('mfg_logs_page_size') || 50));
const totalCount = ref(0);
const totalPages = ref(1);
const pagedLogs = ref([]);
const loading = ref(false);
const jumpPageInput = ref('');

const expandedRows = reactive({});

function toggleExpand(id) {
  expandedRows[id] = !expandedRows[id];
}

const IGNORED_COPY_HEADERS = [
  'host', 'connection', 'keep-alive', 'transfer-encoding', 'upgrade', 'proxy-connection', 
  'content-length', 'content-type', 'authorization', 'x-api-key'
];

async function copyExtractedHeaders(headers) {
  if (!headers || Object.keys(headers).length === 0) return;
  
  const lines = Object.entries(headers)
    .filter(([k]) => !IGNORED_COPY_HEADERS.includes(k.toLowerCase()))
    .map(([k, v]) => {
      if (k.toLowerCase() === 'x-request-id' || k.toLowerCase() === 'x-client-request-id') {
        return `${k}: {uuid}`;
      }
      return `${k}: ${v}`;
    });
  
  const text = lines.join('\n');
  try {
    await navigator.clipboard.writeText(text);
    emit('toast', `🎉 已提取并复制 ${lines.length} 条特征请求头（已自动排除脱敏Key与传输头），可直接粘贴至渠道配置！`, 'success');
  } catch (err) {
    emit('toast', '复制失败，请手动选择复制', 'error');
  }
}

function formatTime(isoString) {
  if (!isoString) return '';
  const date = new Date(isoString);
  return date.toLocaleTimeString('zh-CN', { hour12: false });
}

function getTrailTitle(log) {
  if (!log.triedChannels || log.triedChannels.length === 0) return '';
  return log.triedChannels.join(' -> ');
}

// 分页与数据请求逻辑
async function fetchPagedLogs(showLoading = false) {
  if (showLoading) loading.value = true;
  try {
    const res = await api.getPagedLogs(currentPage.value, pageSize.value, currentFilter.value, searchKeyword.value);
    if (res) {
      pagedLogs.value = res.items || [];
      totalCount.value = res.totalCount || 0;
      totalPages.value = Math.max(1, res.totalPages || 1);
      currentPage.value = res.page || 1;
    }
  } catch (err) {
    console.error('获取分页日志失败:', err);
  } finally {
    if (showLoading) loading.value = false;
  }
}

function onFilterChange(filterId) {
  currentFilter.value = filterId;
  currentPage.value = 1;
  fetchPagedLogs(true);
}

let searchDebounce = null;
function onSearchInput() {
  clearTimeout(searchDebounce);
  searchDebounce = setTimeout(() => {
    currentPage.value = 1;
    fetchPagedLogs();
  }, 300);
}

function clearSearch() {
  searchKeyword.value = '';
  currentPage.value = 1;
  fetchPagedLogs(true);
}

function onPageSizeChange(newSize) {
  pageSize.value = newSize;
  localStorage.setItem('mfg_logs_page_size', String(newSize));
  currentPage.value = 1;
  fetchPagedLogs(true);
}

function goToPage(page) {
  if (page < 1 || page > totalPages.value || page === currentPage.value) return;
  currentPage.value = page;
  fetchPagedLogs(true);
}

function handleJumpPage() {
  const p = parseInt(jumpPageInput.value, 10);
  if (!isNaN(p) && p >= 1 && p <= totalPages.value) {
    goToPage(p);
    jumpPageInput.value = '';
  }
}

function handleManualRefresh() {
  fetchPagedLogs(true);
  emit('refresh');
}

// 可视化页码序列生成（智能省略）
const visiblePages = computed(() => {
  const total = totalPages.value;
  const current = currentPage.value;
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1);
  }
  const pages = [];
  if (current <= 4) {
    for (let i = 1; i <= 5; i++) pages.push(i);
    pages.push('...');
    pages.push(total);
  } else if (current >= total - 3) {
    pages.push(1);
    pages.push('...');
    for (let i = total - 4; i <= total; i++) pages.push(i);
  } else {
    pages.push(1);
    pages.push('...');
    pages.push(current - 1);
    pages.push(current);
    pages.push(current + 1);
    pages.push('...');
    pages.push(total);
  }
  return pages;
});

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
    fetchPagedLogs(false);
  }, 2000);
}

function stopTimer() {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
}

// 保留策略配置弹窗
const showSettingsModal = ref(false);
const savingSettings = ref(false);
const logSettings = reactive({
  retentionDays: 7,
  maxCapacity: 2000,
  autoCleanupEnabled: true
});

async function openSettingsModal() {
  try {
    const data = await api.getLogSettings();
    if (data) {
      Object.assign(logSettings, data);
    }
  } catch (err) {
    console.error('获取日志设置失败:', err);
  }
  showSettingsModal.value = true;
}

async function saveSettings() {
  savingSettings.value = true;
  try {
    await api.saveLogSettings(logSettings);
    emit('toast', '💾 日志清理策略已保存并即刻生效', 'success');
    showSettingsModal.value = false;
    fetchPagedLogs(true);
  } catch (err) {
    emit('toast', `保存失败: ${err.message}`, 'error');
  } finally {
    savingSettings.value = false;
  }
}

async function clearLogs() {
  if (!confirm('确定要彻底清空所有历史请求日志吗？此操作无法撤销。')) return;
  try {
    await api.clearLogs();
    pagedLogs.value = [];
    totalCount.value = 0;
    totalPages.value = 1;
    currentPage.value = 1;
    emit('toast', '🗑️ 所有日志已彻底清空', 'success');
    emit('refresh');
  } catch (err) {
    emit('toast', `清空失败: ${err.message}`, 'error');
  }
}

onMounted(() => {
  fetchPagedLogs(true);
  if (autoRefresh.value) {
    startTimer();
  }
});

onUnmounted(() => {
  stopTimer();
  clearTimeout(searchDebounce);
});
</script>

<style scoped>
.logs-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.toolbar-left {
  flex: 1;
  min-width: 260px;
}

.section-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--text-main);
  margin: 0;
}

.section-subtitle {
  font-size: 13px;
  color: var(--text-muted);
  margin-top: 4px;
}

.toolbar-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.filter-group {
  display: flex;
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  padding: 2px;
  gap: 2px;
}

.filter-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  padding: 6px 12px;
  font-size: 12px;
  border-radius: var(--radius-sm);
  cursor: pointer;
  transition: all 0.2s ease;
}

.filter-btn:hover {
  color: var(--text-main);
}

.filter-btn.active {
  background: var(--primary);
  color: #fff;
  font-weight: 600;
}

/* 搜索框 */
.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 10px;
  font-size: 13px;
  pointer-events: none;
  opacity: 0.6;
}

.search-input {
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
  padding: 6px 28px 6px 30px;
  font-size: 12px;
  width: 220px;
  transition: all 0.2s ease;
}

.search-input:focus {
  width: 260px;
  border-color: var(--primary);
  outline: none;
  background: var(--bg-surface);
}

.search-clear {
  position: absolute;
  right: 8px;
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 12px;
  padding: 2px 4px;
}

.search-clear:hover {
  color: var(--text-main);
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  margin-right: 4px;
}

.status-dot.active {
  background: #10b981;
  box-shadow: 0 0 8px rgba(16, 185, 129, 0.6);
}

.status-dot.inactive {
  background: #94a3b8;
}

/* 表格容器 */
.logs-table-container {
  overflow-x: auto;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
}

.logs-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  text-align: left;
}

.logs-table th {
  padding: 12px 14px;
  background: rgba(0, 0, 0, 0.2);
  color: var(--text-muted);
  font-weight: 600;
  border-bottom: 1px solid var(--border-subtle);
  white-space: nowrap;
}

.logs-table td {
  padding: 12px 14px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  vertical-align: middle;
}

.log-row:hover {
  background: rgba(255, 255, 255, 0.02);
}

.row-failover {
  background: rgba(245, 158, 11, 0.04);
}

.row-error {
  background: rgba(239, 68, 68, 0.04);
}

.truncate-text {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
}

.col-path {
  max-width: 220px;
}

.col-model {
  max-width: 160px;
}

.col-trail {
  max-width: 260px;
}

.col-final {
  max-width: 160px;
}

.endpoint-badge {
  display: flex;
  align-items: center;
  gap: 6px;
}

.method-tag {
  font-size: 10px;
  font-weight: 700;
  padding: 2px 5px;
  border-radius: 3px;
  background: rgba(99, 102, 241, 0.15);
  color: var(--primary);
  flex-shrink: 0;
}

.path-text {
  font-size: 12px;
  color: var(--text-main);
}

.failover-trail {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}

.trail-node {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
}

.trail-fail {
  background: rgba(239, 68, 68, 0.15);
  color: #f87171;
  text-decoration: line-through;
}

.trail-success {
  background: rgba(16, 185, 129, 0.15);
  color: #34d399;
  font-weight: 600;
}

.trail-arrow {
  color: var(--text-muted);
  font-size: 10px;
}

.trail-badge {
  margin-left: 4px;
}

.final-channel-tag {
  padding: 3px 8px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  font-weight: 500;
}

.final-channel-tag.channel-active {
  background: rgba(99, 102, 241, 0.1);
  color: #a5b4fc;
  border: 1px solid rgba(99, 102, 241, 0.2);
}

.final-channel-tag.channel-none {
  background: rgba(239, 68, 68, 0.1);
  color: #f87171;
}

/* 详情折叠面板 */
.detail-row td {
  padding: 0 14px 14px 14px;
  background: rgba(0, 0, 0, 0.15);
}

.detail-content {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  margin-top: 6px;
}

.error-detail-box {
  background: rgba(239, 68, 68, 0.08);
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: var(--radius-sm);
  padding: 8px 12px;
}

.error-title {
  color: #f87171;
  font-weight: 600;
  font-size: 12px;
  margin-bottom: 4px;
}

.error-text {
  color: #fca5a5;
  font-size: 11px;
  white-space: pre-wrap;
  word-break: break-all;
  margin: 0;
}

.headers-sniff-box {
  background: rgba(99, 102, 241, 0.05);
  border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: var(--radius-sm);
  padding: 8px 12px;
}

.headers-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.headers-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--primary);
}

.headers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 6px 12px;
  font-size: 11px;
}

.header-item {
  display: flex;
  align-items: baseline;
  gap: 6px;
  overflow: hidden;
}

.header-key {
  color: #818cf8;
  flex-shrink: 0;
}

.header-val {
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 分页器组件样式 */
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  padding: 10px 16px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
  gap: 12px;
}

.pagination-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.pagination-info {
  font-size: 13px;
  color: var(--text-muted);
}

.page-size-selector {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
}

.size-select {
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
  padding: 4px 8px;
  font-size: 12px;
  cursor: pointer;
  outline: none;
}

.pagination-controls {
  display: flex;
  align-items: center;
  gap: 4px;
}

.page-btn {
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  color: var(--text-main);
  min-width: 32px;
  height: 30px;
  padding: 0 6px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.page-btn:hover:not(:disabled) {
  border-color: var(--primary);
  color: var(--primary);
}

.page-btn.active {
  background: var(--primary);
  border-color: var(--primary);
  color: #fff;
  font-weight: 700;
}

.page-btn:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.page-nav-btn {
  font-size: 14px;
}

.page-ellipsis {
  color: var(--text-muted);
  padding: 0 4px;
  font-size: 12px;
}

.page-jump {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-left: 8px;
  font-size: 12px;
  color: var(--text-muted);
}

.jump-input {
  width: 50px;
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
  padding: 4px 6px;
  font-size: 12px;
  text-align: center;
  outline: none;
}

.jump-input:focus {
  border-color: var(--primary);
}

.loading-card, .empty-card {
  padding: 40px;
  text-align: center;
  color: var(--text-muted);
}

.loading-spinner {
  width: 28px;
  height: 28px;
  border: 3px solid rgba(99, 102, 241, 0.2);
  border-top-color: var(--primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 12px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* 弹窗样式 */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(0, 0, 0, 0.65);
  backdrop-filter: blur(6px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  padding: 20px;
}

.modal-container {
  width: 100%;
  max-width: 520px;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 24px;
  box-shadow: var(--shadow-lg);
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;
}

.modal-title {
  font-size: 17px;
  font-weight: 700;
  margin: 0;
}

.close-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 18px;
  cursor: pointer;
}

.persistence-tip {
  display: flex;
  gap: 10px;
  background: rgba(99, 102, 241, 0.08);
  border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: var(--radius-sm);
  padding: 10px 14px;
  font-size: 12px;
  margin-bottom: 14px;
}

.switch-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 14px;
}

.form-label {
  font-size: 13px;
  font-weight: 600;
}

.form-hint {
  font-size: 11px;
  color: var(--text-muted);
  margin: 0;
}

.form-select {
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
  padding: 8px 12px;
  font-size: 13px;
  outline: none;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 18px;
}
</style>
