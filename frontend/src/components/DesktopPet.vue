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

    <!-- 🐱 主体动画展示区 (核心：经典 Bongo Cat 纯净萌系画风) -->
    <div v-else class="pet-body-wrapper" @click="handlePetClick">
      <!-- 实时计时器徽章 (思考生成或工具执行时展示多轮与耗时) -->
      <div 
        v-if="taskStatus.state === 'thinking' || taskStatus.state === 'tool_use'" 
        class="timer-badge"
        :class="{ 'is-tool': taskStatus.state === 'tool_use' }"
      >
        <span class="timer-icon">{{ taskStatus.state === 'tool_use' ? '🔍' : '⚡' }}</span>
        <span class="timer-text">{{ formattedThinkingTime }}</span>
        <span v-if="taskStatus.turnCount > 1" class="turn-badge">(第 {{ taskStatus.turnCount }} 步)</span>
      </div>

      <!-- 撒花/彩带粒子特效 (完成时绽放) -->
      <div v-if="taskStatus.state === 'completed'" class="confetti-wrapper">
        <span class="confetti c1">🎉</span>
        <span class="confetti c2">✨</span>
        <span class="confetti c3">🌟</span>
        <span class="confetti c4">🎊</span>
      </div>

      <!-- 1. 经典 Bongo Cat 敲键盘猫 (默认经典皮肤) -->
      <div v-if="settings.petAvatar === 'cat'" class="avatar-svg-box bongo-avatar">
        <svg viewBox="0 0 120 100" width="96" height="84" class="bongo-svg">
          <!-- 身体底色与轮廓 -->
          <ellipse cx="60" cy="50" rx="42" ry="34" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" />
          <!-- 猫耳朵 -->
          <polygon points="30,30 20,8 48,20" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" class="bongo-ear ear-l" />
          <polygon points="90,30 100,8 72,20" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" class="bongo-ear ear-r" />
          <polygon points="32,26 25,14 44,20" fill="#fecdd3" />
          <polygon points="88,26 95,14 76,20" fill="#fecdd3" />
          
          <!-- 腮红 -->
          <ellipse cx="32" cy="52" rx="7" ry="4" fill="#fda4af" />
          <ellipse cx="88" cy="52" rx="7" ry="4" fill="#fda4af" />
          
          <!-- 眼睛 -->
          <g v-if="taskStatus.state === 'completed'">
            <path d="M38 42 Q46 32 54 42" stroke="#1e293b" stroke-width="3" fill="none" stroke-linecap="round" />
            <path d="M66 42 Q74 32 82 42" stroke="#1e293b" stroke-width="3" fill="none" stroke-linecap="round" />
          </g>
          <g v-else>
            <circle cx="46" cy="42" r="5" fill="#1e293b" />
            <circle cx="74" cy="42" r="5" fill="#1e293b" />
            <circle cx="48" cy="40" r="1.8" fill="#ffffff" />
            <circle cx="76" cy="40" r="1.8" fill="#ffffff" />
          </g>
          
          <!-- 嘴巴与小粉鼻 -->
          <polygon points="57,48 63,48 60,52" fill="#fda4af" />
          <path d="M52 50 Q60 56 60 50 Q60 56 68 50" stroke="#1e293b" stroke-width="2.2" fill="none" stroke-linecap="round" />
          
          <!-- 木质小桌面与键盘 -->
          <rect x="10" y="68" width="100" height="22" rx="6" fill="#f1f5f9" stroke="#94a3b8" stroke-width="2" />
          <rect x="30" y="72" width="60" height="14" rx="3" fill="#1e293b" />
          <rect x="36" y="75" width="8" height="8" rx="1" fill="#38bdf8" />
          <rect x="48" y="75" width="8" height="8" rx="1" fill="#a855f7" />
          <rect x="60" y="75" width="8" height="8" rx="1" fill="#ec4899" />
          <rect x="72" y="75" width="8" height="8" rx="1" fill="#22c55e" />
          
          <!-- 双爪动作分流 -->
          <!-- 欢呼状态 -->
          <g v-if="taskStatus.state === 'completed'">
            <ellipse cx="25" cy="20" rx="9" ry="12" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" class="cheer-paw-l" />
            <ellipse cx="95" cy="20" rx="9" ry="12" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" class="cheer-paw-r" />
          </g>
          <!-- 敲键盘状态 (极速交替拍打) -->
          <g v-else-if="taskStatus.state === 'thinking'" class="bongo-typing-paws">
            <ellipse cx="36" cy="74" rx="10" ry="8" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" class="bongo-paw-l" />
            <ellipse cx="84" cy="74" rx="10" ry="8" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" class="bongo-paw-r" />
          </g>
          <!-- 工具态 (举放大镜) -->
          <g v-else-if="taskStatus.state === 'tool_use'">
            <ellipse cx="36" cy="74" rx="10" ry="8" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" />
            <ellipse cx="88" cy="46" rx="10" ry="9" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" />
            <circle cx="95" cy="35" r="7" stroke="#6366f1" stroke-width="2.5" fill="none" />
            <line x1="100" y1="40" x2="106" y2="46" stroke="#6366f1" stroke-width="2.5" stroke-linecap="round" />
          </g>
          <!-- 默认空闲 -->
          <g v-else>
            <ellipse cx="36" cy="74" rx="10" ry="8" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" />
            <ellipse cx="84" cy="74" rx="10" ry="8" fill="#ffffff" stroke="#1e293b" stroke-width="2.6" />
          </g>
        </svg>
      </div>

      <!-- 2. 灵动机器人 (Robot) -->
      <div v-else-if="settings.petAvatar === 'robot'" class="avatar-svg-box robot-avatar">
        <svg viewBox="0 0 100 100" width="84" height="84">
          <!-- 身体 -->
          <rect x="25" y="52" width="50" height="38" rx="8" fill="#334155" stroke="#475569" stroke-width="2" />
          <rect x="35" y="62" width="30" height="18" rx="4" fill="#0f172a" />
          <line x1="40" y1="71" x2="60" y2="71" stroke="#38bdf8" stroke-width="2" stroke-dasharray="4,2" class="data-stream" />
          
          <!-- 天线 -->
          <line x1="50" y1="18" x2="50" y2="10" stroke="#94a3b8" stroke-width="3" />
          <circle cx="50" cy="8" r="5" fill="#f43f5e" class="robot-antenna" />
          
          <!-- 头部 -->
          <rect x="20" y="18" width="60" height="38" rx="10" fill="#1e293b" stroke="#38bdf8" stroke-width="2" />
          
          <!-- 眼睛 (LED 屏效果) -->
          <g v-if="taskStatus.state === 'completed'">
            <path d="M30 36 L44 36" stroke="#4ade80" stroke-width="4" stroke-linecap="round" />
            <path d="M56 36 L70 36" stroke="#4ade80" stroke-width="4" stroke-linecap="round" />
          </g>
          <g v-else-if="taskStatus.state === 'thinking'" class="robot-thinking-eyes">
            <rect x="32" y="32" width="12" height="12" rx="2" fill="#38bdf8" />
            <rect x="56" y="32" width="12" height="12" rx="2" fill="#38bdf8" />
          </g>
          <g v-else>
            <circle cx="38" cy="37" r="5" fill="#38bdf8" class="led-eye" />
            <circle cx="62" cy="37" r="5" fill="#38bdf8" class="led-eye" />
          </g>
        </svg>
      </div>

      <!-- 3. 忠诚柴犬 (Dog) -->
      <div v-else class="avatar-svg-box dog-avatar">
        <svg viewBox="0 0 100 100" width="84" height="84">
          <!-- 身体 -->
          <ellipse cx="50" cy="65" rx="32" ry="24" fill="#f59e0b" />
          <ellipse cx="50" cy="68" rx="22" ry="16" fill="#fef3c7" />
          <!-- 头部 -->
          <ellipse cx="50" cy="42" rx="26" ry="22" fill="#f59e0b" />
          <!-- 白斑脸颊 -->
          <ellipse cx="50" cy="46" rx="18" ry="14" fill="#fef3c7" />
          <!-- 耳朵 -->
          <polygon points="28,30 18,10 40,24" fill="#b45309" class="dog-ear ear-l" />
          <polygon points="72,30 82,10 60,24" fill="#b45309" class="dog-ear ear-r" />
          <!-- 眼睛 -->
          <ellipse cx="40" cy="38" rx="3.5" ry="4.5" fill="#1e1b4b" />
          <ellipse cx="60" cy="38" rx="3.5" ry="4.5" fill="#1e1b4b" />
          <circle cx="41.5" cy="36.5" r="1.5" fill="#ffffff" />
          <circle cx="61.5" cy="36.5" r="1.5" fill="#ffffff" />
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
  if (taskStatus.state === 'tool_use') return '🔍';
  if (taskStatus.state === 'thinking') return '⚡';
  if (taskStatus.state === 'completed') return '🎉';
  if (taskStatus.state === 'failover') return '⚠️';
  return '💬';
});

