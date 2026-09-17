<template>
  <div class="logs-page">
    <!-- 1. 顶部数据指标看板卡片 -->
    <div class="metrics-grid">
      <div class="metric-card glass-card">
        <div class="metric-header">
          <span class="metric-label">总请求调用</span>
          <span class="metric-icon">📊</span>
        </div>
        <div class="metric-value font-mono">{{ totalCount }}</div>
        <div class="metric-footer text-dim">历史总调度链路记录</div>
      </div>

      <div class="metric-card glass-card">
        <div class="metric-header">
          <span class="metric-label">正在进行中 (PENDING)</span>
          <span class="pulse-indicator pulse-blue"></span>
        </div>
        <div class="metric-value text-info font-mono">{{ pendingCount }}</div>
        <div class="metric-footer text-dim">长推理/流式传输中任务</div>
      </div>

      <div class="metric-card glass-card">
        <div class="metric-header">
          <span class="metric-label">成功请求</span>
          <span class="metric-icon">✅</span>
        </div>
        <div class="metric-value text-success font-mono">{{ successCount }}</div>
        <div class="metric-footer text-dim">成功率 {{ calcRate }}%</div>
      </div>

      <div class="metric-card glass-card">
        <div class="metric-header">
          <span class="metric-label">失败 / 救急切换</span>
          <span class="metric-icon">🚨</span>
        </div>
        <div class="metric-value text-danger font-mono">{{ failoverCount }}</div>
        <div class="metric-footer text-dim">已自动完成备用重试</div>
      </div>
    </div>

    <!-- 2. 工具操作栏 (过滤胶囊 + 搜索框 + 自动刷新) -->
    <div class="action-toolbar glass-card">
      <!-- 状态过滤胶囊 Tab -->
      <div class="status-tabs">
        <button 
          v-for="tab in filterTabs" 
          :key="tab.id"
          :class="['status-tab-btn', { active: currentFilter === tab.id }]"
          @click="onFilterChange(tab.id)"
        >
          <span>{{ tab.label }}</span>
          <span v-if="tab.count !== undefined" class="tab-count-pill">{{ tab.count }}</span>
        </button>
      </div>

      <!-- 搜索与操作按钮 -->
      <div class="toolbar-right-tools">
        <div class="search-input-wrapper">
          <span class="search-ico">🔍</span>
          <input 
            v-model="searchKeyword" 
            @input="onSearchInput" 
            class="search-field font-mono" 
            placeholder="搜索路径/模型/渠道/请求体/报错..."
          />
          <button v-if="searchKeyword" class="search-clear-btn" @click="clearSearch">✕</button>
        </div>

        <button 
          :class="['btn', autoRefresh ? 'btn-primary' : 'btn-secondary', 'btn-sm']" 
          @click="toggleAutoRefresh"
          :title="autoRefresh ? '暂停每 2 秒自动同步' : '开启每 2 秒自动同步'"
        >
          <span :class="['pulse-indicator', autoRefresh ? 'pulse-green' : '']" style="width: 6px; height: 6px;"></span>
          <span>{{ autoRefresh ? '自动刷新 (2s)' : '已暂停' }}</span>
        </button>

        <button class="btn btn-secondary btn-sm" @click="fetchLogs" :disabled="loading">
          <span>🔄 刷新</span>
        </button>

        <button class="btn btn-secondary btn-sm" @click="showSettingsModal = true">
          <span>⚙️ 策略</span>
        </button>

        <button class="btn btn-danger btn-sm" @click="clearLogs">
          <span>🗑️ 清空</span>
        </button>
      </div>
    </div>

    <!-- 3. 日志列表表格区 -->
    <div v-if="loading && pagedLogs.length === 0" class="glass-card state-box">
      <div class="loading-ring"></div>
      <div class="state-title">正在实时同步请求链路日志...</div>
    </div>

    <div v-else-if="pagedLogs.length === 0" class="glass-card state-box">
      <span class="state-icon">📋</span>
      <div class="state-title">
        <span v-if="searchKeyword">未检索到包含 "{{ searchKeyword }}" 的相关记录</span>
        <span v-else>当前筛选分类下暂无请求记录</span>
      </div>
      <p class="state-desc">在客户端或沙箱中发起模型请求后，此处将实时展示完整的调度轨迹与请求载荷。</p>
    </div>

    <div v-else class="logs-container glass-card">
      <div class="table-responsive">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width: 100px;">请求状态</th>
              <th style="width: 110px;">发起时间</th>
              <th>端点路由</th>
              <th>请求模型</th>
              <th>故障转移调度链路</th>
              <th>最终渠道</th>
              <th style="width: 90px;">耗时 / Tokens</th>
              <th style="width: 100px; text-align: right;">载荷与诊断</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="log in pagedLogs" :key="log.id">
              <tr 
                :class="['data-row', { 
                  'is-expanded': expandedRows[log.id], 
                  'row-failover': log.isFailover, 
                  'row-error': log.status === 'FAILED' || log.statusCode >= 400,
                  'row-pending': log.status === 'PENDING'
                }]"
                @click="toggleExpand(log.id)"
              >
                <!-- 状态徽章 -->
                <td>
                  <span v-if="log.status === 'PENDING'" class="badge badge-pending">
                    <span class="pulse-indicator pulse-blue"></span> 运行中
                  </span>
                  <span v-else-if="log.status === 'SUCCESS' || (log.statusCode >= 200 && log.statusCode < 400 && log.status !== 'FAILED')" class="badge badge-success">
                    {{ log.statusCode }} 成功
                  </span>
                  <span v-else class="badge badge-danger">
                    {{ log.statusCode > 0 ? log.statusCode : 'ERR' }} 失败
                  </span>
                </td>

                <!-- 时间戳 -->
                <td class="font-mono text-dim" style="font-size: 12px;">
                  {{ formatTime(log.timestamp) }}
                </td>

                <!-- 端点路径 -->
                <td>
                  <div class="path-badge" :title="log.requestPath">
                    <span class="method-tag">{{ log.requestMethod }}</span>
                    <span class="path-val font-mono truncate-text">{{ log.requestPath }}</span>
                  </div>
                </td>

                <!-- 模型 -->
                <td>
                  <span class="model-pill font-mono truncate-text" :title="log.model || '未指定'">
                    {{ log.model || 'AI Model' }}
                  </span>
                </td>

                <!-- 故障转移链路 -->
                <td>
                  <div v-if="log.triedChannels && log.triedChannels.length > 1" class="trail-chain">
                    <template v-for="(ch, idx) in log.triedChannels" :key="idx">
                      <span :class="['trail-chip', idx === log.triedChannels.length - 1 ? 'chip-success' : 'chip-fail']" :title="ch">
                        {{ ch }}
                      </span>
                      <span v-if="idx < log.triedChannels.length - 1" class="trail-sep">➔</span>
                    </template>
                    <span class="badge badge-warning trail-mark">救急切换</span>
                  </div>
                  <div v-else-if="log.triedChannels && log.triedChannels.length === 1" class="trail-single">
                    <span class="text-muted font-mono" style="font-size: 12px;" :title="log.triedChannels[0]">{{ log.triedChannels[0] }}</span>
                  </div>
                  <span v-else class="text-dim">-</span>
                </td>

                <!-- 最终渠道 -->
                <td>
                  <span v-if="log.finalChannel" class="channel-pill font-mono truncate-text" :title="log.finalChannel">
                    {{ log.finalChannel }}
                  </span>
                  <span v-else class="text-dim" style="font-size: 12px;">{{ log.status === 'PENDING' ? '调度中...' : '无可用响应' }}</span>
                </td>

                <!-- 耗时与 Token -->
                <td>
                  <div class="timing-box font-mono">
                    <span class="timing-ms">{{ log.durationMs }}ms</span>
                    <span v-if="log.promptTokens || log.completionTokens" class="tokens-mini" :title="'Prompt: ' + (log.promptTokens || 0) + ' | Completion: ' + (log.completionTokens || 0)">
                      {{ (log.promptTokens || 0) + (log.completionTokens || 0) }} tok
                    </span>
                  </div>
                </td>

                <!-- 展开按钮 -->
                <td style="text-align: right;">
                  <button class="btn btn-secondary btn-xs" @click.stop="toggleExpand(log.id)">
                    <span>{{ expandedRows[log.id] ? '收起' : '排查 / 载荷' }}</span>
                    <span class="expand-caret">{{ expandedRows[log.id] ? '▲' : '▼' }}</span>
                  </button>
                </td>
              </tr>

              <!-- 展开的专业级检查抽屉 (Inspector Panel) -->
              <tr v-if="expandedRows[log.id]" class="inspector-row">
                <td colspan="8">
                  <div class="inspector-panel glass-card">
                    <!-- 抽屉顶部 Tab 切换 -->
                    <div class="inspector-tabs">
                      <button 
                        :class="['inspector-tab', { active: getActiveSubTab(log.id) === 'request' }]"
                        @click="setActiveSubTab(log.id, 'request')"
                      >
                        <span>📋 请求载荷 (Request Body)</span>
                        <span v-if="log.requestBody" class="tab-dot"></span>
                      </button>

                      <button 
                        :class="['inspector-tab', { active: getActiveSubTab(log.id) === 'response' }]"
                        @click="setActiveSubTab(log.id, 'response')"
                      >
                        <span>💬 响应与错误诊断 (Diagnostics)</span>
                        <span v-if="log.errorDetails || log.status === 'FAILED'" class="tab-dot dot-danger"></span>
                      </button>

                      <button 
                        :class="['inspector-tab', { active: getActiveSubTab(log.id) === 'headers' }]"
                        @click="setActiveSubTab(log.id, 'headers')"
                      >
                        <span>🕹️ 客户端请求头 (Headers)</span>
                        <span v-if="log.requestHeaders && Object.keys(log.requestHeaders).length > 0" class="tab-dot"></span>
                      </button>
                    </div>

                    <!-- 抽屉内容区 -->
                    <div class="inspector-body">
                      <!-- Tab 1: 请求载荷 (Prompt/Messages/Input) -->
                      <div v-if="getActiveSubTab(log.id) === 'request'" class="sub-tab-content">
                        <div class="code-header-bar">
                          <span class="code-title">客户端原始发送 JSON Payload:</span>
                          <button 
                            v-if="log.requestBody" 
                            class="btn btn-secondary btn-xs" 
                            @click="copyText(log.requestBody, '请求载荷已复制')"
                          >
                            📋 复制完整载荷
                          </button>
                        </div>
                        <div v-if="log.requestBody" class="code-block-wrapper">
                          <pre class="code-block font-mono">{{ formatJsonString(log.requestBody) }}</pre>
                        </div>
                        <div v-else class="empty-hint">该请求未携带请求体或为空。</div>
                      </div>

                      <!-- Tab 2: 响应与错误诊断 -->
                      <div v-else-if="getActiveSubTab(log.id) === 'response'" class="sub-tab-content">
                        <!-- 错误高亮卡片 -->
                        <div v-if="log.errorDetails || log.status === 'FAILED'" class="error-alert-card">
                          <div class="error-card-top">
                            <span class="error-badge-title">🚨 错误排查追踪:</span>
                            <span v-if="log.errorDetails && log.errorDetails.includes('invalid_encrypted_content')" class="badge badge-warning">
                              已识别：加密数据无法解密
                            </span>
                          </div>
                          <pre class="error-code font-mono">{{ log.errorDetails || '上游调用发生异常中断' }}</pre>
                          
                          <div v-if="log.errorDetails && log.errorDetails.includes('invalid_encrypted_content')" class="solution-box">
                            <strong>💡 智能修复提示：</strong>
                            这是上游服务商对前一轮加密思维链（<code>encrypted_content</code>）的校验报错。当前网关已内置【自动剔除请求加密数据】功能，在网关设置中保持开启即可彻底杜绝该错误。
                          </div>
                        </div>

                        <!-- 响应文本预览 -->
                        <div v-if="log.responseBody" class="response-preview-box">
                          <div class="code-header-bar">
                            <span class="code-title">上游返回内容摘要 (Response Preview):</span>
                            <button class="btn btn-secondary btn-xs" @click="copyText(log.responseBody, '响应内容已复制')">📋 复制</button>
                          </div>
                          <pre class="code-block font-mono">{{ formatJsonString(log.responseBody) }}</pre>
                        </div>
                        <div v-else-if="!log.errorDetails && log.status !== 'FAILED'" class="empty-hint">
                          {{ log.status === 'PENDING' ? '⏳ 正在等待上游响应传输...' : '该流式请求已直通传输完成。' }}
                        </div>
                      </div>

                      <!-- Tab 3: 请求头嗅探 -->
                      <div v-else-if="getActiveSubTab(log.id) === 'headers'" class="sub-tab-content">
                        <div class="code-header-bar">
                          <span class="code-title">嗅探到的客户端环境头 (Client Headers):</span>
                          <button 
                            v-if="log.requestHeaders" 
                            class="btn btn-secondary btn-xs" 
                            @click="copyText(JSON.stringify(log.requestHeaders, null, 2), '请求头已复制')"
                          >
                            📋 复制 Headers
                          </button>
                        </div>
                        <div v-if="log.requestHeaders && Object.keys(log.requestHeaders).length > 0" class="headers-list-grid">
                          <div v-for="(val, key) in log.requestHeaders" :key="key" class="header-key-val font-mono">
                            <span class="h-key">{{ key }}:</span>
                            <span class="h-val" :title="val">{{ val }}</span>
                          </div>
                        </div>
                        <div v-else class="empty-hint">未采集到有效的客户端请求头。</div>
                      </div>
                    </div>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>

      <!-- 底部精致分页器 -->
      <div class="pagination-footer">
        <div class="page-meta">
          <span>共 <strong class="text-primary font-mono">{{ totalCount }}</strong> 条日志，</span>
          <span>第 <strong class="font-mono">{{ currentPage }}</strong> / <strong class="font-mono">{{ totalPages }}</strong> 页</span>
          <div class="page-size-wrap">
            <span class="size-txt">每页:</span>
            <select :value="pageSize" @change="onPageSizeChange(Number($event.target.value))" class="form-select size-select font-mono">
              <option :value="20">20 条</option>
              <option :value="50">50 条</option>
              <option :value="100">100 条</option>
              <option :value="200">200 条</option>
            </select>
          </div>
        </div>

        <div class="page-nav-btns">
          <button class="btn btn-secondary btn-xs" :disabled="currentPage <= 1" @click="goToPage(1)">« 首页</button>
          <button class="btn btn-secondary btn-xs" :disabled="currentPage <= 1" @click="goToPage(currentPage - 1)">‹ 上一页</button>
          
          <template v-for="(p, idx) in visiblePages" :key="idx">
            <span v-if="p === '...'" class="page-dots text-dim">...</span>
            <button 
              v-else 
              :class="['page-number-btn font-mono', { active: p === currentPage }]" 
              @click="goToPage(p)"
            >
              {{ p }}
            </button>
          </template>

          <button class="btn btn-secondary btn-xs" :disabled="currentPage >= totalPages" @click="goToPage(currentPage + 1)">下一页 ›</button>
          <button class="btn btn-secondary btn-xs" :disabled="currentPage >= totalPages" @click="goToPage(totalPages)">末页 »</button>
        </div>
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
          <div class="form-group">
            <div class="switch-row">
              <label class="form-label mb-0">自动清理过期与超限日志</label>
              <label class="switch">
                <input type="checkbox" v-model="logSettings.autoCleanupEnabled">
                <span class="slider"></span>
              </label>
            </div>
            <p class="form-hint">开启后将按设定的天数与容量阈值定期自动淘汰最旧的历史记录，保证网关轻量运行。</p>
          </div>

          <div class="form-group" v-if="logSettings.autoCleanupEnabled">
            <label class="form-label">历史保留天数</label>
            <select v-model.number="logSettings.retentionDays" class="form-select">
              <option :value="1">保留最近 1 天</option>
              <option :value="3">保留最近 3 天</option>
              <option :value="7">保留最近 7 天 (推荐)</option>
              <option :value="14">保留最近 14 天</option>
              <option :value="30">保留最近 30 天</option>
              <option :value="0">永久保留 (不限天数)</option>
            </select>
          </div>

          <div class="form-group" v-if="logSettings.autoCleanupEnabled">
            <label class="form-label">最大记录条数</label>
            <select v-model.number="logSettings.maxCapacity" class="form-select font-mono">
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
            <span>{{ savingSettings ? '保存中...' : '💾 保存策略' }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue';
import { api } from '../api';

const props = defineProps({
  logs: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['refresh', 'toast']);

const pagedLogs = ref([]);
const totalCount = ref(0);
const currentPage = ref(1);
const pageSize = ref(50);
const loading = ref(false);

const currentFilter = ref('all');
const searchKeyword = ref('');
const autoRefresh = ref(true);
const showSettingsModal = ref(false);
const savingSettings = ref(false);

const expandedRows = reactive({});
const activeSubTabs = reactive({});

const logSettings = reactive({
  autoCleanupEnabled: true,
  retentionDays: 7,
  maxCapacity: 2000
});

let searchDebounceTimer = null;
let pollTimer = null;

// 指标计算
const pendingCount = computed(() => pagedLogs.value.filter(l => l.status === 'PENDING').length);
const successCount = computed(() => pagedLogs.value.filter(l => l.status === 'SUCCESS' || (l.statusCode >= 200 && l.statusCode < 400 && l.status !== 'FAILED')).length);
const failoverCount = computed(() => pagedLogs.value.filter(l => l.isFailover || l.status === 'FAILED' || l.statusCode >= 400).length);
const calcRate = computed(() => {
  if (totalCount.value === 0) return 100;
  return Math.round((successCount.value / Math.max(1, pagedLogs.value.length)) * 100);
});

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)));

