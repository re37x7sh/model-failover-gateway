<template>
  <div class="app-layout">
    <Header 
      :current-tab="currentTab" 
      @update:current-tab="currentTab = $event" 
      @open-settings="showSettingsModal = true"
    />

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

async function loadAllData() {
  await Promise.all([loadChannels(), loadLogsAndSummary()]);
}

onMounted(() => {
  loadAllData();
  // 每 5 秒静默同步一次概览数据
  setInterval(() => {
    loadLogsAndSummary();
  }, 5000);
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
