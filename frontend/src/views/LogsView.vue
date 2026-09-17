<template>
  <div class="logs-page">
    <!-- 0. 顶部视图双模切换：模型请求流水 vs 后端服务运行日志 -->
    <div class="view-mode-tabs">
      <button 
        :class="['mode-tab-btn', { active: activeViewMode === 'requests' }]" 
        @click="switchToRequests"
      >
        <span class="tab-ico">📋</span>
        <span>模型请求与全链路追踪</span>
        <span class="tab-badge-num font-mono">{{ summaryMetrics.totalRequests || totalCount }}</span>
      </button>

      <button 
        :class="['mode-tab-btn', { active: activeViewMode === 'system' }]" 
        @click="switchToSysLogs"
      >
        <span class="tab-ico">💻</span>
        <span>后端服务运行日志 (Console)</span>
        <span class="pulse-indicator pulse-green" style="width: 6px; height: 6px;"></span>
      </button>
    </div>

    <!-- ================= 模式 1: 模型请求流水与全链路追踪 ================= -->
    <div v-if="activeViewMode === 'requests'" class="requests-view-wrapper">
      <!-- 1. 顶部数据指标看板卡片 -->
      <div class="metrics-grid">
        <div class="metric-card glass-card">
          <div class="metric-header">
            <span class="metric-label">总请求调用</span>
            <span class="metric-icon">📊</span>
          </div>
          <div class="metric-value font-mono">{{ summaryMetrics.totalRequests || totalCount }}</div>
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
          <div class="metric-footer text-dim">已自动完成备用重试 ({{ summaryMetrics.totalFailovers }} 次)</div>
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
        <!-- 局部独立滚动视口（向下滑动只滚动框选区域并动态加载） -->
        <div 
          class="table-scroll-viewport" 
          ref="tableScrollViewportRef" 
          @scroll="handleTableScroll"
        >
          <table class="data-table">
            <thead class="sticky-thead">
              <tr>
                <th style="width: 105px;">请求状态</th>
                <th style="width: 110px;">发起时间</th>
                <th>端点路由</th>
                <th>请求模型</th>
                <th>故障转移调度链路</th>
                <th>最终渠道</th>
                <th style="width: 95px;">耗时 / Tokens</th>
                <th style="width: 110px; text-align: right;">载荷与诊断</th>
              </tr>
            </thead>
            <tbody>
              <!-- 虚拟滚动顶部垫高占位 -->
              <tr v-if="virtualRange.paddingTop > 0" :style="{ height: `${virtualRange.paddingTop}px` }">
                <td colspan="8" style="padding: 0; border: none;"></td>
              </tr>
              <template v-for="log in visibleLogs" :key="log.id">
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
                              @click="downloadJson(log.requestBody, `request-payload-${log.id || 'body'}.json`)"
                              title="下载客户端请求体 JSON 文件"
                            >
                              📥 下载 JSON
                            </button>
                          </div>
                          <div v-if="log.requestBody" class="code-viewer font-mono">
                            <pre>{{ formatJsonString(log.requestBody) }}</pre>
                          </div>
                          <div v-else class="empty-hint">
                            未记录请求体（可能由于请求体为空、超出限制或此前未开启完整载荷捕获）。
                          </div>
                        </div>

                        <!-- Tab 2: 响应与错误诊断 -->
                        <div v-else-if="getActiveSubTab(log.id) === 'response'" class="sub-tab-content">
                          <!-- 错误诊断提示框 -->
                          <div v-if="log.errorDetails" class="error-diag-card">
                            <div class="diag-title">🚨 上游错误诊断与失败原因:</div>
                            <pre class="diag-content font-mono">{{ log.errorDetails }}</pre>
                          </div>

                          <div class="code-header-bar mt-12">
                            <span class="code-title">响应快照 / 最终返回摘要:</span>
                            <button 
                              v-if="log.responseBody" 
                              class="btn btn-secondary btn-xs" 
                              @click="copyText(formatJsonString(log.responseBody), '响应快照已复制')"
                            >
                              📋 复制响应
                            </button>
                          </div>
                          <div v-if="log.responseBody" class="code-viewer font-mono">
                            <pre>{{ formatJsonString(log.responseBody) }}</pre>
                          </div>
                          <div v-else-if="!log.errorDetails" class="empty-hint">
                            {{ log.status === 'PENDING' ? '流式请求传输中，等待最终完成响应...' : '未记录响应体内容或该请求直接返回了流式 SSE。' }}
                          </div>
                        </div>

                        <!-- Tab 3: 请求头信息 -->
                        <div v-else-if="getActiveSubTab(log.id) === 'headers'" class="sub-tab-content">
                          <div class="code-header-bar">
                            <span class="code-title">客户端已发送 HTTP Headers (脱敏后):</span>
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
              <!-- 虚拟滚动底部垫高占位 -->
              <tr v-if="virtualRange.paddingBottom > 0" :style="{ height: `${virtualRange.paddingBottom}px` }">
                <td colspan="8" style="padding: 0; border: none;"></td>
              </tr>
            </tbody>
          </table>

          <!-- 触底动态加载指示栏 -->
          <div class="scroll-load-indicator">
            <div v-if="loadingMore" class="loading-more-box">
              <span class="mini-spinner"></span>
              <span class="load-text">正在向下滑动动态加载下一批日志...</span>
            </div>
            <div v-else-if="pagedLogs.length >= totalCount && totalCount > 0" class="loaded-all-box">
              <span class="check-dot">✓</span>
              <span>已向下滑动加载全部 {{ totalCount }} 条历史记录</span>
            </div>
            <div v-else-if="pagedLogs.length > 0 && pagedLogs.length < totalCount" class="scroll-more-hint">
              <span>↓ 继续向下滑动自动加载下一批 15 条日志</span>
            </div>
          </div>
        </div>

        <!-- 底部轻量信息与快速回顶栏 -->
        <div class="scroll-footer-bar">
          <div class="scroll-footer-left">
            <span>已动态展示 <strong class="text-primary font-mono">{{ pagedLogs.length }}</strong> / 共 <strong class="font-mono">{{ totalCount }}</strong> 条请求日志</span>
            <span v-if="isVirtualScrollActive" class="badge badge-info font-mono" style="font-size: 10.5px; padding: 1px 6px;" title="虚拟滚动引擎：仅渲染视口切片节点，杜绝海量日志卡顿">
              ⚡ 虚拟渲染已加速 (渲染 {{ visibleLogs.length }}/{{ pagedLogs.length }} 项)
            </span>
            <span v-if="loadingMore" class="badge badge-pending font-mono">加载中...</span>
            <span class="scroll-hint-tip">💡 默认展示 15 条，向下滑动仅在框选区域内局部滚动并动态加载</span>
          </div>

          <div class="scroll-footer-right">
            <button 
              v-if="pagedLogs.length > 15" 
              class="btn btn-secondary btn-xs" 
              @click="scrollToTop"
              title="快速返回表格顶部"
            >
              <span>▲ 回到顶部</span>
            </button>
            <button 
              v-if="pagedLogs.length < totalCount" 
              class="btn btn-secondary btn-xs" 
              :disabled="loadingMore" 
              @click="loadMoreLogs"
              title="也可手动点击加载下一批 15 条"
            >
              <span>{{ loadingMore ? '加载中...' : '加载更多 15 条 ↓' }}</span>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- ================= 模式 2: 后端服务运行控制台日志 ================= -->
    <div v-else class="system-logs-container glass-card">
      <div class="syslogs-toolbar">
        <div class="syslogs-left">
          <span class="sys-label">日志级别:</span>
          <div class="sys-level-pills">
            <button 
              v-for="lvl in sysLogLevels" 
              :key="lvl"
              :class="['pill-btn', { active: selectedSysLevel === lvl }]"
              @click="selectedSysLevel = lvl; fetchSysLogs()"
            >
              {{ lvl }}
            </button>
          </div>
        </div>

        <div class="syslogs-actions">
          <label class="auto-refresh-check" title="每 2 秒实时同步服务日志">
            <input type="checkbox" v-model="autoRefreshSysLogs" @change="toggleAutoRefreshSysLogs" />
            <span>实时刷新 (2s)</span>
          </label>

          <button class="btn btn-secondary btn-xs" @click="fetchSysLogs" :disabled="fetchingSysLogs">
            <span>🔄 刷新</span>
          </button>

          <button class="btn btn-secondary btn-xs" @click="copyAllSysLogs" :disabled="sysLogs.length === 0">
            <span>📋 复制全部</span>
          </button>

          <button class="btn btn-danger btn-xs" @click="clearSysLogs" :disabled="sysLogs.length === 0">
            <span>🗑️ 清空</span>
          </button>
        </div>
      </div>

      <div class="terminal-screen font-mono" ref="sysLogRef">
        <div v-if="sysLogs.length === 0" class="terminal-empty">
          > 暂无系统级运行日志...
        </div>
        <div 
          v-for="log in sysLogs" 
          :key="log.id" 
          :class="['term-row', `level-${(log.level || '').toLowerCase()}`]"
        >
          <span class="t-time">[{{ formatTime(log.timestamp) }}]</span>
          <span :class="['t-level', `badge-${(log.level || '').toLowerCase()}`]">[{{ log.level }}]</span>
          <span class="t-cat" v-if="log.category">[{{ log.category }}]</span>
          <span class="t-msg">{{ log.message }}</span>
          <div v-if="log.exception" class="t-exc">{{ log.exception }}</div>
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

