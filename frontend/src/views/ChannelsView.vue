<template>
  <div class="channels-view">
    <!-- 头部工具栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <h2 class="section-title">渠道与故障转移优先级配置</h2>
        <p class="section-subtitle">
          支持客户端专属分组（Claude / Codex 隔离）、单渠道多 Key 容灾、模型别名自动重命名映射。
        </p>
      </div>

      <div class="toolbar-actions">
        <button class="btn btn-secondary" @click="testAllChannels" :disabled="testingAll">
          <span v-if="testingAll">⏳ 正在测试...</span>
          <span v-else>🔍 批量探测连通性</span>
        </button>
        <button class="btn btn-secondary" @click="exportChannelsConfig" :disabled="channels.length === 0" title="导出全量渠道配置 JSON 备份">
          <span>💾 备份导出</span>
        </button>
        <button class="btn btn-secondary" @click="triggerFileInput" title="从 JSON 备份文件导入渠道配置">
          <span>📥 导入配置</span>
        </button>
        <input 
          type="file" 
          ref="fileInputRef" 
          accept=".json" 
          style="display: none" 
          @change="handleFileImport" 
        />
        <button class="btn btn-primary" @click="openAddModal">
          <span>➕ 添加新渠道</span>
        </button>
      </div>
    </div>

    <!-- 分组过滤 Tab -->
    <div class="group-filter-bar">
      <span class="group-filter-label">客户端分组筛选:</span>
      <div class="filter-tabs">
        <button 
          v-for="tab in groupTabs" 
          :key="tab.id"
          :class="['filter-tab-btn', { active: activeGroupTab === tab.id }]"
          @click="activeGroupTab = tab.id"
        >
          <span>{{ tab.label }}</span>
          <span class="tab-count-badge">{{ getGroupCount(tab.id) }}</span>
        </button>
      </div>
    </div>

    <!-- 渠道列表 -->
    <div v-if="filteredChannels.length === 0" class="glass-card empty-card">
      <div class="empty-text">当前筛选分组下暂无配置渠道，请点击上方“添加新渠道”</div>
      <button class="btn btn-primary" @click="openAddModal">➕ 立即添加</button>
    </div>

    <div v-else class="channel-cards-list">
      <div 
        v-for="(channel, index) in filteredChannels" 
        :key="channel.id" 
        :class="['glass-card', 'channel-card', { disabled: !channel.isEnabled, primary: index === 0 && channel.isEnabled }]"
      >
        <div class="card-left">
          <!-- 优先级与排序调整 -->
          <div class="priority-box">
            <div class="priority-badge" :title="`优先级 #${channel.priority}`">
              <span class="priority-num">#{{ index + 1 }}</span>
              <span class="priority-label">{{ getPriorityLabel(index) }}</span>
            </div>
            <div class="priority-arrows">
              <button 
                class="arrow-btn" 
                :disabled="index === 0" 
                @click="movePriority(index, -1)" 
                title="上移（提高优先级）"
              >
                ▲
              </button>
              <button 
                class="arrow-btn" 
                :disabled="index === filteredChannels.length - 1" 
                @click="movePriority(index, 1)" 
                title="下移（降低优先级）"
              >
                ▼
              </button>
            </div>
          </div>

          <!-- 渠道核心信息 -->
          <div class="channel-info">
            <div class="channel-title-row">
              <span class="channel-name">{{ channel.name }}</span>

              <!-- 分组标签 -->
              <span v-if="channel.group === 'claude'" class="badge badge-info">🎯 Claude 专用</span>
              <span v-else-if="channel.group === 'codex'" class="badge badge-warning">🎯 Codex 专用</span>
              <span v-else-if="channel.group && channel.group !== 'all'" class="badge badge-info">🏷️ {{ channel.group }}</span>
              <span v-else class="badge badge-muted">🌐 通用</span>

              <!-- 模型映射标签 -->
              <span v-if="channel.modelMapping" class="badge badge-success" :title="`模型映射规则:\n${channel.modelMapping}`">
                🔄 模型别名映射
              </span>

              <!-- 独立上游代理标签 -->
              <span v-if="channel.proxyUrl" class="badge badge-info font-mono" :title="`该渠道上游网络代理: ${channel.proxyUrl}`">
                🌐 代理: {{ channel.proxyUrl }}
              </span>

              <!-- 智能熔断冷却标签 -->
              <span v-if="channel.isCircuitBroken" class="badge badge-warning font-mono" :title="`该渠道因连续报错已触发智能熔断冷却，剩余 ${channel.circuitBreakerRemainingSeconds} 秒`">
                ⚡ 智能熔断冷却中 ({{ channel.circuitBreakerRemainingSeconds }}s)
              </span>
              <span v-else-if="channel.failCount > 0" class="badge badge-danger">
                连续失败 {{ channel.failCount }} 次
              </span>
              <span v-if="channel.lastFailureReason" class="badge badge-warning" :title="channel.lastFailureReason">
                {{ channel.lastFailureReason }}
              </span>
              <span v-if="testResults[channel.id]" :class="['badge', testResults[channel.id].success ? 'badge-success' : 'badge-danger']">
                {{ testResults[channel.id].message }}
              </span>
            </div>

            <div class="channel-details-grid">
              <div class="detail-item client-endpoint-highlight">
                <span class="detail-label endpoint-label">👉 本地调用 Base URL:</span>
                <div class="endpoint-val-box">
                  <span class="code-tag font-mono highlight-url">{{ getClientEndpoint(channel) }}</span>
                  <button class="icon-btn" @click="copyText(getClientEndpoint(channel), '本地调用 Base URL 已复制')" title="复制此渠道在 VSCode/Cursor/插件中配置的本地 Base URL">
                    📋
                  </button>
                </div>
              </div>
              <div class="detail-item">
                <span class="detail-label">上游服务 Base URL:</span>
                <span class="code-tag">{{ channel.baseUrl }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">API Key:</span>
                <div class="key-container">
                  <span v-if="getApiKeyList(channel.apiKey).length > 1" class="badge badge-info">
                    🔑 {{ getApiKeyList(channel.apiKey).length }} 个 Key (键级容灾)
                  </span>
                  <span class="code-tag">{{ showKeyMap[channel.id] ? channel.apiKey : maskApiKeys(channel.apiKey) }}</span>
                  <button class="icon-btn" @click="toggleShowKey(channel.id)" title="显隐 API Key">
                    {{ showKeyMap[channel.id] ? '👁️' : '🔒' }}
                  </button>
                  <button class="icon-btn" @click="copyText(channel.apiKey, 'API Key 已复制到剪贴板')" title="仅复制 API Key 文本">
                    📋
                  </button>
                </div>
              </div>
              <div class="detail-item">
                <span class="detail-label">匹配模型:</span>
                <span class="code-tag">{{ channel.models || '*' }}</span>
              </div>
              <div v-if="channel.customHeaders" class="detail-item">
                <span class="detail-label">自定义头:</span>
                <span class="badge badge-warning" :title="channel.customHeaders">
                  🎭 {{ getHeaderCount(channel.customHeaders) }} 条规则 (含客户端伪装/提取)
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- 卡片右侧：状态开关与操作 -->
        <div class="card-right">
          <div class="switch-box" title="启用/禁用当前渠道">
            <label class="switch">
              <input type="checkbox" :checked="channel.isEnabled" @change="toggleChannel(channel, $event)">
              <span class="slider"></span>
            </label>
            <span class="switch-label">{{ channel.isEnabled ? '已启用' : '已停用' }}</span>
          </div>

          <div class="action-buttons">
            <button 
              class="btn btn-secondary btn-sm" 
              @click="testSingleChannel(channel)"
              :disabled="testingChannelId === channel.id"
            >
              <span v-if="testingChannelId === channel.id">⏳ 探测中...</span>
              <span v-else>🔍 探测</span>
            </button>
            <button 
              class="btn btn-secondary btn-sm" 
              @click="cloneChannelDirectly(channel)" 
              :disabled="cloningChannelId === channel.id"
              title="一键完整克隆该渠道的所有配置（BaseURL/Key/请求头/别名映射等）并直接生成新渠道"
            >
              <span v-if="cloningChannelId === channel.id">⏳ 克隆中...</span>
              <span v-else>📑 克隆渠道</span>
            </button>
            <button class="btn btn-secondary btn-sm" @click="openEditModal(channel)">
              ✏️ 编辑
            </button>
            <button class="btn btn-danger btn-sm" @click="confirmDelete(channel)">
              🗑️ 删除
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- 添加/编辑渠道弹窗 Modal -->
    <div v-if="showModal" class="modal-overlay" @click.self="closeModal">
      <div class="glass-card modal-container">
        <div class="modal-header">
          <h3 class="modal-title">{{ isEdit ? '编辑渠道配置' : '添加新模型渠道' }}</h3>
          <button class="close-btn" @click="closeModal">✕</button>
        </div>

        <form class="modal-body" @submit.prevent="saveChannel">
          <div class="form-group">
            <label class="form-label">渠道显示名称 <span class="required">*</span></label>
            <input 
              v-model="form.name" 
              class="form-input" 
              placeholder="例如：agentrouter / 主力Claude中转 / 备用Codex" 
              required 
            />
          </div>

          <!-- 分组选择 -->
          <div class="form-group">
            <label class="form-label">所属客户端分组</label>
            <select v-model="form.groupSelect" class="form-input">
              <option value="all">🌐 通用渠道 (Claude / Codex / 所有客户端均可使用)</option>
              <option value="claude">🎯 仅限 Claude (Claude Code / VSCode Claude 插件)</option>
              <option value="codex">🎯 仅限 CodeX (仅供 CodeX 插件专属使用)</option>
              <option value="custom">🏷️ 自定义专属分组标识...</option>
            </select>
          </div>

          <div v-if="form.groupSelect === 'custom'" class="form-group">
            <label class="form-label">自定义分组名称 <span class="required">*</span></label>
            <input 
              v-model="form.customGroup" 
              class="form-input font-mono" 
              placeholder="例如：Copilot / my-special-group" 
              required 
            />
          </div>

          <!-- 实时客户端调用地址提示框 -->
          <div class="group-endpoint-preview">
            <div class="preview-title">💡 客户端（VSCode / Cursor / Continue）中配置的 Base URL：</div>
            <div class="preview-row">
              <span class="code-tag font-mono highlight-url">{{ getFormClientEndpoint() }}</span>
              <button type="button" class="btn btn-secondary btn-xs" @click="copyText(getFormClientEndpoint(), '客户端 Base URL 已复制')">
                📋 复制调用地址
              </button>
            </div>
          </div>

          <div class="form-group">
            <label class="form-label">上游 Base URL (服务商接口地址) <span class="required">*</span></label>
            <input 
              v-model="form.baseUrl" 
              class="form-input font-mono" 
              placeholder="例如：https://agentrouter.org/v1 或 https://api.anthropic.com" 
              required 
            />
            <span class="form-tip">支持官方 API 地址或任意第三方中转站 /v1 地址。</span>
          </div>

          <div class="form-group">
            <div class="form-label-row">
              <label class="form-label">API Key（支持配置多个，每行一个） <span class="required">*</span></label>
              <span v-if="formKeyCount > 1" class="badge badge-info">已填入 {{ formKeyCount }} 个 Key (键级容灾)</span>
            </div>
            <textarea 
              v-model="form.apiKey" 
              class="form-input font-mono key-textarea" 
              rows="3"
              placeholder="支持配置多个 API Key，每行一个（或逗号分隔）：&#10;sk-first-key-xxxx&#10;sk-second-key-yyyy&#10;sk-third-key-zzzz" 
              required 
            ></textarea>
            <span class="form-tip">💡 提示：若填入多个 Key，当某个 Key 出现余额不足或限流时，网关将优先在此渠道内部的备用 Key 之间自动无感重试，全部用尽后再切换到下一个渠道。</span>
          </div>

          <!-- 模型别名映射 -->
          <div class="form-group">
            <label class="form-label">模型别名重命名映射 (每行一条：客户端模型 => 上游目标模型)</label>
            <textarea 
              v-model="form.modelMapping" 
              class="form-input font-mono key-textarea" 
              rows="2"
              placeholder="例如：&#10;claude-3-7-sonnet => gpt-5.6-sol&#10;claude-3-5-sonnet-20241022 => claude-3-5-sonnet&#10;* => gpt-5.6-sol" 
            ></textarea>
            <span class="form-tip">💡 提示：当客户端发起请求时，网关将在发往上游前自动把客户端的模型名重写为上游中转站指定的模型名。</span>
          </div>

          <!-- 自定义请求头与客户端伪装/提取 -->
          <div class="form-group">
            <div class="form-label-row">
              <label class="form-label">自定义请求头与客户端伪装 (每行一条 Header: Value)</label>
              <div class="preset-badge-group">
                <button type="button" class="btn-xs btn-preset" @click="applyHeaderPreset('codex')">🎭 模拟 Codex 专享头</button>
                <button type="button" class="btn-xs btn-preset" @click="applyHeaderPreset('openai_org')">🏢 OpenAI 组织</button>
                <button type="button" class="btn-xs btn-preset" @click="applyHeaderPreset('anthropic_cache')">⚡ Anthropic 缓存</button>
                <button type="button" class="btn-xs btn-preset" @click="applyHeaderPreset('extract_template')">📥 客户端提取模板</button>
              </div>
            </div>
            <textarea 
              v-model="form.customHeaders" 
              class="form-input font-mono key-textarea" 
              rows="3"
              placeholder="例如：&#10;User-Agent: GithubCopilot/1.155.0 (VSCode/1.90.0)&#10;Editor-Version: vscode/1.90.0&#10;Openai-Intent: conversation-panel&#10;X-Custom-Tenant: {header:X-Tenant-Key:-default}&#10;Authorization: Bearer {header:Authorization:-{apiKey}}" 
            ></textarea>
            <div class="header-help-toggle" @click="showSyntaxHelp = !showSyntaxHelp">
              <span>{{ showSyntaxHelp ? '▼ 收起动态变量提取语法说明' : '▶ 查看动态变量提取语法说明 ({header:xxx}, {uuid}, {apiKey}...)' }}</span>
            </div>
            <div v-if="showSyntaxHelp" class="syntax-help-box">
              <div class="syntax-grid">
                <div><code>{header:X-Name}</code>: 提取客户端传入的指定请求头</div>
                <div><code>{header:X-Name:-default}</code>: 提取请求头，未传时回退默认值</div>
                <div><code>{uuid}</code>: 自动生成唯一的 32 位请求 GUID</div>
                <div><code>{apiKey}</code>: 当前渠道正在尝试的有效 API Key</div>
                <div><code>{model}</code>: 目标调用的模型名称</div>
                <div><code>{client_ip}</code>: 客户端真实 IP 地址</div>
                <div><code>{timestamp}</code>: 当前 Unix 毫秒时间戳</div>
              </div>
            </div>
            <span class="form-tip">💡 提示：用于突破上游仅允许特定客户端（如 Codex/Copilot）调用的限制，或从客户端请求中提取指定 Header 转发给上游。</span>
          </div>

          <div class="form-group">
            <label class="form-label">支持模型 (通配符或逗号分隔)</label>
            <input 
              v-model="form.models" 
              class="form-input font-mono" 
              placeholder="默认填 * 代表支持所有模型，或填 gpt-5.6-sol,claude-3-7-sonnet" 
            />
          </div>

          <!-- 上游网络代理配置 -->
          <div class="form-group">
            <div class="form-label-row">
              <label class="form-label">上游网络代理 (可选，用于官方API或需翻墙渠道)</label>
              <div class="preset-badge-group">
                <button type="button" class="btn-xs btn-preset" @click="form.proxyUrl = 'http://127.0.0.1:7890'">Clash (7890)</button>
                <button type="button" class="btn-xs btn-preset" @click="form.proxyUrl = 'http://127.0.0.1:10808'">v2ray (10808)</button>
                <button type="button" class="btn-xs btn-preset" @click="form.proxyUrl = 'http://127.0.0.1:7897'">Verge (7897)</button>
                <button type="button" class="btn-xs btn-preset" @click="form.proxyUrl = ''">清空直连</button>
              </div>
            </div>
            <input 
              v-model="form.proxyUrl" 
              class="form-input font-mono" 
              placeholder="例如：http://127.0.0.1:7890 或 socks5://127.0.0.1:10808 (留空为直接连接)" 
            />
            <span class="form-tip">💡 提示：若配置了代理，发往该渠道的连通性测试与模型请求均自动走此代理，国内中转站与海外官方渠道可无缝混用。</span>
          </div>

          <div class="form-row">
            <div class="form-group flex-1">
              <label class="form-label">优先级 (数值越小越靠前)</label>
              <input v-model.number="form.priority" type="number" class="form-input font-mono" min="1" />
            </div>
            <div class="form-group flex-1 flex-center-switch">
              <label class="form-label">是否立即启用</label>
              <label class="switch">
                <input type="checkbox" v-model="form.isEnabled">
                <span class="slider"></span>
              </label>
            </div>
          </div>

          <!-- 弹窗内即时测试结果 -->
          <div v-if="modalTestResult" :class="['test-result-box', modalTestResult.success ? 'test-success' : 'test-failed']">
            <span>{{ modalTestResult.success ? '✅' : '❌' }} {{ modalTestResult.message }}</span>
          </div>

          <div class="modal-footer">
            <button 
              type="button" 
              class="btn btn-secondary" 
              @click="testFormChannel"
              :disabled="testingModal || !form.baseUrl || !form.apiKey"
            >
              <span v-if="testingModal">⏳ 测试中...</span>
              <span v-else>🔍 探测此配置</span>
            </button>
            <div class="footer-right-actions">
              <button type="button" class="btn btn-secondary" @click="cancelModal">取消</button>
              <button type="submit" class="btn btn-primary" :disabled="saving">
                <span v-if="saving">保存中...</span>
                <span v-else>💾 保存渠道</span>
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed } from 'vue';
import { api } from '../api';

