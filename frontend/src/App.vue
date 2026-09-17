<template>
  <div v-if="isPetStandalone" class="pet-standalone-canvas">
    <DesktopPet :is-standalone="true" @open-settings="openFullDashboard" />
  </div>
  <div v-else class="app-shell">
    <!-- 1. 左侧常驻高质感侧边栏 (支持折叠) -->
    <Sidebar 
      :current-tab="currentTab" 
      :alerts="alerts"
      :collapsed="isSidebarCollapsed"
      @update:current-tab="currentTab = $event" 
      @update:collapsed="isSidebarCollapsed = $event"
      @open-settings="showSettingsModal = true"
      @dismiss-alert="handleDismissAlert"
      @clear-alerts="handleClearAllAlerts"
    />

    <!-- 2. 右侧主工作视口 -->
    <div class="main-viewport">
      <!-- 顶部轻量 Topbar 状态栏 -->
      <header class="top-nav-bar">
        <div class="top-nav-left">
          <button 
            class="topbar-collapse-btn" 
            @click="isSidebarCollapsed = !isSidebarCollapsed" 
            :title="isSidebarCollapsed ? '展开侧边栏 (Ctrl+B)' : '折叠侧边栏 (Ctrl+B)'"
          >
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="3" y="3" width="18" height="18" rx="2" />
              <path d="M9 3v18" />
              <path v-if="!isSidebarCollapsed" d="m16 15-3-3 3-3" />
              <path v-else d="m14 9 3 3-3 3" />
            </svg>
          </button>
          <span class="view-tag">{{ getViewCategory(currentTab) }}</span>
          <span class="nav-slash">/</span>
          <h1 class="view-title">{{ getViewTitle(currentTab) }}</h1>
        </div>

        <div class="top-nav-right">
          <div class="status-summary-pill" v-if="summary.totalRequests > 0">
            <span class="pill-dot"></span>
            <span class="pill-label">总调用:</span>
            <span class="pill-val font-mono">{{ summary.totalRequests }}</span>
            <span class="pill-divider"></span>
            <span class="pill-label">成功率:</span>
            <span class="pill-val text-success font-mono">{{ summary.successRate }}%</span>
          </div>

          <div v-if="summary.currentPrimaryChannelName" class="primary-channel-pill" :title="'当前主用渠道: ' + summary.currentPrimaryChannelName">
            <span class="pill-icon">⚡</span>
            <span class="pill-channel-name truncate-text">{{ summary.currentPrimaryChannelName }}</span>
          </div>
        </div>
      </header>

      <!-- ⚠️ 渠道异常全局告警横幅（可手动关闭） -->
      <transition name="banner-slide">
        <div v-if="alerts.length > 0" class="alert-banner">
          <div class="alert-banner-inner">
            <div class="alert-banner-content">
              <span class="alert-icon">⚠️</span>
              <span class="alert-title">渠道异常警告：</span>
              <span class="alert-channel font-mono">[{{ alerts[0].channelName }}]</span>
              <span class="alert-reason">{{ alerts[0].reason }}</span>
              <span v-if="alerts[0].occurCount > 1" class="badge badge-warning">发生 {{ alerts[0].occurCount }} 次</span>
              <span class="alert-hint">（网关已自动无缝切换备用通道）</span>
            </div>
            <div class="alert-banner-actions">
              <button class="btn btn-xs btn-secondary" @click="handleDismissAlert(alerts[0].id)">✕ 忽略</button>
              <button v-if="alerts.length > 1" class="btn btn-xs btn-danger" @click="handleClearAllAlerts">全部清除 ({{ alerts.length }})</button>
            </div>
          </div>
        </div>
      </transition>

      <!-- 核心页面视图挂载区 -->
      <main class="content-container">
        <transition name="view-fade" mode="out-in">
          <DashboardView 
            v-if="currentTab === 'dashboard'" 
            :summary="summary" 
            :recent-logs="logs"
            @navigate="currentTab = $event"
          />

          <ChannelsView 
            v-else-if="currentTab === 'channels'" 
            :channels="channels"
            @refresh="loadAllData"
            @toast="showToast"
          />

          <LogsView 
            v-else-if="currentTab === 'logs'" 
            :logs="logs"
            @refresh="loadLogsAndSummary"
            @toast="showToast"
          />

          <TokenStatsView 
            v-else-if="currentTab === 'tokens'" 
            @toast="showToast"
          />

          <PlaygroundView 
            v-else-if="currentTab === 'playground'" 
            @toast="showToast"
          />

          <GuideView 
            v-else-if="currentTab === 'guide'" 
            @toast="showToast"
            @open-settings="showSettingsModal = true"
          />
        </transition>
      </main>
    </div>

    <!-- 系统设置与一键接管弹窗 -->
    <SettingsModal 
      v-model="showSettingsModal" 
      @toast="showToast"
      @refresh="loadAllData"
    />

    <Toast ref="toastRef" />

    <!-- 🐾 灵动桌面悬浮萌宠 -->
    <DesktopPet @open-settings="showSettingsModal = true" />
  </div>
