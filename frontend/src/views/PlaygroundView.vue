<template>
  <div class="playground-view">
    <div class="view-header">
      <div class="header-left">
        <h2 class="view-title">🧪 调试沙箱与实时诊断 (Playground)</h2>
        <p class="view-desc">在 Web 控制台直接与大模型对话，实时验证模型别名改写、上游网络代理及容灾链路。</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-secondary btn-sm" @click="clearMessages" :disabled="messages.length === 0">
          <span>🗑️ 清空对话</span>
        </button>
        <button class="btn btn-secondary btn-sm" @click="showInspector = !showInspector">
          <span>{{ showInspector ? '👁️ 隐藏诊断面板' : '🔍 展开诊断面板' }}</span>
        </button>
      </div>
    </div>

    <!-- 顶部参数配置栏 -->
    <div class="config-bar glass-card">
      <div class="config-item">
        <label class="config-label">调用模型 (Model):</label>
        <div class="model-select-wrapper">
          <input 
            v-model="model" 
            class="form-input font-mono model-input" 
            placeholder="例如 claude-3-7-sonnet" 
            list="model-suggestions"
          />
          <datalist id="model-suggestions">
            <option value="claude-3-7-sonnet" />
            <option value="claude-3-5-sonnet-20241022" />
            <option value="claude-3-5-haiku-20241022" />
            <option value="gpt-4o" />
            <option value="gpt-4o-mini" />
            <option value="gpt-5.6-sol" />
            <option value="deepseek-chat" />
            <option value="deepseek-reasoner" />
          </datalist>
        </div>
      </div>

      <div class="config-item">
        <label class="config-label">路由分组 (Group):</label>
        <select v-model="selectedGroup" class="form-input select-input">
          <option value="all">🌐 通用分组 (/v1)</option>
          <option value="claude">🎯 Claude 专用 (/claude)</option>
          <option value="codex">🎯 Codex 专用 (/codex)</option>
        </select>
      </div>

      <div class="config-item">
        <label class="config-label">温度 (Temp: {{ temperature }}):</label>
        <input 
          type="range" 
          v-model.number="temperature" 
          min="0" 
          max="2" 
          step="0.1" 
          class="range-input" 
        />
      </div>

      <div class="config-item flex-center">
        <label class="switch-label">
          <input type="checkbox" v-model="isStream" />
          <span>⚡ 流式打字输出 (Stream)</span>
        </label>
      </div>
    </div>

    <!-- 主体区域：左侧聊天，右侧实时诊断面板 -->
    <div class="playground-layout">
      <!-- 聊天主窗口 -->
      <div class="chat-container glass-card">
        <div class="messages-viewport" ref="messagesViewport">
          <div v-if="messages.length === 0" class="empty-state">
            <div class="empty-icon">🤖</div>
            <h3 class="empty-title">欢迎使用网关 Web 调试沙箱</h3>
            <p class="empty-desc">无需配置客户端，即可直接测试当前网关已启用的各渠道连通性、模型改写与响应质量。</p>
            <div class="quick-prompts">
              <button class="prompt-chip" @click="sendQuickPrompt('帮我用 C# 写一个高并发安全的 HttpClient 连接池管理类')">
                💡 C# 并发连接池代码
              </button>
              <button class="prompt-chip" @click="sendQuickPrompt('请以极简要点总结一下 Claude Prompt Caching 的省钱原理')">
                💡 解释 Prompt Caching
              </button>
              <button class="prompt-chip" @click="sendQuickPrompt('Hello, please introduce yourself and your model name.')">
                💡 模型自我介绍与名称探测
              </button>
            </div>
          </div>

          <div 
            v-for="(msg, idx) in messages" 
            :key="idx" 
            :class="['message-row', msg.role]"
          >
            <div class="avatar">
              {{ msg.role === 'user' ? '🧑' : '🤖' }}
            </div>
            <div class="bubble">
              <div class="bubble-header">
                <span class="role-name">{{ msg.role === 'user' ? '用户 (You)' : (msg.model || model) }}</span>
                <span class="msg-time">{{ msg.time }}</span>
              </div>
              <div class="bubble-content font-mono">{{ msg.content }}</div>
              <div v-if="msg.role === 'assistant' && msg.isGenerating" class="generating-cursor">▍</div>
            </div>
          </div>
        </div>

        <!-- 底部输入框 -->
        <div class="chat-input-area">
          <textarea 
            v-model="inputContent" 
            class="chat-textarea font-mono" 
            placeholder="输入您的问题... (按 Enter 发送，Shift + Enter 换行)" 
            rows="2"
            @keydown="handleKeyDown"
            :disabled="isGenerating"
          ></textarea>
          <div class="input-actions">
            <button 
              v-if="isGenerating" 
              class="btn btn-danger btn-sm" 
              @click="stopGeneration"
            >
              <span>⏹ 停止生成</span>
            </button>
            <button 
              v-else 
              class="btn btn-primary btn-sm" 
              @click="sendMessage" 
              :disabled="!inputContent.trim()"
            >
              <span>🚀 发送消息</span>
            </button>
          </div>
        </div>
      </div>

      <!-- 右侧实时诊断面板 -->
      <div v-if="showInspector" class="inspector-container glass-card">
        <div class="inspector-header">
          <span class="inspector-title">🔍 实时调用诊断透视</span>
          <span v-if="latestDebug.channel" class="badge badge-success font-mono">
            {{ latestDebug.channel }}
          </span>
        </div>

        <div v-if="!latestDebug.hasRun" class="inspector-empty">
          <span>暂无诊断数据，发送消息后将在此实时透视调用链路</span>
        </div>

        <div v-else class="inspector-body">
          <!-- KPI 概览 -->
          <div class="inspector-kpis">
            <div class="kpi-box">
              <span class="kpi-label">⚡ 响应耗时</span>
              <span class="kpi-val highlight">{{ latestDebug.latencyMs }} ms</span>
            </div>
            <div class="kpi-box">
              <span class="kpi-label">📊 状态码</span>
              <span :class="['kpi-val', latestDebug.statusCode === 200 ? 'text-success' : 'text-danger']">
                HTTP {{ latestDebug.statusCode }}
              </span>
            </div>
          </div>

          <!-- 链路详情 -->
          <div class="diagnostic-group">
            <div class="diag-item">
              <span class="diag-label">命中渠道:</span>
              <span class="diag-val font-mono">{{ latestDebug.channel || '未命中' }}</span>
            </div>
            <div class="diag-item">
              <span class="diag-label">模型别名映射:</span>
              <span class="diag-val font-mono">
                {{ latestDebug.modelMapping || `${model} (原样直通)` }}
              </span>
            </div>
            <div class="diag-item">
              <span class="diag-label">请求分组端点:</span>
              <span class="diag-val font-mono">/{{ selectedGroup }}/v1/chat/completions</span>
            </div>
            <div v-if="latestDebug.failoverChain" class="diag-item">
              <span class="diag-label">故障转移链路:</span>
              <span class="diag-val font-mono text-warning">{{ latestDebug.failoverChain }}</span>
            </div>
          </div>

          <!-- Token 消耗细分 -->
          <div class="diagnostic-group">
            <div class="group-title">📊 Token 消耗估算</div>
            <div class="diag-grid">
              <div class="diag-mini-item">
                <span class="mini-label">Prompt (输入)</span>
                <span class="mini-val">{{ latestDebug.promptTokens }}</span>
              </div>
              <div class="diag-mini-item">
                <span class="mini-label">Completion (输出)</span>
                <span class="mini-val">{{ latestDebug.completionTokens }}</span>
              </div>
              <div class="diag-mini-item">
                <span class="mini-label">Total (总消耗)</span>
                <span class="mini-val highlight">{{ latestDebug.totalTokens }}</span>
              </div>
            </div>
          </div>

          <!-- 原始请求结构体 -->
          <div class="code-fold-section">
            <div class="fold-header" @click="showReqJson = !showReqJson">
              <span>{{ showReqJson ? '▼' : '▶' }} 客户端请求 JSON 负载</span>
            </div>
            <pre v-if="showReqJson" class="json-code font-mono">{{ latestDebug.requestJson }}</pre>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, nextTick } from 'vue';