const props = defineProps({
  channels: {
    type: Array,
    required: true
  }
});

const emit = defineEmits(['refresh', 'toast']);

const showKeyMap = reactive({});
const testResults = reactive({});
const testingChannelId = ref(null);
const testingAll = ref(false);

const activeGroupTab = ref('all_groups');
const groupTabs = [
  { id: 'all_groups', label: '全部渠道' },
  { id: 'claude', label: '🎯 Claude 专用' },
  { id: 'codex', label: '🎯 Codex 专用' },
  { id: 'all', label: '🌐 通用渠道' }
];

function getGroupCount(tabId) {
  if (tabId === 'all_groups') return props.channels.length;
  if (tabId === 'claude') return props.channels.filter(c => (c.group || 'all').toLowerCase() === 'claude').length;
  if (tabId === 'codex') return props.channels.filter(c => (c.group || 'all').toLowerCase() === 'codex').length;
  if (tabId === 'all') return props.channels.filter(c => (c.group || 'all').toLowerCase() === 'all').length;
  return props.channels.filter(c => (c.group || '').toLowerCase() === tabId.toLowerCase()).length;
}

const filteredChannels = computed(() => {
  if (activeGroupTab.value === 'all_groups') {
    return props.channels;
  }
  if (activeGroupTab.value === 'claude') {
    return props.channels.filter(c => {
      const g = (c.group || 'all').toLowerCase();
      return g === 'claude' || g === 'all';
    });
  }
  if (activeGroupTab.value === 'codex') {
    return props.channels.filter(c => {
      const g = (c.group || 'all').toLowerCase();
      return g === 'codex' || g === 'all';
    });
  }
  return props.channels.filter(c => (c.group || 'all').toLowerCase() === activeGroupTab.value.toLowerCase());
});