</template>

<script setup>
import { ref, reactive, watch, onMounted } from 'vue';
import Sidebar from './components/Sidebar.vue';
import Toast from './components/Toast.vue';
import SettingsModal from './components/SettingsModal.vue';
import DesktopPet from './components/DesktopPet.vue';
import DashboardView from './views/DashboardView.vue';
import ChannelsView from './views/ChannelsView.vue';
import PlaygroundView from './views/PlaygroundView.vue';
import TokenStatsView from './views/TokenStatsView.vue';
import GuideView from './views/GuideView.vue';
import LogsView from './views/LogsView.vue';
import { api } from './api';

const isPetStandalone = ref(
  window.location.pathname.includes('/pet') || 
  window.location.search.includes('mode=pet')
);

function openFullDashboard() {
  window.open('/', '_blank');
}

const currentTab = ref('dashboard');
const showSettingsModal = ref(false);
const toastRef = ref(null);

// 侧边栏折叠状态持久化
const isSidebarCollapsed = ref(localStorage.getItem('sidebar_collapsed') === 'true');
watch(isSidebarCollapsed, (val) => {
  localStorage.setItem('sidebar_collapsed', val ? 'true' : 'false');
});

const channels = ref([]);
const logs = ref([]);
const alerts = ref([]);

const summary = reactive({
  totalChannels: 0,
  activeChannels: 0,
  totalRequests: 0,
  totalFailovers: 0,
  successfulRequests: 0,
  failedRequests: 0,
  successRate: 100,
  currentPrimaryChannelName: ''
});

function getViewCategory(tab) {
  switch (tab) {
    case 'dashboard':
    case 'channels':
    case 'logs':
    case 'tokens':
      return '网关路由与监控';
    case 'playground':
    case 'guide':
      return '开发与集成测试';
    default:
      return '控制台';
  }
}

function getViewTitle(tab) {
  switch (tab) {
    case 'dashboard': return '仪表盘概览';
    case 'channels': return '模型渠道与调度管理';
    case 'logs': return '实时请求与全链路追踪';
    case 'tokens': return 'Token 消耗与 Prompt 缓存分析';
    case 'playground': return '内置 Web 调试沙箱';
    case 'guide': return 'IDE 客户端无缝接管指引';
    default: return '模型故障转移网关';
  }
}

function showToast(msg, type = 'info') {
  toastRef.value?.show(msg, type);
}

async function loadChannels() {
  try {
    const list = await api.getChannels();
    channels.value = list || [];
  } catch (err) {
    console.error('加载渠道列表失败:', err);
  }
}

async function loadLogsAndSummary() {
  try {
    const [logsData, summaryData] = await Promise.all([
      api.getLogs(100),
      api.getSummary()
    ]);
    logs.value = logsData || [];
    if (summaryData) {
      Object.assign(summary, summaryData);
    }
  } catch (err) {
    console.error('加载日志或概览失败:', err);
  }
}

async function loadAlerts() {
  try {
    const res = await api.getNotifications();
    alerts.value = res || [];
  } catch (err) { }
}

async function handleDismissAlert(id) {
  try {
    await api.dismissNotification(id);
    alerts.value = alerts.value.filter(a => a.id !== id);
    showToast('已关闭该条异常通知', 'info');
  } catch (err) {
    showToast(`操作失败: ${err.message}`, 'error');
  }
}