const emit = defineEmits(['toast']);

const model = ref('claude-3-7-sonnet');
const selectedGroup = ref('all');
const temperature = ref(0.7);
const isStream = ref(true);
const inputContent = ref('');
const isGenerating = ref(false);
const showInspector = ref(true);
const showReqJson = ref(false);

const messages = ref([]);
const messagesViewport = ref(null);
let abortController = null;

const latestDebug = reactive({
  hasRun: false,
  channel: '',
  modelMapping: '',
  latencyMs: 0,
  statusCode: 200,
  failoverChain: '',
  promptTokens: 0,
  completionTokens: 0,
  totalTokens: 0,
  requestJson: ''
});

function clearMessages() {
  messages.value = [];
  latestDebug.hasRun = false;
  emit('toast', '已清空沙箱对话记录', 'info');
}

function sendQuickPrompt(promptText) {
  inputContent.value = promptText;
  sendMessage();
}

function handleKeyDown(e) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendMessage();
  }
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesViewport.value) {
      messagesViewport.value.scrollTop = messagesViewport.value.scrollHeight;
    }
  });
}

function stopGeneration() {
  if (abortController) {
    abortController.abort();
    abortController = null;
  }
  isGenerating.value = false;
  const lastMsg = messages.value[messages.value.length - 1];
  if (lastMsg && lastMsg.role === 'assistant') {
    lastMsg.isGenerating = false;
  }
  emit('toast', '已停止模型输出', 'warning');
}

