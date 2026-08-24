<template>
  <div 
    v-if="settings.enableDesktopPet" 
    ref="petContainerRef"
    class="desktop-pet-container"
    :class="{ 
      'is-dragging': isDragging, 
      'is-minimized': isMinimized,
      'is-thinking': taskStatus.state === 'thinking',
      'is-completed': taskStatus.state === 'completed',
      'is-failover': taskStatus.state === 'failover'
    }"
    :style="{ left: position.x + 'px', top: position.y + 'px' }"
    @mousedown="handleMouseDown"
  >
    <!-- 💬 灵动气泡对话框 -->
    <transition name="bubble-pop">
      <div v-if="!isMinimized && currentBubbleText" class="pet-bubble glass-card">
        <div class="bubble-content">
          <span class="bubble-icon">{{ bubbleIcon }}</span>
          <span class="bubble-text">{{ displayBubbleText }}</span>
        </div>
        <button class="bubble-close-btn" @click.stop="currentBubbleText = ''">✕</button>
      </div>
    </transition>

    <!-- 🐾 悬浮快捷控制栏（鼠标悬停展开） -->
    <div class="pet-action-toolbar" v-if="!isMinimized">
      <button 
        class="pet-tool-btn" 
        :title="settings.enableSound ? '提示音：已开启 (点击静音)' : '提示音：已静音 (点击开启)'"
        @click.stop="toggleSound"
      >
        {{ settings.enableSound ? '🔊' : '🔇' }}
      </button>
      <button 
        class="pet-tool-btn" 
        title="切换宠物形象 (猫咪 / 机器人 / 柴犬)"
        @click.stop="switchAvatar"
      >
        {{ currentAvatarIcon }}
      </button>
      <button 
        class="pet-tool-btn" 
        title="🖥️ 弹出为独立桌面悬浮窗 (置顶/画中画，无需常驻主网页)"
        @click.stop="openPipOrWindow"
      >
        🖥️
      </button>
      <button 
        class="pet-tool-btn" 
        title="打开提醒设置"
        @click.stop="$emit('openSettings')"
      >
        ⚙️
      </button>
      <button 
        class="pet-tool-btn" 
        title="最小化收起"
        @click.stop="isMinimized = true"
      >
        ➖
      </button>
    </div>

    <!-- 最小化恢复按钮 -->
    <div v-if="isMinimized" class="pet-minimized-icon" @click="isMinimized = false" title="展开桌面宠物">
      <span class="mini-avatar">{{ currentAvatarIcon }}</span>
      <span v-if="taskStatus.state === 'thinking'" class="mini-pulse-dot"></span>
    </div>

    <!-- 🐱 主体动画展示区 -->
    <div v-else class="pet-body-wrapper" @click="handlePetClick">
      <!-- 实时计时器徽章 (思考生成时展示) -->
      <div v-if="taskStatus.state === 'thinking'" class="timer-badge">
        <span class="timer-icon">⚡</span>
        <span class="timer-text">{{ thinkingSeconds }}s</span>
      </div>

      <!-- 撒花/彩带粒子特效 (完成时绽放) -->
      <div v-if="taskStatus.state === 'completed'" class="confetti-wrapper">
        <span class="confetti c1">🎉</span>
        <span class="confetti c2">✨</span>
        <span class="confetti c3">🌟</span>
        <span class="confetti c4">🎊</span>
      </div>

      <!-- 1. 赛博猫咪 (Cat) -->
      <div v-if="settings.petAvatar === 'cat'" class="avatar-svg-box cat-avatar">
        <svg viewBox="0 0 100 100" width="84" height="84">
          <!-- 身体 -->
          <ellipse cx="50" cy="65" rx="30" ry="24" fill="#818cf8" />
          <!-- 肚皮 -->
          <ellipse cx="50" cy="68" rx="20" ry="16" fill="#e0e7ff" />
          <!-- 头部 -->
          <circle cx="50" cy="42" r="24" fill="#6366f1" />
          <!-- 猫耳朵 -->
          <polygon points="30,28 20,8 42,22" fill="#4f46e5" class="cat-ear ear-l" />
          <polygon points="70,28 80,8 58,22" fill="#4f46e5" class="cat-ear ear-r" />
          <polygon points="31,25 24,12 40,22" fill="#f43f5e" />
          <polygon points="69,25 76,12 60,22" fill="#f43f5e" />
          
          <!-- 护目镜 (思考时佩戴) -->
          <g v-if="taskStatus.state === 'thinking'" class="goggles">
            <rect x="28" y="32" width="44" height="14" rx="6" fill="#1e1b4b" stroke="#38bdf8" stroke-width="2" />
            <circle cx="40" cy="39" r="4" fill="#38bdf8" class="goggle-lens" />
            <circle cx="60" cy="39" r="4" fill="#38bdf8" class="goggle-lens" />
          </g>

          <!-- 正常眼睛 -->
          <g v-else class="cat-eyes">
            <ellipse cx="40" cy="39" rx="3.5" ry="5" fill="#1e1b4b" class="eye-l" />
            <ellipse cx="60" cy="39" rx="3.5" ry="5" fill="#1e1b4b" class="eye-r" />
            <circle cx="41.5" cy="37.5" r="1.5" fill="#ffffff" />
            <circle cx="61.5" cy="37.5" r="1.5" fill="#ffffff" />
          </g>

          <!-- 鼻子与胡须 -->
          <polygon points="50,45 47,43 53,43" fill="#f43f5e" />
          <path d="M47 47 Q50 50 53 47" stroke="#1e1b4b" stroke-width="1.5" fill="none" />
          <line x1="26" y1="44" x2="16" y2="42" stroke="#cbd5e1" stroke-width="1.5" />
          <line x1="26" y1="47" x2="16" y2="48" stroke="#cbd5e1" stroke-width="1.5" />
          <line x1="74" y1="44" x2="84" y2="42" stroke="#cbd5e1" stroke-width="1.5" />
          <line x1="74" y1="47" x2="84" y2="48" stroke="#cbd5e1" stroke-width="1.5" />

          <!-- 猫爪子与打字键盘 -->
          <g v-if="taskStatus.state === 'thinking'" class="typing-hands">
            <rect x="30" y="74" width="40" height="12" rx="3" fill="#0f172a" stroke="#6366f1" stroke-width="1.5" />
            <circle cx="36" cy="73" r="5" fill="#e0e7ff" class="paw paw-l" />
            <circle cx="64" cy="73" r="5" fill="#e0e7ff" class="paw paw-r" />
          </g>
          <g v-else>
            <circle cx="38" cy="76" r="5" fill="#e0e7ff" />
            <circle cx="62" cy="76" r="5" fill="#e0e7ff" />
          </g>

          <!-- 猫尾巴 -->
          <path d="M78 68 Q92 60 88 45" stroke="#4f46e5" stroke-width="5" stroke-linecap="round" fill="none" class="cat-tail" />
        </svg>
      </div>

      <!-- 2. 灵动机器人 (Robot) -->
      <div v-else-if="settings.petAvatar === 'robot'" class="avatar-svg-box robot-avatar">
        <svg viewBox="0 0 100 100" width="84" height="84">
          <!-- 天线 -->
          <line x1="50" y1="20" x2="50" y2="8" stroke="#38bdf8" stroke-width="3" />
          <circle cx="50" cy="7" r="5" fill="#f43f5e" class="antenna-light" />
          <!-- 头盔 -->
          <rect x="25" y="20" width="50" height="38" rx="10" fill="#1e293b" stroke="#38bdf8" stroke-width="2.5" />
          <!-- 屏幕面罩 -->
          <rect x="31" y="27" width="38" height="24" rx="6" fill="#0f172a" />
          
          <!-- 眼睛阵列 -->
          <g v-if="taskStatus.state === 'completed'">
            <text x="35" y="44" fill="#4ade80" font-size="14" font-weight="bold">^ _ ^</text>
          </g>
          <g v-else-if="taskStatus.state === 'thinking'">
            <circle cx="42" cy="39" r="4" fill="#38bdf8" class="matrix-eye" />
            <circle cx="58" cy="39" r="4" fill="#38bdf8" class="matrix-eye" />
          </g>
          <g v-else>
            <rect x="38" y="36" width="7" height="7" rx="2" fill="#38bdf8" />
            <rect x="55" y="36" width="7" height="7" rx="2" fill="#38bdf8" />
          </g>

          <!-- 身体 -->
          <rect x="30" y="60" width="40" height="26" rx="6" fill="#334155" stroke="#64748b" stroke-width="2" />
          <circle cx="50" cy="72" r="5" fill="#38bdf8" class="core-light" />
          <!-- 手臂 -->
          <rect x="18" y="63" width="10" height="16" rx="4" fill="#1e293b" class="robot-arm arm-l" />
          <rect x="72" y="63" width="10" height="16" rx="4" fill="#1e293b" class="robot-arm arm-r" />
        </svg>
      </div>

      <!-- 3. 忠诚柴犬 (Dog) -->
      <div v-else class="avatar-svg-box dog-avatar">
        <svg viewBox="0 0 100 100" width="84" height="84">
          <!-- 身体 -->
          <ellipse cx="50" cy="66" rx="28" ry="22" fill="#f59e0b" />
          <ellipse cx="50" cy="69" rx="18" ry="14" fill="#fef3c7" />
          <!-- 头部 -->
          <circle cx="50" cy="42" r="23" fill="#d97706" />
          <ellipse cx="50" cy="47" rx="16" ry="12" fill="#fef3c7" />
          <!-- 耳朵 -->
          <polygon points="32,26 22,10 40,18" fill="#b45309" class="dog-ear ear-l" />
          <polygon points="68,26 78,10 60,18" fill="#b45309" class="dog-ear ear-r" />
          <!-- 眼睛 -->
          <circle cx="41" cy="40" r="3.5" fill="#1e1b4b" />
          <circle cx="59" cy="40" r="3.5" fill="#1e1b4b" />
          <!-- 鼻子与微笑 -->
          <ellipse cx="50" cy="45" rx="3.5" ry="2.5" fill="#1e1b4b" />
          <path d="M47 48 Q50 51 53 48" stroke="#1e1b4b" stroke-width="1.5" fill="none" />
          <!-- 红色项圈 -->
          <rect x="36" y="58" width="28" height="6" rx="2" fill="#ef4444" />
          <circle cx="50" cy="63" r="3" fill="#fbbf24" />
          <!-- 尾巴 -->
          <path d="M76 65 Q88 52 82 40" stroke="#f59e0b" stroke-width="6" stroke-linecap="round" fill="none" class="dog-tail" />
        </svg>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, watch } from 'vue';