const formattedThinkingTime = computed(() => {
  const s = thinkingSeconds.value;
  if (s >= 60) {
    const mins = Math.floor(s / 60);
    const secs = s % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }
  return `${s}s`;
});

// 过滤气泡文本开头已有的重复 Emoji
const displayBubbleText = computed(() => {
  if (!currentBubbleText.value) return '';
  return currentBubbleText.value
    .replace(/^(⚡|🎉|⚠️|💬|✨|🔍)\s*/g, '')
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

// 轮询后端实时任务状态并驱动动画（精准理解 Agent 多轮工作流）
async function pollTaskStatus() {
  try {
    const res = await api.getTaskStatus();
    if (res && res.state) {
      const prevState = taskStatus.state;
      Object.assign(taskStatus, res);

      // 状态变迁处理
      if (taskStatus.state === 'thinking') {
        if (prevState !== 'thinking' && prevState !== 'tool_use') {
          thinkingSeconds.value = 1;
          if (thinkingTimer) clearInterval(thinkingTimer);
          thinkingTimer = setInterval(() => {
            thinkingSeconds.value++;
          }, 1000);
          const turnTip = taskStatus.turnCount > 1 ? ` (第 ${taskStatus.turnCount} 步)` : '';
          showBubble(`正在思考生成中...${turnTip}`, 15000);
        }
      } else if (taskStatus.state === 'tool_use') {
        // 工具执行状态：保持计时，提示工具调用
        showBubble(`正在调用工具处理中 (第 ${taskStatus.turnCount || 1} 步)... 🔍`, 8000);
      } else if (taskStatus.state === 'completed') {
        if (prevState === 'thinking' || prevState === 'tool_use') {
          if (thinkingTimer) clearInterval(thinkingTimer);
          const totalMs = taskStatus.sessionDurationMs > 0 ? taskStatus.sessionDurationMs : taskStatus.durationMs;
          const durSec = (totalMs / 1000).toFixed(1);
          const durText = durSec >= 60 ? `${Math.floor(durSec / 60)}分${Math.floor(durSec % 60)}秒` : `${durSec}s`;
          const tokenStr = taskStatus.sessionTotalTokens > 0 
            ? ` (累计 ${taskStatus.sessionTotalTokens.toLocaleString()} Tokens)` 
            : (taskStatus.totalTokens > 0 ? ` (消耗 ${taskStatus.totalTokens.toLocaleString()} Tokens)` : '');
          const turnInfo = taskStatus.turnCount > 1 ? `共 ${taskStatus.turnCount} 步，` : '';
          
          showBubble(`任务全部完成啦！${turnInfo}总耗时 ${durText}${tokenStr} 🎉`, 8000);

          // 触发声音 (达到阈值时)
          if (settings.enableSound && (totalMs / 1000) >= settings.taskCompleteThresholdSeconds) {
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

/* 动效：Bongo Cat 招牌交替敲键盘动效 */
@keyframes bongoPawL {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-8px); }
}

@keyframes bongoPawR {
  0%, 100% { transform: translateY(-8px); }
  50% { transform: translateY(0); }
}

.bongo-paw-l {
  animation: bongoPawL 0.16s infinite ease-in-out;
  transform-origin: center;
}

.bongo-paw-r {
  animation: bongoPawR 0.16s infinite ease-in-out;
  transform-origin: center;
}

@keyframes bongoCheerL {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  50% { transform: translateY(-6px) rotate(10deg); }
}

@keyframes bongoCheerR {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  50% { transform: translateY(-6px) rotate(-10deg); }
}

.cheer-paw-l {
  animation: bongoCheerL 0.4s infinite ease-in-out alternate;
  transform-origin: bottom center;
}

.cheer-paw-r {
  animation: bongoCheerR 0.4s infinite ease-in-out alternate;
  transform-origin: bottom center;
}

.timer-badge.is-tool {
  background: linear-gradient(135deg, #ea580c, #f97316);
  box-shadow: 0 2px 8px rgba(234, 88, 12, 0.5);
}

.turn-badge {
  font-size: 9.5px;
  opacity: 0.92;
  margin-left: 2px;
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

.cat-ear, .bongo-ear {
  transform-origin: 50px 30px;
  animation: ear-twitch 4s infinite ease-in-out;
}

@keyframes ear-twitch {
  0%, 90%, 100% { transform: rotate(0); }
  92% { transform: rotate(-5deg); }
  96% { transform: rotate(5deg); }
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