// 视图模式：'requests'（模型业务流水）| 'system'（系统服务运行日志）
const activeViewMode = ref('requests');

// 1. 请求业务日志相关状态
const pagedLogs = ref([]);
const totalCount = ref(0);
const currentPage = ref(1);
const pageSize = ref(15); // 默认展示 15 条数据
const loading = ref(false);
const loadingMore = ref(false);
const tableScrollViewportRef = ref(null);

// 虚拟滚动引擎状态 (Virtual Scroll Windowing)
const scrollTop = ref(0);
const viewportHeight = ref(600);
const rowHeight = 52;
const bufferCount = 6;

const isVirtualScrollActive = computed(() => pagedLogs.value.length > 25);

const virtualRange = computed(() => {
  if (!isVirtualScrollActive.value) {
    return {
      startIndex: 0,
      endIndex: pagedLogs.value.length,
      paddingTop: 0,
      paddingBottom: 0
    };
  }

  const start = Math.max(0, Math.floor(scrollTop.value / rowHeight) - bufferCount);
  const end = Math.min(
    pagedLogs.value.length,
    Math.ceil((scrollTop.value + viewportHeight.value) / rowHeight) + bufferCount
  );

  return {
    startIndex: start,
    endIndex: end,
    paddingTop: start * rowHeight,
    paddingBottom: Math.max(0, (pagedLogs.value.length - end) * rowHeight)
  };
});