import { api } from '../api';
import { playChimeSound } from '../utils/sound';

const props = defineProps({
  isStandalone: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['openSettings']);

const petContainerRef = ref(null);
const isDragging = ref(false);
const isMinimized = ref(false);
const dragOffset = reactive({ x: 0, y: 0 });

// 初始右下角位置 (支持 localStorage 记忆)
const position = reactive({
  x: window.innerWidth - 180,
  y: window.innerHeight - 180
});

const settings = reactive({
  enableDesktopPet: true,
  enableTaskCompleteNotification: true,
  taskCompleteThresholdSeconds: 5,
  enableSound: true,
  enableBalloon: true,
  petAvatar: 'cat'
});

const taskStatus = reactive({
  state: 'idle', // idle, thinking, completed, failover
  model: '',
  channelName: '',
  durationMs: 0,
  totalTokens: 0,
  message: '',
  timestamp: null
});

const currentBubbleText = ref('今天也要元气满满地写代码哦！✨');
const thinkingSeconds = ref(0);
let thinkingTimer = null;
let pollTimer = null;
let bubbleFadeTimer = null;

const avatars = ['cat', 'robot', 'dog'];

const currentAvatarIcon = computed(() => {
  if (settings.petAvatar === 'robot') return '🤖';
  if (settings.petAvatar === 'dog') return '🐶';
  return '🐱';
});

const bubbleIcon = computed(() => {
  if (taskStatus.state === 'thinking') return '⚡';
  if (taskStatus.state === 'completed') return '🎉';
  if (taskStatus.state === 'failover') return '⚠️';
  return '💬';
});

// 过滤气泡文本开头已有的重复 Emoji
const displayBubbleText = computed(() => {
  if (!currentBubbleText.value) return '';
  return currentBubbleText.value
    .replace(/^(⚡|🎉|⚠️|💬|✨)\s*/g, '')
    .trim();
});

const idleQuotes = [
  '代码写累了吗？记得喝口水哦~ 🍵',
  '网关正在全天候守护您的 API 链路！⚡',
  '点击我可以切换专属鼓励语哦~ 🐾',
  '今天也是效率拉满的一天！🚀',
  'Claude / Codex 插件已全部接管就绪 ✨'
];

function handlePetClick() {
  if (taskStatus.state === 'idle') {
    const randomQuote = idleQuotes[Math.floor(Math.random() * idleQuotes.length)];
    showBubble(randomQuote, 5000);
    if (settings.enableSound) {
      playChimeSound();
    }
  }
}

function showBubble(text, duration = 6000) {
  currentBubbleText.value = text;
  if (bubbleFadeTimer) clearTimeout(bubbleFadeTimer);
  bubbleFadeTimer = setTimeout(() => {
    if (taskStatus.state === 'idle') {
      currentBubbleText.value = '';
    }
  }, duration);
}

function toggleSound() {
  settings.enableSound = !settings.enableSound;
  saveSettings();
  if (settings.enableSound) {
    playChimeSound();
    showBubble('提示音已开启 🔊', 3000);
  } else {
    showBubble('提示音已静音 🔇', 3000);
  }
}

function switchAvatar() {
  const nextIdx = (avatars.indexOf(settings.petAvatar) + 1) % avatars.length;
  settings.petAvatar = avatars[nextIdx];
  saveSettings();
  showBubble(`切换为 ${currentAvatarIcon.value} 模式啦！`, 3000);
}

async function openPipOrWindow() {
  if ('documentPictureInPicture' in window) {
    try {
      const pipWin = await window.documentPictureInPicture.requestWindow({
        width: 200,
        height: 240
      });
      [...document.styleSheets].forEach((styleSheet) => {
        try {
          const cssRules = [...styleSheet.cssRules].map((rule) => rule.cssText).join('');
          const style = document.createElement('style');
          style.textContent = cssRules;
          pipWin.document.head.appendChild(style);
        } catch {
          if (styleSheet.href) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = styleSheet.href;
            pipWin.document.head.appendChild(link);
          }
        }
      });
      pipWin.document.body.innerHTML = '<div id="pip-pet-root" style="display:flex;align-items:center;justify-content:center;height:100vh;background:transparent;overflow:hidden;"></div>';
      const petNode = petContainerRef.value;
      if (petNode) {
        pipWin.document.getElementById('pip-pet-root').appendChild(petNode);
      }
      return;
    } catch (e) {
      console.warn('PiP 画中画调用失败，使用独立悬浮窗口:', e);
    }
  }

  // 降级为弹出独立置顶窗口
  const left = window.screen.width - 240;
  const top = window.screen.height - 300;
  window.open(
    '/pet',
    'DesktopPetWindow',
    `width=220,height=260,left=${left},top=${top},menubar=no,status=no,toolbar=no,location=no,resizable=yes`
  );
}