const filterTabs = [
  { id: 'all', label: '全部' },
  { id: 'pending', label: '进行中 (PENDING)' },
  { id: 'success', label: '成功响应' },
  { id: 'failed', label: '失败 / 异常' },
  { id: 'failover', label: '故障转移' }
];

function getActiveSubTab(logId) {
  return activeSubTabs[logId] || 'request';
}

function setActiveSubTab(logId, tab) {
  activeSubTabs[logId] = tab;
}

function toggleExpand(logId) {
  expandedRows[logId] = !expandedRows[logId];
  if (!activeSubTabs[logId]) {
    activeSubTabs[logId] = 'request';
  }
}

function formatTime(isoStr) {
  if (!isoStr) return '-';
  const d = new Date(isoStr);
  if (isNaN(d.getTime())) return isoStr;
  const pad = n => String(n).padStart(2, '0');
  return `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

function formatJsonString(str) {
  if (!str) return '';
  try {
    const obj = JSON.parse(str);
    return JSON.stringify(obj, null, 2);
  } catch {
    return str;
  }
}

async function copyText(text, successMsg = '已复制') {
  if (!text) return;
  try {
    await navigator.clipboard.writeText(text);
    emit('toast', successMsg, 'success');
  } catch {
    emit('toast', '复制失败，请手动选择复制', 'error');
  }
}

async function fetchLogs() {
  loading.value = true;
  try {
    const res = await api.getPagedLogs({
      page: currentPage.value,
      pageSize: pageSize.value,
      filter: currentFilter.value,
      keyword: searchKeyword.value.trim()
    });
    if (res) {
      pagedLogs.value = res.items || [];
      totalCount.value = res.totalCount || 0;
    }
  } catch (err) {
    console.error('拉取日志失败:', err);
  } finally {
    loading.value = false;
  }
}

function onFilterChange(filterId) {
  currentFilter.value = filterId;
  currentPage.value = 1;
  fetchLogs();
}

function onSearchInput() {
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer);
  searchDebounceTimer = setTimeout(() => {
    currentPage.value = 1;
    fetchLogs();
  }, 300);
}

function clearSearch() {
  searchKeyword.value = '';
  currentPage.value = 1;
  fetchLogs();
}

function toggleAutoRefresh() {
  autoRefresh.value = !autoRefresh.value;
  if (autoRefresh.value) {
    startPolling();
    emit('toast', '已开启每 2 秒自动同步日志', 'info');
  } else {
    stopPolling();
    emit('toast', '已暂停自动同步', 'info');
  }
}

function startPolling() {
  stopPolling();
  pollTimer = setInterval(() => {
    if (!loading.value) {
      fetchLogs();
    }
  }, 2000);
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}

function onPageSizeChange(newSize) {
  pageSize.value = newSize;
  currentPage.value = 1;
  fetchLogs();
}

function goToPage(page) {
  if (page < 1 || page > totalPages.value) return;
  currentPage.value = page;
  fetchLogs();
}

const visiblePages = computed(() => {
  const pages = [];
  const total = totalPages.value;
  const current = currentPage.value;
  if (total <= 7) {
    for (let i = 1; i <= total; i++) pages.push(i);
  } else {
    pages.push(1);
    if (current > 3) pages.push('...');
    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);
    for (let i = start; i <= end; i++) pages.push(i);
    if (current < total - 2) pages.push('...');
    pages.push(total);
  }
  return pages;
});

async function clearLogs() {
  if (!confirm('确定要清空所有请求日志吗？此操作不可恢复。')) return;
  try {
    await api.clearLogs();
    pagedLogs.value = [];
    totalCount.value = 0;
    emit('toast', '所有请求日志已成功清空', 'success');
  } catch (err) {
    emit('toast', `清空失败: ${err.message}`, 'error');
  }
}

async function loadLogSettings() {
  try {
    const res = await api.getLogSettings();
    if (res) Object.assign(logSettings, res);
  } catch { }
}

async function saveSettings() {
  savingSettings.value = true;
  try {
    await api.saveLogSettings(logSettings);
    showSettingsModal.value = false;
    emit('toast', '日志清理策略已保存生效！', 'success');
  } catch (err) {
    emit('toast', `保存失败: ${err.message}`, 'error');
  } finally {
    savingSettings.value = false;
  }
}

onMounted(() => {
  fetchLogs();
  loadLogSettings();
  if (autoRefresh.value) startPolling();
});

onUnmounted(() => {
  stopPolling();
});
</script>

<style scoped>
.logs-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 1. 指标看板行 */
.metrics-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 14px;
}

.metric-card {
  padding: 16px 18px;
  display: flex;
  flex-direction: column;
}

.metric-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.metric-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-dim);
}

.metric-icon {
  font-size: 14px;
}

.metric-value {
  font-size: 24px;
  font-weight: 700;
  letter-spacing: -0.5px;
  margin-bottom: 4px;
}

.metric-footer {
  font-size: 11px;
}

/* 2. 操作栏 */
.action-toolbar {
  padding: 10px 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}

.status-tabs {
  display: flex;
  gap: 4px;
  background: rgba(0, 0, 0, 0.25);
  padding: 3px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
}

.status-tab-btn {
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 500;
  border-radius: var(--radius-sm);
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  transition: all 0.16s ease;
  display: flex;
  align-items: center;
  gap: 6px;
}

.status-tab-btn:hover {
  color: var(--text-main);
}

.status-tab-btn.active {
  background: var(--bg-surface-elevated);
  color: #818cf8;
  font-weight: 600;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}

.toolbar-right-tools {
  display: flex;
  align-items: center;
  gap: 8px;
}

.search-input-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.search-ico {
  position: absolute;
  left: 10px;
  font-size: 12px;
  color: var(--text-dim);
}

.search-field {
  padding: 6px 28px 6px 28px;
  width: 260px;
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  color: var(--text-main);
  font-size: 12px;
  outline: none;
  transition: border-color 0.2s;
}

.search-field:focus {
  border-color: var(--accent-primary);
}

.search-clear-btn {
  position: absolute;
  right: 8px;
  background: none;
  border: none;
  color: var(--text-dim);
  cursor: pointer;
  font-size: 11px;
}

/* 3. 数据表格容器 */
.logs-container {
  overflow: hidden;
}

.table-responsive {
  overflow-x: auto;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.data-table th {
  padding: 12px 16px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-dim);
  background: rgba(0, 0, 0, 0.15);
  border-bottom: 1px solid var(--border-subtle);
  white-space: nowrap;
}

.data-table td {
  padding: 12px 16px;
  font-size: 13px;
  border-bottom: 1px solid var(--border-subtle);
  vertical-align: middle;
}

.data-row {
  cursor: pointer;
  transition: background-color 0.15s ease;
}

.data-row:hover {
  background-color: var(--bg-card-hover);
}

.data-row.is-expanded {
  background-color: var(--bg-surface-elevated);
}

.row-failover {
  border-left: 2px solid var(--warning);
}

.row-error {
  border-left: 2px solid var(--danger);
}

.row-pending {
  border-left: 2px solid var(--pending);
}

/* 单元格微元素 */
.path-badge {
  display: flex;
  align-items: center;
  gap: 6px;
  max-width: 260px;
}

.method-tag {
  font-size: 10px;
  font-weight: 700;
  color: var(--accent-primary);
  background: rgba(99, 102, 241, 0.15);
  padding: 2px 4px;
  border-radius: var(--radius-xs);
}

.path-val {
  font-size: 12px;
  color: var(--text-main);
}

.model-pill {
  font-size: 12px;
  background: rgba(255, 255, 255, 0.05);
  padding: 3px 8px;
  border-radius: var(--radius-sm);
  display: inline-block;
  max-width: 140px;
}

.channel-pill {
  font-size: 12px;
  color: #a5b4fc;
  display: inline-block;
  max-width: 150px;
}

.trail-chain {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-wrap: wrap;
}

.trail-chip {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: var(--radius-xs);
  font-family: var(--font-mono);
}

.chip-fail {
  background: var(--danger-bg);
  color: var(--danger);
}

.chip-success {
  background: var(--success-bg);
  color: var(--success);
}

.trail-sep {
  font-size: 10px;
  color: var(--text-dim);
}

.timing-box {
  display: flex;
  flex-direction: column;
}

.timing-ms {
  font-size: 12px;
  color: var(--text-main);
}

.tokens-mini {
  font-size: 10px;
  color: var(--text-dim);
}

.expand-caret {
  font-size: 9px;
  margin-left: 4px;
}

/* 4. 展开的诊断抽屉 Inspector Panel */
.inspector-row td {
  padding: 0;
  background: rgba(0, 0, 0, 0.2);
}

.inspector-panel {
  margin: 10px 16px 16px;
  border: 1px solid var(--border-medium);
  background: var(--bg-surface);
  overflow: hidden;
}

.inspector-tabs {
  display: flex;
  background: rgba(0, 0, 0, 0.25);
  border-bottom: 1px solid var(--border-subtle);
}

.inspector-tab {
  padding: 10px 16px;
  font-size: 12px;
  font-weight: 500;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--text-muted);
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: all 0.16s ease;
}

.inspector-tab:hover {
  color: var(--text-main);
}

.inspector-tab.active {
  color: #818cf8;
  border-bottom-color: var(--accent-primary);
  background: rgba(99, 102, 241, 0.08);
}

.tab-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--accent-primary);
}

.dot-danger {
  background: var(--danger);
}

.inspector-body {
  padding: 16px;
}

.code-header-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.code-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-dim);
}

.code-block-wrapper {
  max-height: 380px;
  overflow-y: auto;
  background: rgba(0, 0, 0, 0.35);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  padding: 12px;
}

.code-block {
  font-size: 12px;
  line-height: 1.5;
  color: #e2e8f0;
  white-space: pre-wrap;
  word-break: break-all;
}

.error-alert-card {
  background: var(--danger-bg);
  border: 1px solid var(--danger-border);
  border-radius: var(--radius-md);
  padding: 14px;
  margin-bottom: 14px;
}

.error-card-top {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.error-badge-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--danger);
}

.error-code {
  color: #fecdd3;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
}

.solution-box {
  margin-top: 10px;
  padding: 8px 12px;
  background: rgba(0, 0, 0, 0.25);
  border-radius: var(--radius-sm);
  font-size: 12px;
  color: #fed7aa;
}

.headers-list-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: 8px;
  background: rgba(0, 0, 0, 0.25);
  padding: 12px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
}

.header-key-val {
  display: flex;
  gap: 8px;
  font-size: 12px;
}

.h-key {
  color: #a5b4fc;
  font-weight: 600;
}

.h-val {
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-hint {
  font-size: 12px;
  color: var(--text-dim);
  padding: 16px 0;
  text-align: center;
}

/* 5. 分页器 */
.pagination-footer {
  padding: 12px 18px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-top: 1px solid var(--border-subtle);
  flex-wrap: wrap;
  gap: 12px;
}

.page-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--text-dim);
}

.page-size-wrap {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-left: 12px;
}

.size-select {
  width: auto;
  padding: 3px 8px;
  font-size: 12px;
}

.page-nav-btns {
  display: flex;
  align-items: center;
  gap: 4px;
}

.page-number-btn {
  min-width: 26px;
  height: 26px;
  padding: 0 6px;
  font-size: 12px;
  border-radius: var(--radius-xs);
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  color: var(--text-muted);
  cursor: pointer;
}

.page-number-btn.active {
  background: var(--accent-primary);
  color: #ffffff;
  border-color: var(--accent-primary);
  font-weight: 600;
}

/* 空态与加载 */
.state-box {
  padding: 48px 24px;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.state-icon {
  font-size: 32px;
}

.state-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-muted);
}

.state-desc {
  font-size: 12px;
  color: var(--text-dim);
}

.loading-ring {
  width: 28px;
  height: 28px;
  border: 2px solid rgba(99, 102, 241, 0.2);
  border-top-color: var(--accent-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 8px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* 模态弹窗 */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-container {
  width: 460px;
  max-width: 90vw;
  padding: 20px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border-subtle);
}

.modal-title {
  font-size: 14px;
  font-weight: 600;
}

.close-btn {
  background: none;
  border: none;
  color: var(--text-dim);
  cursor: pointer;
  font-size: 14px;
}

.form-group {
  margin-bottom: 14px;
}

.form-label {
  display: block;
  font-size: 12px;
  font-weight: 500;
  color: var(--text-dim);
  margin-bottom: 6px;
}

.form-hint {
  font-size: 11px;
  color: var(--text-dim);
  margin-top: 4px;
}

.switch-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
  padding-top: 12px;
  border-top: 1px solid var(--border-subtle);
}
</style>