const showModal = ref(false);
const isEdit = ref(false);
const saving = ref(false);
const testingModal = ref(false);
const modalTestResult = ref(null);
const showSyntaxHelp = ref(false);
const cloningChannelId = ref(null);

const form = reactive({
  id: '',
  name: '',
  groupSelect: 'all',
  customGroup: '',
  baseUrl: '',
  apiKey: '',
  proxyUrl: '',
  modelMapping: '',
  customHeaders: '',
  models: '*',
  priority: 1,
  isEnabled: true
});

function getApiKeyList(apiKeyStr) {
  if (!apiKeyStr) return [];
  return apiKeyStr.split(/[\r\n,;]+/)
    .map(k => k.trim())
    .filter(k => k.length > 0);
}

function getHeaderCount(headersStr) {
  if (!headersStr) return 0;
  return headersStr.split(/[\r\n]+/)
    .map(l => l.trim())
    .filter(l => l.length > 0 && l.includes(':')).length;
}

function getClientEndpoint(channel) {
  const origin = window.location.origin || 'http://127.0.0.1:5000';
  const g = (channel?.group || 'all').toLowerCase();
  if (g === 'all' || !g) {
    return `${origin}/v1`;
  }
  if (g === 'claude') {
    return `${origin}/claude`;
  }
  if (g === 'codex') {
    return `${origin}/codex/v1`;
  }
  return `${origin}/${channel.group}/v1`;
}