async function loadSettings() {
  try {
    const res = await api.getNotificationSettings();
    if (res) {
      Object.assign(settings, res);
    }
  } catch (err) {
    console.error('加载宠物与提醒配置失败:', err);
  }
}

async function saveSettings() {
  try {
    await api.saveNotificationSettings(settings);
  } catch (err) {
    console.error('保存设置失败:', err);
  }
}

// 拖拽逻辑
function handleMouseDown(e) {
  if (e.target.closest('button')) return;
  isDragging.value = true;
  dragOffset.x = e.clientX - position.x;
  dragOffset.y = e.clientY - position.y;

  window.addEventListener('mousemove', handleMouseMove);
  window.addEventListener('mouseup', handleMouseUp);
}

function handleMouseMove(e) {
  if (!isDragging.value) return;
  let newX = e.clientX - dragOffset.x;
  let newY = e.clientY - dragOffset.y;

  // 边界约束
  newX = Math.max(10, Math.min(window.innerWidth - 120, newX));
  newY = Math.max(10, Math.min(window.innerHeight - 120, newY));

  position.x = newX;
  position.y = newY;
}

function handleMouseUp() {
  if (isDragging.value) {
    isDragging.value = false;
    window.removeEventListener('mousemove', handleMouseMove);
    window.removeEventListener('mouseup', handleMouseUp);
    localStorage.setItem('desktop_pet_pos', JSON.stringify({ x: position.x, y: position.y }));
  }
}

