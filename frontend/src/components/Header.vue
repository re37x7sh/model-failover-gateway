<template>
  <header class="app-header">
    <div class="header-container">
      <div class="brand">
        <div class="logo-icon">
          <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon>
          </svg>
        </div>
        <div class="brand-text">
          <span class="brand-title">Model Failover Gateway</span>
          <span class="brand-badge">Local Proxy 5000</span>
        </div>
      </div>

      <nav class="nav-links">
        <button 
          v-for="tab in tabs" 
          :key="tab.id"
          :class="['nav-btn', { active: currentTab === tab.id }]"
          @click="$emit('update:currentTab', tab.id)"
        >
          <span class="tab-icon">{{ tab.icon }}</span>
          <span class="tab-label">{{ tab.label }}</span>
        </button>
      </nav>

      <div class="header-actions">
        <!-- 🔔 渠道异常告警通知中心 -->
        <div class="notification-wrapper">
          <button 
            :class="['bell-btn', { 'has-alerts': alerts.length > 0, active: showNotificationDropdown }]" 
            @click="showNotificationDropdown = !showNotificationDropdown"
            :title="t.alerts.title"
          >
            <span>🔔</span>
            <span v-if="alerts.length > 0" class="bell-badge">{{ alerts.length }}</span>
          </button>

          <!-- 浮动通知下拉面板 -->
          <div v-if="showNotificationDropdown" class="notification-dropdown glass-card">
            <div class="notif-header">
              <div class="notif-title-row">
                <span class="notif-title">⚠️ {{ t.alerts.title }}</span>
                <span v-if="alerts.length > 0" class="badge badge-danger">{{ alerts.length }}</span>
              </div>
              <button 
                v-if="alerts.length > 0" 
                class="notif-clear-btn" 
                @click="$emit('clearAlerts')"
              >
                {{ t.alerts.clearAll }}
              </button>
            </div>

            <div v-if="alerts.length === 0" class="notif-empty">
              <span class="empty-icon">🎉</span>
              <span>{{ t.alerts.empty }}</span>
            </div>

            <div v-else class="notif-list">
              <div v-for="item in alerts" :key="item.id" class="notif-item">
                <div class="notif-item-top">
                  <span class="notif-channel">[{{ item.channelName }}]</span>
                  <span v-if="item.group && item.group !== 'all'" class="badge badge-info">{{ item.group }}</span>
                  <span class="notif-time">{{ formatTime(item.timestamp) }}</span>
                </div>
                <div class="notif-reason">{{ item.reason }}</div>
                <div class="notif-item-bottom">
                  <span v-if="item.occurCount > 1" class="badge badge-warning">
                    {{ t.alerts.occurredTimes }} {{ item.occurCount }} 次
                  </span>
                  <span v-else class="badge badge-muted">{{ t.alerts.autoSwitched }}</span>
                  <button 
                    class="notif-dismiss-btn" 
                    @click.stop="$emit('dismissAlert', item.id)"
                    :title="t.alerts.dismiss"
                  >
                    ✕ {{ t.alerts.dismiss }}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <button class="settings-btn" @click="$emit('openSettings')" :title="t.nav.settings">
          <span>⚡ {{ t.nav.settings }}</span>
        </button>

        <div class="gateway-status" :title="'http://127.0.0.1:5000'">
          <span class="status-dot active"></span>
          <span class="status-text">127.0.0.1</span>
        </div>

        <!-- 🌐 中英双语切换 -->
        <button class="lang-toggle-btn" @click="toggleLanguage" :title="lang === 'zh' ? 'Switch to English' : '切换为简体中文'">
          <span>{{ lang === 'zh' ? '🇨🇳 中文' : '🇬🇧 EN' }}</span>
        </button>

        <button class="theme-toggle-btn" @click="toggleTheme" :title="isDark ? '切换浅色模式' : '切换深色模式'">
          <span v-if="isDark">☀️</span>
          <span v-else>🌙</span>
        </button>
      </div>
    </div>
  </header>
</template>

<script setup>
import { ref, computed } from 'vue';
import { useI18n } from '../i18n';

const { lang, t, toggleLanguage } = useI18n();

defineProps({
  currentTab: {
    type: String,
    required: true
  },
  alerts: {
    type: Array,
    default: () => []
  }
});

defineEmits(['update:currentTab', 'openSettings', 'dismissAlert', 'clearAlerts']);

const tabs = computed(() => [
  { id: 'dashboard', label: t.value.nav.dashboard, icon: '📊' },
  { id: 'channels', label: t.value.nav.channels, icon: '⚡' },
  { id: 'tokens', label: t.value.nav.tokens, icon: '💎' },
  { id: 'logs', label: t.value.nav.logs, icon: '📜' },
  { id: 'guide', label: t.value.nav.guide, icon: '🚀' }
]);

const isDark = ref(true);
const showNotificationDropdown = ref(false);

function formatTime(isoStr) {
  if (!isoStr) return '';
  const d = new Date(isoStr);
  return d.toTimeString().split(' ')[0];
}

function toggleTheme() {
  isDark.value = !isDark.value;
  if (isDark.value) {
    document.body.classList.remove('light');
    document.body.classList.add('dark');
  } else {
    document.body.classList.remove('dark');
    document.body.classList.add('light');
  }
}
</script>