function getFormClientEndpoint() {
  const origin = window.location.origin || 'http://127.0.0.1:5000';
  const finalGroup = (form.groupSelect === 'custom' ? form.customGroup : form.groupSelect) || 'all';
  const g = finalGroup.toLowerCase();
  if (g === 'all' || !g) {
    return `${origin}/v1`;
  }
  if (g === 'claude') {
    return `${origin}/claude (或 ${origin}/claude/v1)`;
  }
  if (g === 'codex') {
    return `${origin}/codex/v1`;
  }
  return `${origin}/${finalGroup}/v1`;
}

function applyHeaderPreset(type) {
  if (type === 'codex') {
    const codexHeaders = [
      'User-Agent: GithubCopilot/1.155.0 (VSCode/1.90.0)',
      'Editor-Version: vscode/1.90.0',
      'Editor-Plugin-Version: copilot/1.155.0',
      'Openai-Organization: github-copilot',
      'Openai-Intent: conversation-panel',
      'X-Github-Api-Version: 2023-07-07',
      'X-Request-Id: {uuid}',
      'Accept: application/json, text/event-stream'
    ].join('\n');
    form.customHeaders = form.customHeaders && form.customHeaders.trim() 
      ? form.customHeaders.trim() + '\n' + codexHeaders 
      : codexHeaders;
    emit('toast', '已载入 Codex / Copilot 官方客户端模拟头预设', 'success');
  } else if (type === 'openai_org') {
    const org = 'OpenAI-Organization: org-your-org-id\nOpenAI-Project: proj-your-proj-id';
    form.customHeaders = form.customHeaders && form.customHeaders.trim() 
      ? form.customHeaders.trim() + '\n' + org 
      : org;
    emit('toast', '已添加 OpenAI 组织与项目头模板', 'info');
  } else if (type === 'anthropic_cache') {
    const cache = 'anthropic-version: 2023-06-01\nanthropic-beta: prompt-caching-2024-07-25';
    form.customHeaders = form.customHeaders && form.customHeaders.trim() 
      ? form.customHeaders.trim() + '\n' + cache 
      : cache;
    emit('toast', '已添加 Anthropic 缓存头模板', 'info');
  } else if (type === 'extract_template') {
    const extract = 'X-Forwarded-User: {header:X-User-Id:-anonymous}\nAuthorization: Bearer {header:Authorization:-{apiKey}}';
    form.customHeaders = form.customHeaders && form.customHeaders.trim() 
      ? form.customHeaders.trim() + '\n' + extract 
      : extract;
    emit('toast', '已添加客户端 Header 动态提取示例', 'info');
  }
}