// 轮询后端实时任务状态并驱动动画
async function pollTaskStatus() {
  try {
    const res = await api.getTaskStatus();
    if (res && res.state) {
      const prevState = taskStatus.state;
      Object.assign(taskStatus, res);

      // 状态变迁处理
      if (taskStatus.state === 'thinking') {
        if (prevState !== 'thinking') {
          thinkingSeconds.value = 1;
          if (thinkingTimer) clearInterval(thinkingTimer);
          thinkingTimer = setInterval(() => {
            thinkingSeconds.value++;
          }, 1000);
          showBubble(`正在思考生成中... (${taskStatus.model || 'AI'})`, 15000);
        }
      } else if (taskStatus.state === 'completed') {
        if (prevState === 'thinking') {
          if (thinkingTimer) clearInterval(thinkingTimer);
          const durSec = (taskStatus.durationMs / 1000).toFixed(1);
          const tokenStr = taskStatus.totalTokens > 0 ? ` (消耗 ${taskStatus.totalTokens} Tokens)` : '';
          
          showBubble(`任务已完成！耗时 ${durSec}s${tokenStr}`, 8000);

          // 触发声音 (达到阈值时)
          if (settings.enableSound && (taskStatus.durationMs / 1000) >= settings.taskCompleteThresholdSeconds) {
            playChimeSound();
          }
        }
      } else if (taskStatus.state === 'failover') {
        showBubble(`${taskStatus.message || '触发渠道故障转移'}，已切换备用！`, 6000);
      }
    }
  } catch { }
}