<style scoped>
.app-header {
  position: sticky;
  top: 0;
  z-index: 100;
  background: var(--bg-glass);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border-bottom: 1px solid var(--border-subtle);
  padding: 12px 24px;
}

.header-container {
  max-width: 1440px;
  margin: 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  text-decoration: none;
  cursor: default;
}

.logo-icon {
  width: 38px;
  height: 38px;
  border-radius: var(--radius-md);
  background: linear-gradient(135deg, var(--accent-primary), var(--accent-secondary));
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  box-shadow: 0 4px 12px var(--accent-glow);
}

.brand-text {
  display: flex;
  flex-direction: column;
}

.brand-title {
  font-size: 16px;
  font-weight: 700;
  letter-spacing: -0.3px;
  background: linear-gradient(to right, #ffffff, #94a3b8);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.brand-badge {
  font-size: 10px;
  font-family: var(--font-mono);
  color: var(--accent-primary);
  font-weight: 600;
}

.nav-links {
  display: flex;
  align-items: center;
  gap: 6px;
  background: rgba(0, 0, 0, 0.15);
  padding: 4px;
  border-radius: var(--radius-lg);
  border: 1px solid var(--border-subtle);
}

.nav-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 14px;
  font-weight: 500;
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all 0.2s ease;
}

.nav-btn:hover {
  color: var(--text-main);
  background: rgba(255, 255, 255, 0.05);
}

.nav-btn.active {
  color: #ffffff;
  background: var(--accent-primary);
  box-shadow: 0 2px 8px var(--accent-glow);
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.gateway-status {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border-radius: var(--radius-full);
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  font-size: 12px;
  font-family: var(--font-mono);
  color: var(--text-muted);
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-dot.active {
  background: var(--success);
  box-shadow: 0 0 8px var(--success);
}

.settings-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.2), rgba(168, 85, 247, 0.2));
  border: 1px solid var(--border-active);
  border-radius: var(--radius-md);
  color: var(--accent-primary);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.settings-btn:hover {
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.35), rgba(168, 85, 247, 0.35));
  box-shadow: 0 0 12px var(--accent-glow);
  transform: translateY(-1px);
}

.theme-toggle-btn,
.lang-toggle-btn {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-subtle);
  color: var(--text-main);
  padding: 6px 12px;
  border-radius: var(--radius-md);
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  transition: all 0.2s ease;
}

.theme-toggle-btn:hover,
.lang-toggle-btn:hover {
  background: var(--bg-hover);
  border-color: var(--border-focus);
}

/* 🔔 告警通知中心样式 */
.notification-wrapper {
  position: relative;
}

.bell-btn {
  position: relative;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-subtle);
  color: var(--text-main);
  padding: 6px 10px;
  border-radius: var(--radius-md);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  transition: all 0.2s ease;
}

.bell-btn:hover,
.bell-btn.active {
  background: var(--bg-hover);
  border-color: var(--border-focus);
}

.bell-btn.has-alerts {
  border-color: var(--warning);
  color: var(--warning);
}

.bell-badge {
  position: absolute;
  top: -4px;
  right: -4px;
  background: var(--danger);
  color: #fff;
  font-size: 10px;
  font-weight: 800;
  padding: 1px 5px;
  border-radius: 10px;
  min-width: 16px;
  text-align: center;
  box-shadow: 0 0 6px rgba(239, 68, 68, 0.6);
  animation: badge-pulse 2s infinite;
}

@keyframes badge-pulse {
  0% { transform: scale(1); }
  50% { transform: scale(1.15); }
  100% { transform: scale(1); }
}

.notification-dropdown {
  position: absolute;
  top: calc(100% + 10px);
  right: 0;
  width: 360px;
  max-height: 420px;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  z-index: 200;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  animation: dropdown-in 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes dropdown-in {
  from { opacity: 0; transform: translateY(-8px); }
  to { opacity: 1; transform: translateY(0); }
}

.notif-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-subtle);
  background: rgba(255, 255, 255, 0.02);
}

.notif-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.notif-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-main);
}

.notif-clear-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 11px;
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 4px;
  transition: all 0.15s;
}

.notif-clear-btn:hover {
  color: var(--danger);
  background: var(--danger-bg);
}

.notif-empty {
  padding: 28px 16px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  color: var(--text-muted);
  font-size: 12px;
}

.empty-icon {
  font-size: 24px;
}

.notif-list {
  overflow-y: auto;
  max-height: 340px;
  display: flex;
  flex-direction: column;
}

.notif-item {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-subtle);
  display: flex;
  flex-direction: column;
  gap: 6px;
  transition: background 0.15s;
}

.notif-item:last-child {
  border-bottom: none;
}

.notif-item:hover {
  background: rgba(255, 255, 255, 0.02);
}

.notif-item-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
}

.notif-channel {
  font-weight: 700;
  font-size: 13px;
  color: var(--warning);
}

.notif-time {
  font-size: 11px;
  font-family: var(--font-mono);
  color: var(--text-dim);
  margin-left: auto;
}

.notif-reason {
  font-size: 12px;
  color: var(--text-main);
  line-height: 1.4;
  word-break: break-all;
}

.notif-item-bottom {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 4px;
}

.notif-dismiss-btn {
  background: transparent;
  border: 1px solid var(--border-subtle);
  color: var(--text-muted);
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.15s;
}

.notif-dismiss-btn:hover {
  background: var(--danger-bg);
  border-color: var(--danger);
  color: var(--danger);
}
</style>
