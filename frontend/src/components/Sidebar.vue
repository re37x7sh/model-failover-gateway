<template>
  <aside :class="['app-sidebar', { 'is-collapsed': isCollapsed }]">
    <!-- 1. 品牌与运行状态及折叠按钮 -->
    <div class="sidebar-brand">
      <div class="brand-left">
        <div class="brand-logo" :title="'Model Gateway - 127.0.0.1:5000'">
          <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon>
          </svg>
          <span v-if="isCollapsed" class="logo-pulse-dot"></span>
        </div>
        <div v-show="!isCollapsed" class="brand-info">
          <span class="brand-name">Model Gateway</span>
          <div class="brand-status">
            <span class="pulse-indicator pulse-green"></span>
            <span class="status-port font-mono">127.0.0.1:5000</span>
          </div>
        </div>
      </div>

      <!-- 折叠/展开控制按钮 -->
      <button 
        class="collapse-toggle-btn" 
        @click="toggleCollapse"
        :title="isCollapsed ? '展开侧边栏 (Ctrl+B)' : '折叠侧边栏 (Ctrl+B)'"
      >
        <svg v-if="!isCollapsed" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <rect x="3" y="3" width="18" height="18" rx="2" />
          <path d="M9 3v18" />
          <path d="m16 15-3-3 3-3" />
        </svg>
        <svg v-else viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <rect x="3" y="3" width="18" height="18" rx="2" />
          <path d="M9 3v18" />
          <path d="m14 9 3 3-3 3" />
        </svg>
      </button>
    </div>

    <!-- 2. 核心业务主导航 -->
    <nav class="sidebar-nav">
      <div v-if="!isCollapsed" class="nav-section-title">核心功能</div>
      <div v-else class="nav-section-divider"></div>

      <button 
        v-for="tab in mainTabs" 
        :key="tab.id"
        :class="['nav-item', { active: currentTab === tab.id }]"
        :title="isCollapsed ? tab.label : ''"
        @click="$emit('update:currentTab', tab.id)"
      >
        <span class="nav-icon" v-html="tab.svg"></span>
        <span v-show="!isCollapsed" class="nav-label">{{ tab.label }}</span>
        <span v-if="tab.badge && !isCollapsed" :class="['nav-badge', tab.badgeClass]">{{ tab.badge }}</span>
        <span v-if="tab.badge && isCollapsed" class="nav-badge-dot"></span>
      </button>

      <div v-if="!isCollapsed" class="nav-section-title" style="margin-top: 18px;">开发与调试</div>
      <div v-else class="nav-section-divider"></div>

      <button 
        v-for="tab in devTabs" 
        :key="tab.id"
        :class="['nav-item', { active: currentTab === tab.id }]"
        :title="isCollapsed ? tab.label : ''"
        @click="$emit('update:currentTab', tab.id)"
      >
        <span class="nav-icon" v-html="tab.svg"></span>
        <span v-show="!isCollapsed" class="nav-label">{{ tab.label }}</span>
      </button>
    </nav>

    <!-- 3. 底部工具栏与告警通知 -->
    <div class="sidebar-footer">
      <!-- 异常通知中心卡片 / 折叠后为紧凑徽章 -->
      <div 
        v-if="alerts.length > 0" 
        :class="['alert-notice-card', { 'is-compact': isCollapsed }]" 
        @click="showNotifDropdown = !showNotifDropdown"
        :title="isCollapsed ? `${alerts.length} 个渠道异常，点击查看` : ''"
      >
        <div class="alert-card-left">
          <span class="alert-icon">⚠️</span>
          <div v-show="!isCollapsed" class="alert-text">
            <span class="alert-bold">{{ alerts.length }} 个渠道异常</span>
            <span class="alert-sub">已自动切换备用</span>
          </div>
        </div>
        <span v-if="!isCollapsed" class="alert-arrow">›</span>
        <span v-else class="alert-compact-badge">{{ alerts.length }}</span>
      </div>

      <!-- 告警下拉/浮动面板 -->
      <transition name="pop-fade">
        <div v-if="showNotifDropdown" :class="['sidebar-popover', 'glass-card', { 'pop-floating': isCollapsed }]">
          <div class="pop-header">
            <span class="pop-title">⚠️ 故障转移异常通知</span>
            <button class="pop-clear" @click="$emit('clearAlerts')">清空</button>
          </div>
          <div class="pop-list">
            <div v-for="item in alerts" :key="item.id" class="pop-item">
              <div class="pop-item-top">
                <span class="pop-channel font-mono">[{{ item.channelName }}]</span>
                <button class="pop-dismiss" @click.stop="$emit('dismissAlert', item.id)">✕</button>
              </div>
              <div class="pop-reason">{{ item.reason }}</div>
            </div>
          </div>
        </div>
      </transition>

      <!-- 底部快捷功能区 -->
      <div :class="['footer-actions-grid', { 'is-compact': isCollapsed }]">
        <button class="action-btn" @click="$emit('openSettings')" :title="'全局设置与客户端接管'">
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="3"></circle>
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
          </svg>
          <span v-show="!isCollapsed">设置</span>
        </button>

        <button class="action-btn" @click="toggleLanguage" :title="lang === 'zh' ? 'Switch to English' : '切换为简体中文'">
          <span>{{ lang === 'zh' ? '🇨🇳' : '🇬🇧' }}</span>
          <span v-show="!isCollapsed">{{ lang === 'zh' ? '中' : 'EN' }}</span>
        </button>

        <button class="action-btn" @click="toggleTheme" :title="isDark ? '切换浅色模式' : '切换深色模式'">
          <span>{{ isDark ? '☀️' : '🌙' }}</span>
        </button>
      </div>
    </div>
  </aside>