const formKeyCount = computed(() => {
  return getApiKeyList(form.apiKey).length;
});

function getPriorityLabel(index) {
  if (index === 0) return '主力首选';
  if (index === 1) return '备用备选';
  if (index === 2) return '二级兜底';
  return '级联备用';
}

function maskSingleKey(key) {
  if (!key || key.length < 8) return '********';
  return `${key.slice(0, 5)}...${key.slice(-4)}`;
}

function maskApiKeys(apiKeyStr) {
  const keys = getApiKeyList(apiKeyStr);
  if (keys.length === 0) return '********';
  if (keys.length === 1) return maskSingleKey(keys[0]);
  return `${maskSingleKey(keys[0])} (+${keys.length - 1} 个备用 Key)`;
}

function toggleShowKey(channelId) {
  showKeyMap[channelId] = !showKeyMap[channelId];
}

async function copyText(text, successMsg) {
  try {
    await navigator.clipboard.writeText(text);
    emit('toast', successMsg, 'success');
  } catch (err) {
    emit('toast', '复制失败，请手动选择', 'error');
  }
}

async function movePriority(currentIndex, direction) {
  const targetIndex = currentIndex + direction;
  if (targetIndex < 0 || targetIndex >= filteredChannels.value.length) return;

  const newOrder = [...props.channels];
  const itemA = filteredChannels.value[currentIndex];
  const itemB = filteredChannels.value[targetIndex];

  const idxA = newOrder.findIndex(c => c.id === itemA.id);
  const idxB = newOrder.findIndex(c => c.id === itemB.id);

  if (idxA !== -1 && idxB !== -1) {
    const temp = newOrder[idxA];
    newOrder[idxA] = newOrder[idxB];
    newOrder[idxB] = temp;

    const orderedIds = newOrder.map(c => c.id);
    try {
      await api.reorderChannels(orderedIds);
      emit('toast', '渠道优先级已更新，即刻生效', 'success');
      emit('refresh');
    } catch (err) {
      emit('toast', `调整优先级失败: ${err.message}`, 'error');
    }
  }
}