async function sendMessage() {
  const content = inputContent.value.trim();
  if (!content || isGenerating.value) return;

  const userTime = new Date().toLocaleTimeString();
  messages.value.push({
    role: 'user',
    content,
    time: userTime
  });

  inputContent.value = '';
  scrollToBottom();

  const assistantMsg = reactive({
    role: 'assistant',
    model: model.value,
    content: '',
    time: new Date().toLocaleTimeString(),
    isGenerating: true
  });
  messages.value.push(assistantMsg);
  scrollToBottom();

  isGenerating.value = true;
  abortController = new AbortController();

  // 构建标准 OpenAI / Claude 对话消息数组
  const chatMessages = messages.value
    .filter(m => m !== assistantMsg)
    .map(m => ({ role: m.role, content: m.content }));

  const payload = {
    model: model.value,
    messages: chatMessages,
    temperature: temperature.value,
    stream: isStream.value
  };

  latestDebug.requestJson = JSON.stringify(payload, null, 2);
  const startTime = performance.now();

  const endpoint = selectedGroup.value === 'all' 
    ? '/v1/chat/completions' 
    : `/${selectedGroup.value}/v1/chat/completions`;

  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Gateway-Group': selectedGroup.value
      },
      body: JSON.stringify(payload),
      signal: abortController.signal
    });

    const elapsed = Math.round(performance.now() - startTime);
    latestDebug.hasRun = true;
    latestDebug.statusCode = response.status;
    latestDebug.latencyMs = elapsed;
    latestDebug.channel = response.headers.get('X-Gateway-Channel') || '默认主力渠道';
    latestDebug.modelMapping = response.headers.get('X-Gateway-Model-Mapped') || '';
    latestDebug.failoverChain = response.headers.get('X-Gateway-Failover-Chain') || '';

    if (!response.ok) {
      const errText = await response.text();
      assistantMsg.content = `❌ [HTTP ${response.status} 错误]: ${errText}`;
      assistantMsg.isGenerating = false;
      isGenerating.value = false;
      return;
    }

    if (!isStream.value) {
      const json = await response.json();
      const answer = json.choices?.[0]?.message?.content || json.content?.[0]?.text || JSON.stringify(json);
      assistantMsg.content = answer;
      assistantMsg.isGenerating = false;

      if (json.usage) {
        latestDebug.promptTokens = json.usage.prompt_tokens || 0;
        latestDebug.completionTokens = json.usage.completion_tokens || 0;
        latestDebug.totalTokens = json.usage.total_tokens || 0;
      }
    } else {
      // 流式 SSE 读取
      const reader = response.body.getReader();
      const decoder = new TextDecoder('utf-8');
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          const trimmed = line.trim();
          if (!trimmed || trimmed.startsWith(':')) continue;

          if (trimmed.startsWith('data: ')) {
            const dataStr = trimmed.slice(6).trim();
            if (dataStr === '[DONE]') continue;

            try {
              const data = JSON.parse(dataStr);
              const delta = data.choices?.[0]?.delta?.content || data.delta?.text || '';
              if (delta) {
                assistantMsg.content += delta;
                scrollToBottom();
              }
              if (data.usage) {
                latestDebug.promptTokens = data.usage.prompt_tokens || 0;
                latestDebug.completionTokens = data.usage.completion_tokens || 0;
                latestDebug.totalTokens = data.usage.total_tokens || 0;
              }
            } catch {
              // 忽略单个不完整 chunk 的解析
            }
          }
        }
      }

      if (latestDebug.totalTokens === 0) {
        latestDebug.promptTokens = Math.max(1, Math.round(content.length / 3));
        latestDebug.completionTokens = Math.max(1, Math.round(assistantMsg.content.length / 3));
        latestDebug.totalTokens = latestDebug.promptTokens + latestDebug.completionTokens;
      }
    }
  } catch (err) {
    if (err.name !== 'AbortError') {
      assistantMsg.content = `❌ [请求异常]: ${err.message}`;
      emit('toast', `沙箱请求失败: ${err.message}`, 'error');
    }
  } finally {
    assistantMsg.isGenerating = false;
    isGenerating.value = false;
    abortController = null;
    scrollToBottom();
  }
}
</script>