async function handleClearAllAlerts() {
  try {
    await api.clearAllNotifications();
    alerts.value = [];
    showToast('已清空所有异常通知', 'success');
  } catch (err) {
    showToast(`操作失败: ${err.message}`, 'error');
  }
}

async function loadAllData() {
  await Promise.all([loadChannels(), loadLogsAndSummary(), loadAlerts()]);
}

onMounted(() => {
  loadAllData();
  setInterval(() => {
    loadLogsAndSummary();
    loadAlerts();
  }, 3000);
});
</script>

<style scoped>
/* 顶部 Topbar 风格 */
.top-nav-bar {
  height: 58px;
  min-height: 58px;
  background: var(--bg-sidebar);
  border-bottom: 1px solid var(--border-subtle);
  padding: 0 32px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  backdrop-filter: blur(12px);
  position: sticky;
  top: 0;
  z-index: 30;
}

.top-nav-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.topbar-collapse-btn {
  background: transparent;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-dim);
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.16s ease;
  margin-right: 4px;
}

.topbar-collapse-btn:hover {
  background: var(--bg-card-hover);
  color: var(--text-main);
  border-color: var(--border-medium);
}

.view-tag {
  font-size: 12px;
  color: var(--text-dim);
  font-weight: 500;
}

.nav-slash {
  color: var(--border-medium);
  font-size: 13px;
}

.view-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-main);
  letter-spacing: -0.2px;
}

.top-nav-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.status-summary-pill {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  padding: 4px 12px;
  border-radius: var(--radius-full);
  font-size: 12px;
}

.pill-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--success);
  box-shadow: 0 0 6px var(--success);
}

.pill-label {
  color: var(--text-dim);
}

.pill-val {
  color: var(--text-main);
  font-weight: 600;
}

.pill-divider {
  width: 1px;
  height: 10px;
  background: var(--border-medium);
}

.primary-channel-pill {
  display: flex;
  align-items: center;
  gap: 6px;
  background: rgba(99, 102, 241, 0.1);
  border: 1px solid rgba(99, 102, 241, 0.25);
  padding: 4px 10px;
  border-radius: var(--radius-full);
  font-size: 12px;
  max-width: 200px;
}

.pill-channel-name {
  color: #a5b4fc;
  font-weight: 500;
}

/* 告警横幅 */
.alert-banner {
  background: linear-gradient(90deg, rgba(244, 63, 94, 0.15), rgba(245, 158, 11, 0.15));
  border-bottom: 1px solid rgba(244, 63, 94, 0.3);
  padding: 8px 32px;
}

.alert-banner-inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  max-width: 1600px;
  margin: 0 auto;
}

.alert-banner-content {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  flex-wrap: wrap;
}

.alert-title {
  font-weight: 700;
  color: var(--danger);
}

.alert-channel {
  color: var(--accent-primary);
  font-weight: 600;
}

.alert-reason {
  color: var(--text-main);
}

.alert-hint {
  color: var(--text-dim);
}

.alert-banner-actions {
  display: flex;
  gap: 6px;
}

/* 视图转场 */
.view-fade-enter-active,
.view-fade-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}

.view-fade-enter-from {
  opacity: 0;
  transform: translateY(4px);
}

.view-fade-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

.pet-standalone-canvas {
  width: 100vw;
  height: 100vh;
  overflow: hidden;
  background: transparent;
}

/* ================= 浅色模式专属 Topbar 精修 ================= */
:global(body.light) .top-nav-bar {
  background: rgba(255, 255, 255, 0.88);
  border-color: #e2e8f0;
}

:global(body.light) .status-summary-pill {
  background: #ffffff;
  border-color: #cbd5e1;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

:global(body.light) .primary-channel-pill {
  background: #eef2ff;
  border-color: #c7d2fe;
}

:global(body.light) .pill-channel-name {
  color: #4338ca;
  font-weight: 600;
}

:global(body.light) .topbar-collapse-btn {
  color: #64748b;
  border-color: #cbd5e1;
}

:global(body.light) .topbar-collapse-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}
</style>