async function toggleChannel(channel, event) {
  const isEnabled = event.target.checked;
  try {
    await api.toggleChannel(channel.id, isEnabled);
    emit('toast', isEnabled ? `渠道 [${channel.name}] 已启用` : `渠道 [${channel.name}] 已停用`, 'info');
    emit('refresh');
  } catch (err) {
    emit('toast', `切换状态失败: ${err.message}`, 'error');
  }
}

async function testSingleChannel(channel) {
  testingChannelId.value = channel.id;
  try {
    const result = await api.testChannel(channel);
    testResults[channel.id] = result;
    if (result.success) {
      emit('toast', `[${channel.name}] 探测成功 (${result.latencyMs}ms)`, 'success');
    } else {
      emit('toast', `[${channel.name}] 探测未通过: ${result.message}`, 'warning');
    }
  } catch (err) {
    testResults[channel.id] = { success: false, message: err.message };
    emit('toast', `[${channel.name}] 探测异常: ${err.message}`, 'error');
  } finally {
    testingChannelId.value = null;
  }
}

async function testAllChannels() {
  testingAll.value = true;
  emit('toast', '开始批量探测所有渠道...', 'info');
  for (const ch of props.channels) {
    await testSingleChannel(ch);
  }
  testingAll.value = false;
  emit('toast', '所有渠道探测完成', 'success');
}

// 暂存未提交的新建渠道草稿数据（避免误触关闭丢失）
const addDraftForm = reactive({
  name: '',
  groupSelect: 'all',
  customGroup: '',
  baseUrl: '',
  apiKey: '',
  proxyUrl: '',
  modelMapping: '',
  customHeaders: '',
  models: '*',
  priority: null,
  isEnabled: true
});

function syncAddDraft() {
  if (!isEdit.value) {
    Object.assign(addDraftForm, {
      name: form.name,
      groupSelect: form.groupSelect,
      customGroup: form.customGroup,
      baseUrl: form.baseUrl,
      apiKey: form.apiKey,
      proxyUrl: form.proxyUrl,
      modelMapping: form.modelMapping,
      customHeaders: form.customHeaders,
      models: form.models,
      priority: form.priority,
      isEnabled: form.isEnabled
    });
  }
}

function resetAddDraft() {
  Object.assign(addDraftForm, {
    name: '',
    groupSelect: 'all',
    customGroup: '',
    baseUrl: '',
    apiKey: '',
    proxyUrl: '',
    modelMapping: '',
    customHeaders: '',
    models: '*',
    priority: null,
    isEnabled: true
  });
}

function openAddModal() {
  isEdit.value = false;
  form.id = '';
  // 还原已填写的草稿内容
  form.name = addDraftForm.name || '';
  form.groupSelect = addDraftForm.groupSelect || 'all';
  form.customGroup = addDraftForm.customGroup || '';
  form.baseUrl = addDraftForm.baseUrl || '';
  form.apiKey = addDraftForm.apiKey || '';
  form.proxyUrl = addDraftForm.proxyUrl || '';
  form.modelMapping = addDraftForm.modelMapping || '';
  form.customHeaders = addDraftForm.customHeaders || '';
  form.models = addDraftForm.models || '*';
  form.priority = addDraftForm.priority || (props.channels.length + 1);
  form.isEnabled = addDraftForm.isEnabled !== false;
  modalTestResult.value = null;
  showModal.value = true;
}

function openEditModal(channel) {
  isEdit.value = true;
  form.id = channel.id;
  form.name = channel.name;
  
  const g = (channel.group || 'all').toLowerCase();
  if (g === 'all' || g === 'claude' || g === 'codex') {
    form.groupSelect = g;
    form.customGroup = '';
  } else {
    form.groupSelect = 'custom';
    form.customGroup = channel.group || '';
  }

  form.baseUrl = channel.baseUrl;
  form.apiKey = channel.apiKey;
  form.proxyUrl = channel.proxyUrl || '';
  form.modelMapping = channel.modelMapping || '';
  form.customHeaders = channel.customHeaders || '';
  form.models = channel.models || '*';
  form.priority = channel.priority;
  form.isEnabled = channel.isEnabled;
  modalTestResult.value = null;
  showModal.value = true;
}

function generateCloneName(originalName) {
  const existingNames = new Set(props.channels.map(c => (c.name || '').trim().toLowerCase()));
  let candidate = `${originalName} (副本)`;
  if (!existingNames.has(candidate.toLowerCase())) {
    return candidate;
  }
  let index = 2;
  while (existingNames.has(`${originalName} (副本${index})`.toLowerCase())) {
    index++;
  }
  return `${originalName} (副本${index})`;
}

async function cloneChannelDirectly(channel) {
  cloningChannelId.value = channel.id;
  try {
    const clonedName = generateCloneName(channel.name);
    const payload = {
      name: clonedName,
      group: channel.group || 'all',
      baseUrl: channel.baseUrl,
      apiKey: channel.apiKey,
      proxyUrl: channel.proxyUrl || '',
      modelMapping: channel.modelMapping || '',
      customHeaders: channel.customHeaders || '',
      models: channel.models || '*',
      priority: props.channels.length + 1,
      isEnabled: true
    };
    await api.createChannel(payload);
    emit('toast', `🎉 已成功克隆整条渠道配置为 [${clonedName}] 并即刻生效！`, 'success');
    emit('refresh');
  } catch (err) {
    emit('toast', `克隆渠道失败: ${err.message}`, 'error');
  } finally {
    cloningChannelId.value = null;
  }
}