const visibleLogs = computed(() => {
  if (!isVirtualScrollActive.value) {
    return pagedLogs.value;
  }
  return pagedLogs.value.slice(virtualRange.value.startIndex, virtualRange.value.endIndex);
});

const currentFilter = ref('all');
const searchKeyword = ref('');
const autoRefresh = ref(true);
const showSettingsModal = ref(false);
const savingSettings = ref(false);

const expandedRows = reactive({});
const activeSubTabs = reactive({});

const summaryMetrics = reactive({
  totalRequests: 0,
  successfulRequests: 0,
  failedRequests: 0,
  pendingRequests: 0,
  totalFailovers: 0,
  successRate: 100
});

const logSettings = reactive({
  autoCleanupEnabled: true,
  retentionDays: 7,
  maxCapacity: 2000
});

let searchDebounceTimer = null;
let pollTimer = null;

// 指标计算
const pendingCount = computed(() => summaryMetrics.pendingRequests || pagedLogs.value.filter(l => l.status === 'PENDING').length);
const successCount = computed(() => summaryMetrics.successfulRequests || pagedLogs.value.filter(l => l.status === 'SUCCESS' || (l.statusCode >= 200 && l.statusCode < 400 && l.status !== 'FAILED')).length);
const failoverCount = computed(() => summaryMetrics.failedRequests || summaryMetrics.totalFailovers || pagedLogs.value.filter(l => l.isFailover || l.status === 'FAILED' || l.statusCode >= 400).length);
const calcRate = computed(() => summaryMetrics.successRate || 100);

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)));

const filterTabs = [
  { id: 'all', label: '全部' },
  { id: 'pending', label: '进行中 (PENDING)' },
  { id: 'success', label: '成功响应' },
  { id: 'failed', label: '失败 / 异常' },
  { id: 'failover', label: '故障转移' }
];

// 2. 系统服务运行日志相关状态
const sysLogs = ref([]);
const selectedSysLevel = ref('ALL');
const autoRefreshSysLogs = ref(true);
const fetchingSysLogs = ref(false);
const sysLogRef = ref(null);
let sysPollTimer = null;

const sysLogLevels = ['ALL', 'INFO', 'WARN', 'ERROR'];

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