</template>

<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import { useI18n } from '../i18n';

const { lang, t, toggleLanguage } = useI18n();

const props = defineProps({
  currentTab: {
    type: String,
    required: true
  },
  alerts: {
    type: Array,
    default: () => []
  },
  collapsed: {
    type: Boolean,
    default: undefined
  }
});

const emit = defineEmits(['update:currentTab', 'update:collapsed', 'openSettings', 'dismissAlert', 'clearAlerts']);

// 初始化侧边栏折叠状态：优先用 props，次选用 localStorage
const isCollapsed = ref(
  props.collapsed !== undefined 
    ? props.collapsed 
    : (localStorage.getItem('sidebar_collapsed') === 'true')
);

watch(() => props.collapsed, (val) => {
  if (val !== undefined && val !== isCollapsed.value) {
    isCollapsed.value = val;
  }
});

function toggleCollapse() {
  isCollapsed.value = !isCollapsed.value;
  localStorage.setItem('sidebar_collapsed', isCollapsed.value ? 'true' : 'false');
  emit('update:collapsed', isCollapsed.value);
}

// 快捷键 Ctrl+B 或 Cmd+B 快速折叠/展开侧边栏
function handleKeydown(e) {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'b') {
    e.preventDefault();
    toggleCollapse();
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeydown);
});

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown);
});

const showNotifDropdown = ref(false);
const isDark = ref(document.body.classList.contains('light') ? false : true);

function toggleTheme() {
  if (isDark.value) {
    document.body.classList.add('light');
    isDark.value = false;
  } else {
    document.body.classList.remove('light');
    isDark.value = true;
  }
}

// 核心主导航
const mainTabs = computed(() => [
  {
    id: 'dashboard',
    label: t.value.nav.dashboard,
    svg: `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="9" rx="1"></rect><rect x="14" y="3" width="7" height="5" rx="1"></rect><rect x="14" y="12" width="7" height="9" rx="1"></rect><rect x="3" y="16" width="7" height="5" rx="1"></rect></svg>`
  },
  {
    id: 'channels',
    label: t.value.nav.channels,
    svg: `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 11a9 9 0 0 1 9 9"></path><path d="M4 4a16 16 0 0 1 16 16"></path><circle cx="5" cy="19" r="1"></circle></svg>`
  },
  {
    id: 'logs',
    label: t.value.nav.logs,
    badge: '实时',
    badgeClass: 'badge-pending',
    svg: `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"><line x1="8" y1="6" x2="21" y2="6"></line><line x1="8" y1="12" x2="21" y2="12"></line><line x1="8" y1="18" x2="21" y2="18"></line><line x1="3" y1="6" x2="3.01" y2="6"></line><line x1="3" y1="12" x2="3.01" y2="12"></line><line x1="3" y1="18" x2="3.01" y2="18"></line></svg>`
  },
  {
    id: 'tokens',
    label: t.value.nav.tokens,
    svg: `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"></line><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path></svg>`
  }
]);