// 备份导出与导入还原逻辑
const fileInputRef = ref(null);

function exportChannelsConfig() {
  const url = api.exportChannelsUrl();
  window.open(url, '_blank');
  emit('toast', '💾 正在下载全量渠道配置 JSON 备份文件...', 'info');
}

function triggerFileInput() {
  if (fileInputRef.value) {
    fileInputRef.value.click();
  }
}

async function handleFileImport(event) {
  const file = event.target.files?.[0];
  if (!file) return;

  try {
    const text = await file.text();
    const parsed = JSON.parse(text);
    const channelList = Array.isArray(parsed) ? parsed : (parsed.data || parsed.channels || []);

    if (!Array.isArray(channelList) || channelList.length === 0) {
      emit('toast', '未能从 JSON 文件解析到有效渠道配置', 'error');
      return;
    }

    const overwrite = confirm(
      `检测到包含 ${channelList.length} 个渠道的备份配置。\n\n` +
      `【确定】: 追加合并至现有渠道 (推荐)\n` +
      `【取消】: 放弃本次导入`
    );

    if (!overwrite) {
      return;
    }

    await api.importChannels(channelList, 'append');
    emit('toast', `🎉 成功导入 ${channelList.length} 个渠道配置！`, 'success');
    emit('refresh');
  } catch (err) {
    emit('toast', `导入失败: ${err.message}`, 'error');
  } finally {
    event.target.value = '';
  }
}

function closeModal() {
  // 非取消/非保存关闭（例如点遮罩或点右上角✕）：暂存草稿
  syncAddDraft();
  showModal.value = false;
}

function cancelModal() {
  // 用户明确点击「取消」：主动清空草稿
  if (!isEdit.value) {
    resetAddDraft();
  }
  showModal.value = false;
}

async function testFormChannel() {
  testingModal.value = true;
  try {
    const finalGroup = form.groupSelect === 'custom' ? form.customGroup : form.groupSelect;
    const result = await api.testChannel({
      name: form.name,
      group: finalGroup,
      baseUrl: form.baseUrl,
      apiKey: form.apiKey,
      proxyUrl: form.proxyUrl,
      models: form.models,
      modelMapping: form.modelMapping,
      customHeaders: form.customHeaders
    });
    modalTestResult.value = result;
  } catch (err) {
    modalTestResult.value = { success: false, message: err.message };
  } finally {
    testingModal.value = false;
  }
}

async function saveChannel() {
  saving.value = true;
  try {
    const finalGroup = form.groupSelect === 'custom' ? form.customGroup : form.groupSelect;
    const payload = {
      id: form.id,
      name: form.name,
      group: finalGroup,
      baseUrl: form.baseUrl,
      apiKey: form.apiKey,
      proxyUrl: form.proxyUrl,
      modelMapping: form.modelMapping,
      customHeaders: form.customHeaders,
      models: form.models,
      priority: form.priority,
      isEnabled: form.isEnabled
    };

    if (isEdit.value) {
      await api.updateChannel(form.id, payload);
      emit('toast', '渠道信息已更新', 'success');
    } else {
      await api.createChannel(payload);
      emit('toast', '新渠道已成功添加并生效', 'success');
      resetAddDraft(); // 保存成功后清空草稿
    }
    showModal.value = false;
    emit('refresh');
  } catch (err) {
    emit('toast', `保存失败: ${err.message}`, 'error');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete(channel) {
  if (confirm(`确定要删除渠道 [${channel.name}] 吗？`)) {
    try {
      await api.deleteChannel(channel.id);
      emit('toast', '渠道已删除', 'info');
      emit('refresh');
    } catch (err) {
      emit('toast', `删除失败: ${err.message}`, 'error');
    }
  }
}
</script>

<style scoped>
.channels-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  flex-wrap: wrap;
}

.section-title {
  font-size: 18px;
  font-weight: 700;
}

.section-subtitle {
  font-size: 13px;
  color: var(--text-muted);
  margin-top: 4px;
}

.toolbar-actions {
  display: flex;
  gap: 12px;
}

/* 分组过滤 Bar */
.group-filter-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.group-filter-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-muted);
}

.filter-tabs {
  display: flex;
  gap: 6px;
  background: rgba(0, 0, 0, 0.2);
  padding: 4px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
}

.filter-tab-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 500;
  background: transparent;
  border: none;
  color: var(--text-muted);
  border-radius: var(--radius-sm);
  cursor: pointer;
  transition: all 0.15s;
}

.filter-tab-btn.active {
  background: var(--accent-primary);
  color: #fff;
  font-weight: 600;
  box-shadow: 0 2px 6px var(--accent-glow);
}

.tab-count-badge {
  background: rgba(0, 0, 0, 0.25);
  padding: 1px 6px;
  border-radius: var(--radius-full);
  font-size: 11px;
}