onMounted(() => {
  // 如果是独立全屏/迷你小窗模式，居中放置
  if (props.isStandalone) {
    position.x = (window.innerWidth - 100) / 2;
    position.y = (window.innerHeight - 100) / 2;
  } else {
    // 读取记忆位置
    try {
      const savedPos = localStorage.getItem('desktop_pet_pos');
      if (savedPos) {
        const p = JSON.parse(savedPos);
        position.x = Math.min(window.innerWidth - 120, Math.max(10, p.x));
        position.y = Math.min(window.innerHeight - 120, Math.max(10, p.y));
      }
    } catch { }
  }

  loadSettings();

  // 每 1.2 秒轮询一次任务状态
  pollTimer = setInterval(pollTaskStatus, 1200);

  // 初始问候语淡出
  showBubble('今天也要元气满满地写代码哦！✨', 4500);
});

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer);
  if (thinkingTimer) clearInterval(thinkingTimer);
  if (bubbleFadeTimer) clearTimeout(bubbleFadeTimer);
});
</script>

<style scoped>
.desktop-pet-container {
  position: fixed;
  z-index: 999;
  cursor: grab;
  user-select: none;
  touch-action: none;
  transition: transform 0.15s ease;
}

.desktop-pet-container:active {
  cursor: grabbing;
}

.desktop-pet-container.is-dragging {
  transition: none;
}

/* 💬 气泡对话框 */
.pet-bubble {
  position: absolute;
  bottom: calc(100% + 14px);
  left: 50%;
  transform: translateX(-50%);
  min-width: 170px;
  max-width: 240px;
  padding: 10px 14px;
  background: var(--bg-surface);
  border: 1px solid var(--border-active);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
  pointer-events: auto;
  z-index: 30;
}

.pet-bubble::after {
  content: '';
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  border: 6px solid transparent;
  border-top-color: var(--bg-surface);
}

.bubble-content {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  font-size: 12px;
  line-height: 1.4;
  color: var(--text-main);
  font-weight: 500;
}

.bubble-icon {
  font-size: 14px;
  line-height: 1.2;
}