// 辅助与测试导航
const devTabs = computed(() => [
  {
    id: 'playground',
    label: t.value.nav.playground,
    svg: `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path><polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline><line x1="12" y1="22.08" x2="12" y2="12"></line></svg>`
  },
  {
    id: 'guide',
    label: t.value.nav.guide,
    svg: `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"></path></svg>`
  }
]);
</script>

<style scoped>
.app-sidebar {
  width: var(--sidebar-width);
  min-width: var(--sidebar-width);
  height: 100vh;
  background-color: var(--bg-sidebar);
  border-right: 1px solid var(--border-subtle);
  display: flex;
  flex-direction: column;
  user-select: none;
  z-index: 50;
  transition: width 0.24s cubic-bezier(0.4, 0, 0.2, 1), min-width 0.24s cubic-bezier(0.4, 0, 0.2, 1);
  overflow: hidden;
}

/* 折叠状态 */
.app-sidebar.is-collapsed {
  width: var(--sidebar-collapsed-width);
  min-width: var(--sidebar-collapsed-width);
}

/* 品牌区 */
.sidebar-brand {
  padding: 16px 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--border-subtle);
  min-height: 64px;
  box-sizing: border-box;
  gap: 8px;
}

.app-sidebar.is-collapsed .sidebar-brand {
  padding: 14px 6px;
  flex-direction: column;
  gap: 8px;
  justify-content: center;
}

.brand-left {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.app-sidebar.is-collapsed .brand-left {
  justify-content: center;
}

.brand-logo {
  width: 36px;
  height: 36px;
  border-radius: var(--radius-md);
  background: var(--accent-gradient);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  box-shadow: 0 0 16px var(--accent-glow);
  flex-shrink: 0;
  position: relative;
}

.logo-pulse-dot {
  position: absolute;
  bottom: -2px;
  right: -2px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--success);
  border: 2px solid var(--bg-sidebar);
  box-shadow: 0 0 6px var(--success);
}

.brand-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
  white-space: nowrap;
}

.brand-name {
  font-size: 14px;
  font-weight: 700;
  color: var(--text-main);
  letter-spacing: -0.3px;
}

.brand-status {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 2px;
}

.status-port {
  font-size: 11px;
  color: var(--text-dim);
}

/* 折叠按钮 */
.collapse-toggle-btn {
  background: transparent;
  border: 1px solid transparent;
  color: var(--text-dim);
  width: 28px;
  height: 28px;
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.16s ease;
  flex-shrink: 0;
}

.collapse-toggle-btn:hover {
  background: var(--bg-card-hover);
  color: var(--text-main);
  border-color: var(--border-subtle);
}

/* 导航项 */
.sidebar-nav {
  flex: 1;
  padding: 16px 10px;
  overflow-y: auto;
  overflow-x: hidden;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.app-sidebar.is-collapsed .sidebar-nav {
  padding: 12px 6px;
  align-items: center;
}

.nav-section-title {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  color: var(--text-dim);
  padding: 4px 10px 6px;
  letter-spacing: 0.5px;
  white-space: nowrap;
}

.nav-section-divider {
  width: 28px;
  height: 1px;
  background: var(--border-subtle);
  margin: 10px auto;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 9px 12px;
  border-radius: var(--radius-md);
  background: transparent;
  border: 1px solid transparent;
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.16s ease;
  width: 100%;
  text-align: left;
  white-space: nowrap;
  position: relative;
}

.app-sidebar.is-collapsed .nav-item {
  width: 44px;
  height: 44px;
  padding: 0;
  justify-content: center;
  margin: 0 auto;
}

.nav-item:hover {
  background: var(--bg-card-hover);
  color: var(--text-main);
}

.nav-item.active {
  background: rgba(99, 102, 241, 0.12);
  border-color: rgba(99, 102, 241, 0.3);
  color: #818cf8;
  font-weight: 600;
  box-shadow: inset 0 0 12px rgba(99, 102, 241, 0.08);
}

.nav-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

.nav-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
}