.empty-card {
  padding: 40px;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
}

.empty-text {
  color: var(--text-muted);
}

.channel-cards-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.channel-card {
  padding: 16px 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
}

.channel-card.primary {
  border-color: rgba(99, 102, 241, 0.4);
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.08), var(--bg-card));
}

.channel-card.disabled {
  opacity: 0.6;
  filter: grayscale(40%);
}

.card-left {
  display: flex;
  align-items: center;
  gap: 20px;
  flex: 1;
}

.priority-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.priority-badge {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: rgba(99, 102, 241, 0.15);
  border: 1px solid var(--border-active);
  border-radius: var(--radius-md);
  width: 60px;
  height: 52px;
}

.priority-num {
  font-family: var(--font-mono);
  font-size: 16px;
  font-weight: 800;
  color: var(--accent-primary);
}

.priority-label {
  font-size: 10px;
  color: var(--text-muted);
}

.priority-arrows {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.arrow-btn {
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: 4px;
  color: var(--text-muted);
  width: 24px;
  height: 22px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 10px;
  transition: all 0.15s;
}

.arrow-btn:hover:not(:disabled) {
  background: var(--accent-primary);
  color: #fff;
}

.arrow-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.channel-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
  flex: 1;
}

.channel-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.channel-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-main);
}

.channel-details-grid {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  font-size: 12px;
}

.detail-item {
  display: flex;
  align-items: center;
  gap: 6px;
}

.detail-label {
  color: var(--text-dim);
}

.key-container {
  display: flex;
  align-items: center;
  gap: 4px;
}

.icon-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 12px;
  padding: 2px 4px;
  border-radius: 4px;
}

.icon-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.card-right {
  display: flex;
  align-items: center;
  gap: 20px;
}

.switch-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.switch-label {
  font-size: 11px;
  color: var(--text-muted);
}

.action-buttons {
  display: flex;
  gap: 8px;
}

/* Modal 弹窗 */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0, 0, 0, 0.65);
  backdrop-filter: blur(8px);
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

.modal-container {
  width: 100%;
  max-width: 600px;
  max-height: 90vh;
  overflow-y: auto;
  padding: 24px;
  background: var(--bg-surface);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--border-subtle);
  animation: modal-scale 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes modal-scale {
  from { opacity: 0; transform: scale(0.95); }
  to { opacity: 1; transform: scale(1); }
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.modal-title {
  font-size: 17px;
  font-weight: 700;
}

.close-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 16px;
  cursor: pointer;
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-label-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}

.key-textarea {
  resize: vertical;
  min-height: 64px;
  line-height: 1.4;
}

.required {
  color: var(--danger);
}

.form-tip {
  font-size: 11px;
  color: var(--text-dim);
  margin-top: 4px;
  line-height: 1.4;
}

.form-row {
  display: flex;
  gap: 16px;
  align-items: center;
}

.flex-1 { flex: 1; }

.flex-center-switch {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 6px;
}

.font-mono {
  font-family: var(--font-mono);
}

.test-result-box {
  padding: 10px 14px;
  border-radius: var(--radius-md);
  font-size: 12px;
  font-family: var(--font-mono);
}

.test-success { background: var(--success-bg); color: var(--success); }
.test-failed { background: var(--danger-bg); color: var(--danger); }

.modal-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 12px;
  gap: 12px;
}

.footer-right-actions {
  display: flex;
  gap: 10px;
}

.preset-badge-group {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.btn-preset {
  background: var(--bg-hover);
  border: 1px solid var(--border-subtle);
  color: var(--text-main);
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-preset:hover {
  background: var(--primary-bg);
  color: var(--primary);
  border-color: var(--primary);
}

.header-help-toggle {
  margin-top: 4px;
  font-size: 11px;
  color: var(--primary);
  cursor: pointer;
  user-select: none;
}

.header-help-toggle:hover {
  text-decoration: underline;
}

.syntax-help-box {
  margin-top: 6px;
  padding: 8px 12px;
  background: var(--bg-surface-2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  font-size: 11px;
}

.syntax-grid {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.syntax-grid code {
  color: var(--primary);
  background: var(--bg-hover);
  padding: 1px 4px;
  border-radius: 3px;
  font-family: var(--font-mono);
}

.client-endpoint-highlight {
  grid-column: 1 / -1;
  background: rgba(99, 102, 241, 0.08);
  padding: 8px 12px;
  border-radius: var(--radius-sm);
  border: 1px solid rgba(99, 102, 241, 0.25);
  margin-bottom: 4px;
}

.endpoint-label {
  color: var(--primary) !important;
  font-weight: 700 !important;
}

.endpoint-val-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.highlight-url {
  color: #818cf8 !important;
  font-weight: 700;
  background: rgba(0, 0, 0, 0.35);
  border: 1px solid rgba(99, 102, 241, 0.3);
  padding: 3px 8px;
}

.group-endpoint-preview {
  background: var(--bg-surface-2);
  border: 1px solid rgba(99, 102, 241, 0.25);
  border-radius: var(--radius-md);
  padding: 10px 14px;
  margin-top: 4px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.preview-title {
  font-size: 12px;
  color: var(--text-muted);
}

.preview-row {
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