<style scoped>
.playground-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: calc(100vh - 120px);
}

.view-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.view-title {
  font-size: 20px;
  font-weight: 700;
  margin: 0 0 4px;
}

.view-desc {
  font-size: 13px;
  color: var(--text-muted);
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.config-bar {
  display: flex;
  align-items: center;
  gap: 20px;
  padding: 12px 18px;
  flex-wrap: wrap;
}

.config-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.config-label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 600;
}

.model-input {
  width: 220px;
  padding: 4px 10px;
  font-size: 13px;
}

.select-input {
  padding: 4px 10px;
  font-size: 13px;
  background: rgba(0, 0, 0, 0.3);
  color: var(--text-main);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
}

.range-input {
  width: 90px;
  accent-color: var(--accent-primary);
}

.switch-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-main);
  cursor: pointer;
}

.playground-layout {
  display: grid;
  grid-template-columns: 1fr 340px;
  gap: 16px;
  flex: 1;
  min-height: 0;
}

@media (max-width: 1024px) {
  .playground-layout {
    grid-template-columns: 1fr;
  }
}

.chat-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 0;
  overflow: hidden;
}

.messages-viewport {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  margin: auto;
  text-align: center;
  max-width: 540px;
  padding: 40px 20px;
}

.empty-icon {
  font-size: 42px;
  margin-bottom: 12px;
}

.empty-title {
  font-size: 17px;
  font-weight: 700;
  margin-bottom: 8px;
}

.empty-desc {
  font-size: 13px;
  color: var(--text-muted);
  line-height: 1.6;
  margin-bottom: 20px;
}

.quick-prompts {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.prompt-chip {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
  padding: 8px 14px;
  font-size: 12px;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s ease;
}

.prompt-chip:hover {
  background: rgba(147, 51, 234, 0.15);
  border-color: var(--accent-primary);
}

.message-row {
  display: flex;
  gap: 12px;
  max-width: 88%;
}

.message-row.user {
  margin-left: auto;
  flex-direction: row-reverse;
}

.avatar {
  font-size: 20px;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 50%;
  flex-shrink: 0;
}

.bubble {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  padding: 12px 16px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.message-row.user .bubble {
  background: rgba(147, 51, 234, 0.18);
  border-color: rgba(147, 51, 234, 0.4);
}

.bubble-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  font-size: 11px;
  color: var(--text-dim);
}

.role-name {
  font-weight: 600;
  color: var(--accent-primary);
}

.bubble-content {
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
}

.generating-cursor {
  display: inline-block;
  color: var(--accent-primary);
  animation: blink 1s infinite;
}

@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}

.chat-input-area {
  padding: 14px 18px;
  background: rgba(0, 0, 0, 0.3);
  border-top: 1px solid var(--border-subtle);
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.chat-textarea {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  color: var(--text-main);
  padding: 10px 14px;
  resize: none;
  outline: none;
  font-size: 13px;
}

.chat-textarea:focus {
  border-color: var(--accent-primary);
}

.input-actions {
  display: flex;
  justify-content: flex-end;
}

/* 诊断面板 */
.inspector-container {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 16px;
  overflow-y: auto;
}

.inspector-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--border-subtle);
  padding-bottom: 10px;
}

.inspector-title {
  font-size: 13px;
  font-weight: 700;
}

.inspector-empty {
  color: var(--text-dim);
  font-size: 12px;
  text-align: center;
  padding: 40px 10px;
  line-height: 1.6;
}

.inspector-body {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.inspector-kpis {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.kpi-box {
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  padding: 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.kpi-label {
  font-size: 11px;
  color: var(--text-dim);
}

.kpi-val {
  font-size: 16px;
  font-weight: 700;
  font-family: var(--font-mono);
}

.diagnostic-group {
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.group-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-muted);
}

.diag-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.diag-label {
  font-size: 11px;
  color: var(--text-dim);
}

.diag-val {
  font-size: 12px;
  color: var(--text-main);
  word-break: break-all;
}

.diag-grid {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 6px;
}

.diag-mini-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: rgba(0, 0, 0, 0.2);
  padding: 6px 8px;
  border-radius: 4px;
}

.mini-label {
  font-size: 10px;
  color: var(--text-dim);
}

.mini-val {
  font-size: 12px;
  font-weight: 700;
  font-family: var(--font-mono);
}

.code-fold-section {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.fold-header {
  font-size: 11px;
  color: var(--text-muted);
  cursor: pointer;
  user-select: none;
}

.fold-header:hover {
  color: var(--text-main);
}

.json-code {
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  padding: 8px;
  font-size: 11px;
  max-height: 200px;
  overflow-y: auto;
  color: #a5b4fc;
}
</style>