.nav-badge {
  font-size: 10px;
  padding: 1px 6px;
}

.nav-badge-dot {
  position: absolute;
  top: 8px;
  right: 8px;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--pending);
  box-shadow: 0 0 6px var(--pending);
}

/* 底部区域 */
.sidebar-footer {
  padding: 14px 10px;
  border-top: 1px solid var(--border-subtle);
  display: flex;
  flex-direction: column;
  gap: 10px;
  position: relative;
  box-sizing: border-box;
}

.app-sidebar.is-collapsed .sidebar-footer {
  padding: 12px 6px;
  align-items: center;
}

/* 告警通知卡 */
.alert-notice-card {
  background: var(--warning-bg);
  border: 1px solid var(--warning-border);
  border-radius: var(--radius-md);
  padding: 8px 10px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  cursor: pointer;
  transition: all 0.2s;
  white-space: nowrap;
}

.alert-notice-card.is-compact {
  width: 42px;
  height: 42px;
  padding: 0;
  justify-content: center;
  position: relative;
  margin: 0 auto;
}

.alert-notice-card:hover {
  filter: brightness(1.1);
  transform: translateY(-1px);
}

.alert-card-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.alert-icon {
  font-size: 14px;
}

.alert-text {
  display: flex;
  flex-direction: column;
}

.alert-bold {
  font-size: 11px;
  font-weight: 600;
  color: var(--warning);
}

.alert-sub {
  font-size: 10px;
  color: var(--text-dim);
}

.alert-arrow {
  color: var(--warning);
  font-size: 14px;
}

.alert-compact-badge {
  position: absolute;
  top: -4px;
  right: -4px;
  background: var(--danger);
  color: #fff;
  font-size: 10px;
  font-weight: 700;
  min-width: 16px;
  height: 16px;
  line-height: 16px;
  border-radius: 8px;
  text-align: center;
  padding: 0 3px;
}

/* 底部快捷按钮组 */
.footer-actions-grid {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 6px;
  width: 100%;
}

.footer-actions-grid.is-compact {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  width: 100%;
}

.action-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 7px 10px;
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  color: var(--text-muted);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.16s ease;
  white-space: nowrap;
}

.footer-actions-grid.is-compact .action-btn {
  width: 40px;
  height: 40px;
  padding: 0;
  justify-content: center;
}

.action-btn:hover {
  background: var(--bg-card-hover);
  color: var(--text-main);
  border-color: var(--border-medium);
}

/* 气泡弹窗 */
.sidebar-popover {
  position: absolute;
  bottom: 80px;
  left: 10px;
  right: 10px;
  padding: 12px;
  z-index: 100;
  max-height: 280px;
  overflow-y: auto;
}

.sidebar-popover.pop-floating {
  position: fixed;
  left: 74px;
  bottom: 24px;
  width: 320px;
  right: auto;
  z-index: 1000;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.6);
}

.pop-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  padding-bottom: 6px;
  border-bottom: 1px solid var(--border-subtle);
}

.pop-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--warning);
}

.pop-clear {
  background: none;
  border: none;
  color: var(--text-dim);
  font-size: 11px;
  cursor: pointer;
}

.pop-clear:hover {
  color: var(--text-main);
}

.pop-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.pop-item {
  background: rgba(0,0,0,0.25);
  padding: 6px 8px;
  border-radius: var(--radius-sm);
  font-size: 11px;
}

.pop-item-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2px;
}

.pop-channel {
  color: var(--accent-primary);
  font-weight: 600;
}

.pop-dismiss {
  background: none;
  border: none;
  color: var(--text-dim);
  cursor: pointer;
}

.pop-reason {
  color: var(--text-muted);
}
</style>