// 下载 JSON 文本为本地文件
function downloadJson(jsonContent, defaultFilename = 'request-payload.json') {
  if (!jsonContent) return;
  try {
    const formatted = formatJsonString(jsonContent);
    const blob = new Blob([formatted], { type: 'application/json;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = defaultFilename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
    emit('toast', `已触发下载: ${defaultFilename}`, 'success');
  } catch (err) {
    emit('toast', `下载失败: ${err.message}`, 'error');
  }
}

// 拉取分页请求日志与全网概览指标（默认加载首屏 15 条，支持自动轮询静默更新）
async function fetchLogs(isAutoPoll = false) {
  if (!isAutoPoll) {
    loading.value = true;
    currentPage.value = 1;
  }

  try {
    const [res, s] = await Promise.all([
      api.getPagedLogs(
        1,
        pageSize.value,
        currentFilter.value,
        searchKeyword.value.trim()
      ),
      api.getSummary()
    ]);

    if (res) {
      totalCount.value = res.totalCount || 0;
      if (!isAutoPoll) {
        pagedLogs.value = res.items || [];
        if (tableScrollViewportRef.value) {
          tableScrollViewportRef.value.scrollTop = 0;
        }
      } else {
        // 定时自动轮询：
        // 1. 如果用户停留在前 15 条内，直接静默更新最新数据
        if (currentPage.value === 1 && pagedLogs.value.length <= pageSize.value) {
          pagedLogs.value = res.items || [];
        } else if (res.items && res.items.length > 0 && pagedLogs.value.length > 0) {
          // 2. 如果用户已经向下滚动加载了更多批次，检查是否有新进来的请求，仅将最新条目置顶追加
          const currentFirstId = pagedLogs.value[0].id;
          const newIdx = res.items.findIndex(item => item.id === currentFirstId);
          if (newIdx > 0) {
            const freshItems = res.items.slice(0, newIdx);
            pagedLogs.value.unshift(...freshItems);
          }
        }
      }
    }

    if (s) {
      Object.assign(summaryMetrics, s);
    }
  } catch (err) {
    console.error('拉取请求日志失败:', err);
  } finally {
    if (!isAutoPoll) {
      loading.value = false;
    }
  }
}

// 向下滑动时动态加载下一批 15 条日志
async function loadMoreLogs() {
  if (loadingMore.value || loading.value) return;
  if (pagedLogs.value.length >= totalCount.value) return;

  loadingMore.value = true;
  const nextPage = currentPage.value + 1;
  try {
    const res = await api.getPagedLogs(
      nextPage,
      pageSize.value,
      currentFilter.value,
      searchKeyword.value.trim()
    );
    if (res && res.items && res.items.length > 0) {
      currentPage.value = nextPage;
      // 按照 ID 去重并追加到当前列表末尾
      const existingIds = new Set(pagedLogs.value.map(l => l.id));
      const newItems = res.items.filter(l => !existingIds.has(l.id));
      pagedLogs.value.push(...newItems);
      totalCount.value = res.totalCount || totalCount.value;
    } else {
      totalCount.value = pagedLogs.value.length;
    }
  } catch (err) {
    console.error('动态加载更多日志失败:', err);
  } finally {
    loadingMore.value = false;
  }
}

// 监听框选区域的独立滚动（触底自动动态加载 + 虚拟滚动切片）
function handleTableScroll(e) {
  const el = e.target;
  scrollTop.value = el.scrollTop;
  viewportHeight.value = el.clientHeight || 600;

  // 当向下滑动距离底部小于等于 80px 时自动预加载下一批
  if (el.scrollHeight - el.scrollTop - el.clientHeight <= 80) {
    if (pagedLogs.value.length < totalCount.value && !loadingMore.value && !loading.value) {
      loadMoreLogs();
    }
  }
}

// 快速回到表格顶部
function scrollToTop() {
  if (tableScrollViewportRef.value) {
    tableScrollViewportRef.value.scrollTo({ top: 0, behavior: 'smooth' });
  }
}

// 拉取系统服务运行日志
async function fetchSysLogs() {
  fetchingSysLogs.value = true;
  try {
    const lvl = selectedSysLevel.value === 'ALL' ? '' : selectedSysLevel.value;
    const res = await api.getSystemLogs(200, lvl);
    sysLogs.value = res || [];
  } catch (err) {
    console.error('获取系统服务日志失败:', err);
  } finally {
    fetchingSysLogs.value = false;
  }
}

function switchToSysLogs() {
  activeViewMode.value = 'system';
  stopPolling();
  fetchSysLogs();
  if (autoRefreshSysLogs.value) {
    startSysPolling();
  }
}

function switchToRequests() {
  activeViewMode.value = 'requests';
  stopSysPolling();
  fetchLogs();
  if (autoRefresh.value) {
    startPolling();
  }
}

function toggleAutoRefreshSysLogs() {
  if (autoRefreshSysLogs.value) {
    startSysPolling();
    emit('toast', '已开启每 2 秒同步服务日志', 'info');
  } else {
    stopSysPolling();
    emit('toast', '已暂停服务日志自动同步', 'info');
  }
}

function startSysPolling() {
  stopSysPolling();
  sysPollTimer = setInterval(() => {
    if (!fetchingSysLogs.value && activeViewMode.value === 'system') {
      fetchSysLogs();
    }
  }, 2000);
}

function stopSysPolling() {
  if (sysPollTimer) {
    clearInterval(sysPollTimer);
    sysPollTimer = null;
  }
}

async function clearSysLogs() {
  if (!confirm('确定要清空后端系统运行控制台日志吗？')) return;
  try {
    await api.clearSystemLogs();
    sysLogs.value = [];
    emit('toast', '系统运行日志已清空', 'success');
  } catch (err) {
    emit('toast', `清空失败: ${err.message}`, 'error');
  }
}

async function copyAllSysLogs() {
  if (sysLogs.value.length === 0) return;
  const text = sysLogs.value.map(l => `[${formatTime(l.timestamp)}] [${l.level}] ${l.category ? `[${l.category}] ` : ''}${l.message}${l.exception ? `\n${l.exception}` : ''}`).join('\n');
  copyText(text, `已复制 ${sysLogs.value.length} 条系统运行日志`);
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
    if (!loading.value && activeViewMode.value === 'requests') {
      fetchLogs(true);
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
  if (!confirm('确定要清空全部请求历史记录吗？此操作不可逆。')) return;
  try {
    await api.clearLogs();
    pagedLogs.value = [];
    totalCount.value = 0;
    emit('toast', '请求日志已成功清空', 'success');
    emit('refresh');
    fetchLogs();
  } catch (err) {
    emit('toast', `清空失败: ${err.message}`, 'error');
  }
}

async function loadLogSettings() {
  try {
    const res = await api.getLogSettings();
    if (res) Object.assign(logSettings, res);
  } catch (err) {}
}

async function saveSettings() {
  savingSettings.value = true;
  try {
    await api.saveLogSettings(logSettings);
    showSettingsModal.value = false;
    emit('toast', '日志保留策略已保存', 'success');
    fetchLogs();
  } catch (err) {
    emit('toast', `保存失败: ${err.message}`, 'error');
  } finally {
    savingSettings.value = false;
  }
}

onMounted(() => {
  fetchLogs();
  loadLogSettings();
  if (autoRefresh.value) {
    startPolling();
  }
});

onUnmounted(() => {
  stopPolling();
  stopSysPolling();
});
</script>

<style scoped>
.logs-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 顶部视图双模切换 */
.view-mode-tabs {
  display: flex;
  gap: 10px;
}

.mode-tab-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 18px;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.18s ease;
}

.mode-tab-btn:hover {
  background: var(--bg-card-hover);
  color: var(--text-main);
  border-color: var(--border-medium);
}

.mode-tab-btn.active {
  background: rgba(99, 102, 241, 0.14);
  border-color: rgba(99, 102, 241, 0.4);
  color: #a5b4fc;
  box-shadow: 0 0 16px rgba(99, 102, 241, 0.15);
}

.tab-badge-num {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: var(--radius-full);
  background: rgba(255, 255, 255, 0.08);
  color: var(--text-main);
}

.requests-view-wrapper {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 顶部指标四宫格 */
.metrics-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.metric-card {
  padding: 18px 20px;
  display: flex;
  flex-direction: column;
}

.metric-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.metric-label {
  font-size: 13px;
  color: var(--text-muted);
  font-weight: 500;
}

.metric-value {
  font-size: 28px;
  font-weight: 700;
  color: var(--text-main);
  line-height: 1.2;
  margin-bottom: 6px;
}

.metric-footer {
  font-size: 11px;
}

/* 工具栏 */
.action-toolbar {
  padding: 12px 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
}

.status-tabs {
  display: flex;
  align-items: center;
  gap: 6px;
}

.status-tab-btn {
  padding: 6px 12px;
  border-radius: var(--radius-md);
  border: 1px solid transparent;
  background: transparent;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.16s ease;
  display: flex;
  align-items: center;
  gap: 6px;
}

.status-tab-btn:hover {
  background: var(--bg-card-hover);
  color: var(--text-main);
}

.status-tab-btn.active {
  background: var(--accent-primary);
  color: #ffffff;
  font-weight: 600;
  box-shadow: 0 2px 8px var(--accent-glow);
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
  pointer-events: none;
}

.search-field {
  padding: 6px 28px 6px 30px;
  font-size: 12px;
  width: 260px;
  background: var(--bg-surface-elevated);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  color: var(--text-main);
  outline: none;
  transition: all 0.18s;
}

.search-field:focus {
  border-color: var(--accent-primary);
  box-shadow: 0 0 0 2px var(--accent-glow);
  width: 320px;
}

.search-clear-btn {
  position: absolute;
  right: 8px;
  background: none;
  border: none;
  color: var(--text-dim);
  font-size: 11px;
  cursor: pointer;
}

.search-clear-btn:hover {
  color: var(--text-main);
}

/* 空状态与加载中 */
.state-box {
  padding: 60px 20px;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.state-icon {
  font-size: 40px;
  margin-bottom: 12px;
  opacity: 0.8;
}

.state-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-main);
  margin-bottom: 6px;
}

.state-desc {
  font-size: 13px;
  color: var(--text-dim);
  max-width: 500px;
}

.loading-ring {
  width: 32px;
  height: 32px;
  border: 3px solid rgba(99, 102, 241, 0.2);
  border-top-color: var(--accent-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 14px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* 表格容器与局部独立滚动视口（只滑动框选区域） */
.logs-container {
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.table-scroll-viewport {
  width: 100%;
  max-height: clamp(480px, calc(100vh - 360px), 660px);
  overflow-y: auto;
  overflow-x: auto;
  position: relative;
  scroll-behavior: smooth;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  text-align: left;
}

.sticky-thead th {
  position: sticky;
  top: 0;
  z-index: 10;
  padding: 12px 14px;
  color: var(--text-dim);
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid var(--border-subtle);
  background: var(--bg-surface-elevated);
  box-shadow: 0 1px 0 var(--border-subtle);
}

.data-table th {
  padding: 12px 14px;
  color: var(--text-dim);
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid var(--border-subtle);
  background: var(--bg-surface-elevated);
}

.data-row {
  border-bottom: 1px solid var(--border-subtle);
  cursor: pointer;
  transition: background-color 0.15s ease;
}

.data-row:hover {
  background-color: var(--bg-card-hover);
}

.data-row.is-expanded {
  background-color: rgba(99, 102, 241, 0.05);
}

.data-row.row-failover {
  background-color: rgba(245, 158, 11, 0.04);
}

.data-row.row-error {
  background-color: rgba(244, 63, 94, 0.04);
}

.data-table td {
  padding: 12px 14px;
  vertical-align: middle;
}

/* 单元格微型组件 */
.path-badge {
  display: flex;
  align-items: center;
  gap: 6px;
  max-width: 200px;
}

.method-tag {
  font-size: 10px;
  font-weight: 700;
  padding: 1px 4px;
  border-radius: var(--radius-xs);
  background: rgba(255, 255, 255, 0.08);
  color: var(--text-muted);
}

.path-val {
  font-size: 12px;
  color: var(--text-main);
}

.model-pill {
  display: inline-block;
  font-size: 12px;
  background: rgba(99, 102, 241, 0.08);
  border: 1px solid rgba(99, 102, 241, 0.2);
  color: #a5b4fc;
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  max-width: 140px;
}

.channel-pill {
  display: inline-block;
  font-size: 12px;
  color: var(--text-muted);
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
  font-family: var(--font-mono);
  padding: 1px 6px;
  border-radius: var(--radius-xs);
  max-width: 160px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chip-fail {
  background: var(--danger-bg);
  color: var(--danger);
  border: 1px solid var(--danger-border);
  text-decoration: line-through;
  opacity: 0.85;
}

.chip-success {
  background: var(--success-bg);
  color: var(--success);
  border: 1px solid var(--success-border);
  font-weight: 600;
}

.trail-sep {
  font-size: 10px;
  color: var(--text-dim);
}

.trail-mark {
  font-size: 10px;
  padding: 0 4px;
}

.timing-box {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.timing-ms {
  font-size: 12px;
  color: var(--text-main);
  font-weight: 500;
}

.tokens-mini {
  font-size: 10px;
  color: var(--text-dim);
}

.expand-caret {
  font-size: 9px;
  margin-left: 2px;
}

/* 展开抽屉面板 */
.inspector-row td {
  padding: 0;
  background: var(--bg-surface);
}

.inspector-panel {
  margin: 10px 14px 14px;
  border: 1px solid var(--border-medium);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.inspector-tabs {
  display: flex;
  background: var(--bg-surface-elevated);
  border-bottom: 1px solid var(--border-subtle);
  padding: 4px 10px 0;
  gap: 4px;
}

.inspector-tab {
  padding: 8px 14px;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: all 0.15s ease;
}

.inspector-tab:hover {
  color: var(--text-main);
}

.inspector-tab.active {
  color: var(--accent-primary);
  border-bottom-color: var(--accent-primary);
  background: rgba(99, 102, 241, 0.08);
}

.tab-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--info);
}

.tab-dot.dot-danger {
  background: var(--danger);
  box-shadow: 0 0 6px var(--danger);
}

.inspector-body {
  padding: 16px;
}

.sub-tab-content {
  display: flex;
  flex-direction: column;
}

.code-header-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.code-title {
  font-size: 12px;
  color: var(--text-dim);
  font-weight: 600;
}

.code-viewer {
  background: var(--bg-surface-elevated);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  padding: 12px 14px;
  max-height: 400px;
  overflow: auto;
  font-size: 12px;
  line-height: 1.6;
  color: var(--text-main);
}

.code-viewer pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
}

.error-diag-card {
  background: rgba(244, 63, 94, 0.1);
  border: 1px solid rgba(244, 63, 94, 0.3);
  border-radius: var(--radius-md);
  padding: 12px 14px;
  margin-bottom: 8px;
}

.diag-title {
  font-size: 12px;
  font-weight: 700;
  color: var(--danger);
  margin-bottom: 6px;
}

.diag-content {
  margin: 0;
  font-size: 12px;
  color: #fca5a5;
  white-space: pre-wrap;
  word-break: break-all;
}

.headers-list-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 6px;
  background: var(--bg-surface-elevated);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  padding: 12px;
}

.header-key-val {
  display: flex;
  align-items: baseline;
  gap: 6px;
  font-size: 11px;
  overflow: hidden;
}

.h-key {
  color: #818cf8;
  font-weight: 600;
  flex-shrink: 0;
}

.h-val {
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-hint {
  color: var(--text-dim);
  font-size: 12px;
  font-style: italic;
  padding: 8px 0;
}

.mt-12 {
  margin-top: 12px;
}

/* 触底动态加载指示栏 */
.scroll-load-indicator {
  padding: 14px 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.05);
  border-top: 1px solid var(--border-subtle);
  font-size: 12px;
  color: var(--text-dim);
}

.loading-more-box {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--accent-primary);
  font-weight: 500;
}

.mini-spinner {
  width: 14px;
  height: 14px;
  border: 2px solid rgba(99, 102, 241, 0.2);
  border-top-color: var(--accent-primary);
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

.loaded-all-box {
  display: flex;
  align-items: center;
  gap: 6px;
  color: var(--text-dim);
}

.check-dot {
  color: var(--success);
  font-weight: 700;
}

.scroll-more-hint {
  color: var(--text-muted);
  font-size: 11px;
}

/* 底部轻量信息与快速回顶栏 */
.scroll-footer-bar {
  padding: 10px 18px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-top: 1px solid var(--border-subtle);
  background: var(--bg-surface);
  font-size: 12px;
  gap: 12px;
  flex-wrap: wrap;
}

.scroll-footer-left {
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--text-muted);
  flex-wrap: wrap;
}

.scroll-hint-tip {
  font-size: 11px;
  color: var(--text-dim);
}

.scroll-footer-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* ================= 模式 2: 系统控制台视窗 ================= */
.system-logs-container {
  padding: 16px 20px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.syslogs-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
  border-bottom: 1px solid var(--border-subtle);
  padding-bottom: 12px;
}

.syslogs-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.sys-label {
  font-size: 12px;
  color: var(--text-dim);
}

.sys-level-pills {
  display: flex;
  gap: 4px;
}

.pill-btn {
  padding: 3px 10px;
  font-size: 11px;
  font-weight: 600;
  background: transparent;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-muted);
  cursor: pointer;
  transition: all 0.15s;
}

.pill-btn:hover {
  background: var(--bg-card-hover);
  color: var(--text-main);
}

.pill-btn.active {
  background: var(--accent-primary);
  border-color: var(--accent-primary);
  color: #ffffff;
}

.syslogs-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.auto-refresh-check {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
  cursor: pointer;
  user-select: none;
  margin-right: 4px;
}

.terminal-screen {
  background: var(--bg-surface-elevated);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  padding: 16px;
  height: 640px;
  overflow-y: auto;
  font-size: 12px;
  line-height: 1.7;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.terminal-empty {
  color: var(--text-dim);
  font-style: italic;
  padding: 12px 0;
}

.term-row {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  word-break: break-all;
}

.t-time {
  color: var(--text-dim);
  flex-shrink: 0;
}

.t-level {
  font-weight: 700;
  font-size: 11px;
  flex-shrink: 0;
}

.t-cat {
  color: #818cf8;
  font-weight: 600;
  flex-shrink: 0;
}

.t-msg {
  color: var(--text-main);
}

.level-info .t-level { color: var(--success); }
.level-warn .t-level { color: var(--warning); }
.level-error .t-level { color: var(--danger); }
.level-error .t-msg { color: #fca5a5; }

.t-exc {
  margin-top: 4px;
  padding: 6px 10px;
  background: rgba(244, 63, 94, 0.1);
  border-left: 2px solid var(--danger);
  color: #fecdd3;
  font-size: 11px;
  white-space: pre-wrap;
}

/* 策略弹窗 Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.65);
  backdrop-filter: blur(6px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 200;
}

.modal-container {
  width: 480px;
  max-width: 90vw;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.modal-title {
  font-size: 15px;
  font-weight: 700;
  color: var(--text-main);
}

.close-btn {
  background: none;
  border: none;
  color: var(--text-dim);
  font-size: 16px;
  cursor: pointer;
}

.close-btn:hover {
  color: var(--text-main);
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.switch-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.form-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-main);
}

.mb-0 {
  margin-bottom: 0;
}

.form-hint {
  font-size: 11px;
  color: var(--text-dim);
  line-height: 1.5;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-top: 12px;
  border-top: 1px solid var(--border-subtle);
}

/* 开关组件 Switch */
.switch {
  position: relative;
  display: inline-block;
  width: 38px;
  height: 20px;
}

.switch input {
  opacity: 0;
  width: 0;
  height: 0;
}

.slider {
  position: absolute;
  cursor: pointer;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: rgba(255, 255, 255, 0.15);
  transition: .3s;
  border-radius: 20px;
}

.slider:before {
  position: absolute;
  content: "";
  height: 14px;
  width: 14px;
  left: 3px;
  bottom: 3px;
  background-color: white;
  transition: .3s;
  border-radius: 50%;
}

input:checked + .slider {
  background-color: var(--accent-primary);
}

input:checked + .slider:before {
  transform: translateX(18px);
}

/* ================= 浅色模式专属视觉精修 ================= */
:global(body.light) .mode-tab-btn {
  background: #ffffff;
  border-color: #e2e8f0;
  color: #64748b;
}

:global(body.light) .mode-tab-btn:hover {
  background: #f8fafc;
  color: #0f172a;
}

:global(body.light) .mode-tab-btn.active {
  background: #eef2ff;
  border-color: #c7d2fe;
  color: #4338ca;
  box-shadow: 0 2px 6px rgba(79, 70, 229, 0.08);
}

:global(body.light) .tab-badge-num {
  background: rgba(0, 0, 0, 0.06);
  color: #334155;
}

:global(body.light) .mode-tab-btn.active .tab-badge-num {
  background: #4338ca;
  color: #ffffff;
}

:global(body.light) .status-tab-btn {
  color: #64748b;
}

:global(body.light) .status-tab-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}

:global(body.light) .status-tab-btn.active {
  background: #4f46e5;
  color: #ffffff;
  box-shadow: 0 2px 8px rgba(79, 70, 229, 0.25);
}

:global(body.light) .search-field {
  background: #ffffff !important;
  border-color: #cbd5e1 !important;
  color: #0f172a !important;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04) !important;
}

:global(body.light) .search-field::placeholder {
  color: #94a3b8 !important;
}

:global(body.light) .search-field:focus {
  border-color: #4f46e5 !important;
  box-shadow: 0 0 0 3px rgba(79, 70, 229, 0.12) !important;
}

:global(body.light) .data-table th {
  background: #f8fafc !important;
  color: #475569 !important;
  border-bottom: 2px solid #e2e8f0 !important;
}

:global(body.light) .data-row:hover {
  background-color: #f8fafc;
}

:global(body.light) .data-row.is-expanded {
  background-color: #eff6ff;
}

:global(body.light) .method-tag {
  background: #f1f5f9;
  color: #475569;
}

:global(body.light) .model-pill {
  background: #eef2ff;
  border-color: #c7d2fe;
  color: #4338ca;
}

:global(body.light) .inspector-row td {
  background: #f8fafc;
}

:global(body.light) .inspector-panel {
  background: #ffffff;
  border-color: #cbd5e1;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}

:global(body.light) .inspector-tabs {
  background: #f1f5f9;
  border-bottom: 1px solid #e2e8f0;
}

:global(body.light) .inspector-tab {
  color: #64748b;
}

:global(body.light) .inspector-tab:hover {
  color: #0f172a;
}

:global(body.light) .inspector-tab.active {
  color: #4338ca;
  background: #ffffff;
  border-bottom-color: #4f46e5;
}

:global(body.light) .code-viewer {
  background: #ffffff;
  border-color: #e2e8f0;
  color: #0f172a;
}

:global(body.light) .headers-list-grid {
  background: #ffffff;
  border-color: #e2e8f0;
}

:global(body.light) .h-key {
  color: #4f46e5;
}

:global(body.light) .h-val {
  color: #334155;
}

:global(body.light) .error-diag-card {
  background: #fff1f2;
  border-color: #fecdd3;
}

:global(body.light) .diag-content {
  color: #b91c1c;
}

:global(body.light) .terminal-screen {
  background: #ffffff;
  border-color: #e2e8f0;
  color: #334155;
}

:global(body.light) .t-time {
  color: #64748b;
}

:global(body.light) .t-cat {
  color: #4f46e5;
}

:global(body.light) .t-msg {
  color: #0f172a;
}

:global(body.light) .level-error .t-msg {
  color: #b91c1c;
}

:global(body.light) .t-exc {
  background: #fff1f2;
  border-color: #e11d48;
  color: #9f1239;
}

:global(body.light) .modal-overlay {
  background: rgba(15, 23, 42, 0.45);
}

:global(body.light) .modal-container {
  background: #ffffff;
  border-color: #e2e8f0;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
}

:global(body.light) .slider {
  background-color: #cbd5e1;
}

:global(body.light) .slider:before {
  background-color: #ffffff;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
}

:global(body.light) .sticky-thead th {
  background: #f8fafc !important;
  box-shadow: 0 1px 0 #e2e8f0;
}

:global(body.light) .scroll-load-indicator {
  background: #f8fafc;
  border-color: #e2e8f0;
}

:global(body.light) .scroll-footer-bar {
  background: #ffffff;
  border-color: #e2e8f0;
}
</style>