.bubble-close-btn {
  background: transparent;
  border: none;
  color: var(--text-dim);
  font-size: 11px;
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.bubble-close-btn:hover {
  color: var(--text-main);
}

/* 🐾 悬浮工具栏（居中上方展示，不与底座计时器冲突） */
.pet-action-toolbar {
  position: absolute;
  top: -16px;
  left: 50%;
  transform: translateX(-50%) translateY(4px);
  display: flex;
  align-items: center;
  gap: 4px;
  background: var(--bg-surface);
  padding: 3px 8px;
  border-radius: 20px;
  border: 1px solid var(--border-subtle);
  box-shadow: var(--shadow-md);
  opacity: 0;
  transition: all 0.2s ease;
  pointer-events: none;
  z-index: 20;
  white-space: nowrap;
}

.desktop-pet-container:hover .pet-action-toolbar {
  opacity: 1;
  transform: translateX(-50%) translateY(0);
  pointer-events: auto;
}

.pet-tool-btn {
  background: transparent;
  border: none;
  font-size: 11px;
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 4px;
  transition: transform 0.15s;
}

.pet-tool-btn:hover {
  transform: scale(1.25);
}

/* 最小化状态 */
.pet-minimized-icon {
  width: 42px;
  height: 42px;
  border-radius: 50%;
  background: var(--bg-surface);
  border: 2px solid var(--accent-primary);
  box-shadow: 0 4px 12px var(--accent-glow);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  cursor: pointer;
  position: relative;
  transition: transform 0.2s;
}

.pet-minimized-icon:hover {
  transform: scale(1.1);
}

.mini-pulse-dot {
  position: absolute;
  top: 0;
  right: 0;
  width: 10px;
  height: 10px;
  background: var(--danger);
  border-radius: 50%;
  box-shadow: 0 0 6px var(--danger);
  animation: pulse-dot 1.2s infinite;
}

@keyframes pulse-dot {
  0% { transform: scale(0.9); opacity: 0.8; }
  50% { transform: scale(1.3); opacity: 1; }
  100% { transform: scale(0.9); opacity: 0.8; }
}

/* 计时器徽章：放置在宠物底座下方，绝不遮挡上方工具栏或气泡 */
.timer-badge {
  position: absolute;
  bottom: -6px;
  left: 50%;
  transform: translateX(-50%);
  background: linear-gradient(135deg, #f59e0b, #ef4444);
  color: #fff;
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 800;
  font-family: var(--font-mono);
  box-shadow: 0 2px 8px rgba(245, 158, 11, 0.5);
  display: flex;
  align-items: center;
  gap: 4px;
  z-index: 5;
  white-space: nowrap;
  animation: badge-bounce 1s infinite alternate;
}

@keyframes badge-bounce {
  from { transform: translateY(0); }
  to { transform: translateY(-3px); }
}

/* 彩带撒花特效 */
.confetti-wrapper {
  position: absolute;
  top: -20px;
  left: 0;
  right: 0;
  pointer-events: none;
}

.confetti {
  position: absolute;
  font-size: 18px;
  animation: confetti-fly 1.5s cubic-bezier(0.2, 0.8, 0.2, 1) forwards;
}

.c1 { left: 10%; animation-delay: 0s; }
.c2 { left: 40%; animation-delay: 0.1s; }
.c3 { left: 70%; animation-delay: 0.2s; }
.c4 { left: 90%; animation-delay: 0.05s; }

@keyframes confetti-fly {
  0% { opacity: 0; transform: translateY(10px) scale(0.5); }
  30% { opacity: 1; transform: translateY(-25px) scale(1.3) rotate(20deg); }
  100% { opacity: 0; transform: translateY(-40px) scale(1) rotate(-20deg); }
}

/* 宠物主体 SVG 动效 */
.avatar-svg-box {
  filter: drop-shadow(0 6px 16px rgba(0, 0, 0, 0.35));
  transition: transform 0.2s;
}

.avatar-svg-box:hover {
  transform: translateY(-4px) scale(1.05);
}

/* 动效：猫咪耳朵与尾巴摆动 */
.cat-tail {
  transform-origin: 78px 68px;
  animation: tail-wag 3s infinite ease-in-out;
}

@keyframes tail-wag {
  0%, 100% { transform: rotate(0deg); }
  50% { transform: rotate(15deg); }
}

.cat-ear {
  transform-origin: 50px 30px;
  animation: ear-twitch 4s infinite ease-in-out;
}

@keyframes ear-twitch {
  0%, 90%, 100% { transform: rotate(0); }
  92% { transform: rotate(-5deg); }
  96% { transform: rotate(5deg); }
}

/* 动效：打字敲键盘 */
.is-thinking .paw-l {
  animation: paw-type 0.2s infinite alternate ease-in-out;
}

.is-thinking .paw-r {
  animation: paw-type 0.2s infinite alternate-reverse ease-in-out;
}

@keyframes paw-type {
  from { transform: translateY(0); }
  to { transform: translateY(-4px); }
}

/* 动效：跳跃欢呼 (Completed) */
.is-completed .avatar-svg-box {
  animation: pet-jump 0.6s cubic-bezier(0.36, 0.07, 0.19, 0.97) 2;
}

@keyframes pet-jump {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-16px) scale(1.1); }
}

/* 动效：机器人天线发光与核心呼吸 */
.antenna-light {
  animation: light-blink 1.5s infinite;
}

@keyframes light-blink {
  0%, 100% { fill: #f43f5e; }
  50% { fill: #38bdf8; }
}

.core-light {
  animation: core-pulse 2s infinite ease-in-out;
}

@keyframes core-pulse {
  0%, 100% { fill: #38bdf8; opacity: 0.6; }
  50% { fill: #6366f1; opacity: 1; }
}

/* 气泡进出动画 */
.bubble-pop-enter-active,
.bubble-pop-leave-active {
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}

.bubble-pop-enter-from,
.bubble-pop-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(8px) scale(0.9);
}
</style>
