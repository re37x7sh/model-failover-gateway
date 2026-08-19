<template>
  <transition name="toast-fade">
    <div v-if="visible" :class="['toast-notification', `toast-${type}`]">
      <span class="toast-icon">
        <span v-if="type === 'success'">✅</span>
        <span v-else-if="type === 'error'">❌</span>
        <span v-else-if="type === 'warning'">⚠️</span>
        <span v-else>ℹ️</span>
      </span>
      <span class="toast-message">{{ message }}</span>
    </div>
  </transition>
</template>

<script setup>
import { ref } from 'vue';

const visible = ref(false);
const message = ref('');
const type = ref('info');
let timer = null;

function show(msg, toastType = 'info', duration = 3000) {
  message.value = msg;
  type.value = toastType;
  visible.value = true;
  if (timer) clearTimeout(timer);
  timer = setTimeout(() => {
    visible.value = false;
  }, duration);
}

defineExpose({ show });
</script>

<style scoped>
.toast-notification {
  position: fixed;
  bottom: 24px;
  right: 24px;
  z-index: 9999;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 20px;
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: 500;
  backdrop-filter: blur(16px);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--border-subtle);
  max-width: 420px;
}

.toast-success { background: rgba(16, 185, 129, 0.9); color: #ffffff; }
.toast-error { background: rgba(239, 68, 68, 0.9); color: #ffffff; }
.toast-warning { background: rgba(245, 158, 11, 0.9); color: #ffffff; }
.toast-info { background: rgba(99, 102, 241, 0.9); color: #ffffff; }

.toast-fade-enter-active,
.toast-fade-leave-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.toast-fade-enter-from,
.toast-fade-leave-to {
  opacity: 0;
  transform: translateY(16px) scale(0.95);
}
</style>
