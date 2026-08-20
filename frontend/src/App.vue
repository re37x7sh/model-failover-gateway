<template>
  <div class="app-layout">
    <Header 
      :current-tab="currentTab" 
      :alerts="alerts"
      @update:current-tab="currentTab = $event" 
      @open-settings="showSettingsModal = true"
      @dismiss-alert="handleDismissAlert"
      @clear-alerts="handleClearAllAlerts"
    />

    <!-- ⚠️ 渠道异常全局告警横幅（可手动关闭） -->
    <transition name="banner-slide">
      <div v-if="alerts.length > 0" class="alert-top-bar">
        <div class="alert-top-container">
          <div class="alert-content">
            <span class="alert-icon">⚠️</span>
            <span class="alert-title">渠道异常告警：</span>
            <span class="alert-channel">[{{ alerts[0].channelName }}]</span>
            <span class="alert-reason">{{ alerts[0].reason }}</span>
            <span v-if="alerts[0].occurCount > 1" class="badge badge-warning">发生 {{ alerts[0].occurCount }} 次</span>
            <span class="alert-tip">（网关已自动触发智能故障转移）</span>
          </div>
          <div class="alert-actions">
            <button class="alert-dismiss-btn" @click="handleDismissAlert(alerts[0].id)" title="手动关闭此通知">
              ✕ 关闭通知
            </button>
            <button v-if="alerts.length > 1" class="alert-clear-all-btn" @click="handleClearAllAlerts" title="清空全部异常通知">
              全部忽略 ({{ alerts.length }})
            </button>
          </div>
        </div>
      </div>
    </transition>

    <main class="main-content">
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

        <TokenStatsView 
          v-else-if="currentTab === 'tokens'" 
          @toast="showToast"
        />

        <LogsView 
          v-else-if="currentTab === 'logs'" 
          :logs="logs"
          @refresh="loadLogsAndSummary"
          @toast="showToast"
        />

        <GuideView 
          v-else-if="currentTab === 'guide'" 
          @toast="showToast"
          @open-settings="showSettingsModal = true"
        />
      </transition>
    </main>

    <!-- 系统设置与一键接管弹窗 -->
    <SettingsModal 
      v-model="showSettingsModal" 
      @toast="showToast"
      @refresh="loadAllData"
    />

    <Toast ref="toastRef" />
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue';
import Header from './components/Header.vue';
import Toast from './components/Toast.vue';
import SettingsModal from './components/SettingsModal.vue';
import DashboardView from './views/DashboardView.vue';
import ChannelsView from './views/ChannelsView.vue';
import TokenStatsView from './views/TokenStatsView.vue';
import LogsView from './views/LogsView.vue';
import GuideView from './views/GuideView.vue';
import { api } from './api';

const currentTab = ref('dashboard');
const showSettingsModal = ref(false);
const toastRef = ref(null);

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
  } catch (err) {
    // 静默忽略轮询错误
  }
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
  // 每 3 秒静默同步一次告警与概览数据
  setInterval(() => {
    loadLogsAndSummary();
    loadAlerts();
  }, 3000);
});
</script>

<style scoped>
.app-layout {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.main-content {
  flex: 1;
  max-width: 1280px;
  width: 100%;
  margin: 0 auto;
  padding: 24px;
}

/* ⚠️ 全局顶部告警横幅样式 */
.alert-top-bar {
  background: linear-gradient(90deg, rgba(239, 68, 68, 0.2), rgba(245, 158, 11, 0.2));
  border-bottom: 1px solid rgba(239, 68, 68, 0.35);
  backdrop-filter: blur(8px);
  padding: 10px 24px;
  position: relative;
  z-index: 90;
}

.alert-top-container {
  max-width: 1440px;
  margin: 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.alert-content {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-main);
  flex-wrap: wrap;
}

.alert-icon {
  font-size: 16px;
  line-height: 1;
}

.alert-title {
  font-weight: 700;
  color: var(--danger);
}

.alert-channel {
  font-weight: 600;
  color: var(--warning);
}

.alert-reason {
  color: var(--text-main);
}

.alert-tip {
  font-size: 12px;
  color: var(--text-dim);
}

.alert-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.alert-dismiss-btn,
.alert-clear-all-btn {
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid rgba(255, 255, 255, 0.15);
  color: var(--text-main);
  padding: 4px 10px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s;
  white-space: nowrap;
}

.alert-dismiss-btn:hover {
  background: var(--danger);
  border-color: var(--danger);
  color: #fff;
}

.alert-clear-all-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.banner-slide-enter-active,
.banner-slide-leave-active {
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}

.banner-slide-enter-from,
.banner-slide-leave-to {
  opacity: 0;
  transform: translateY(-100%);
}

.view-fade-enter-active,
.view-fade-leave-active {
  transition: opacity 0.2s cubic-bezier(0.4, 0, 0.2, 1), transform 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.view-fade-enter-from {
  opacity: 0;
  transform: translateY(8px);
}

.view-fade-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}
</style>
