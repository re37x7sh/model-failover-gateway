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
  }
});

defineEmits(['update:currentTab', 'openSettings']);

const tabs = computed(() => [
  { id: 'dashboard', label: t.value.nav.dashboard, icon: '📊' },
  { id: 'channels', label: t.value.nav.channels, icon: '⚡' },
  { id: 'tokens', label: t.value.nav.tokens, icon: '💎' },
  { id: 'logs', label: t.value.nav.logs, icon: '📜' },
  { id: 'guide', label: t.value.nav.guide, icon: '🚀' }
]);

const isDark = ref(true);

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
  max-width: 1280px;
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
}

.logo-icon {
  width: 36px;
  height: 36px;
  border-radius: 10px;
  background: linear-gradient(135deg, #6366f1, #a855f7);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  box-shadow: 0 4px 12px rgba(99, 102, 241, 0.4);
}

.brand-text {
  display: flex;
  flex-direction: column;
}

.brand-title {
  font-size: 16px;
  font-weight: 700;
  letter-spacing: -0.02em;
  background: linear-gradient(135deg, var(--text-main), var(--text-muted));
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.brand-badge {
  font-size: 11px;
  color: var(--accent-primary);
  font-family: var(--font-mono);
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
</style>
