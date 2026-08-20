import { ref, computed } from 'vue';

export const currentLang = ref(localStorage.getItem('gateway_language') || 'zh');

export function setLanguage(lang) {
  currentLang.value = lang;
  localStorage.setItem('gateway_language', lang);
}

export function toggleLanguage() {
  const newLang = currentLang.value === 'zh' ? 'en' : 'zh';
  setLanguage(newLang);
}

export const messages = {
  zh: {
    // 导航与头部
    nav: {
      dashboard: '仪表盘',
      channels: '渠道管理',
      tokens: 'Token 统计',
      logs: '实时日志',
      guide: '接入指引',
      settings: '一键接管 / 设置',
      running: '运行中',
      langLabel: '中 / EN'
    },
    // 仪表盘
    dashboard: {
      title: '网关概览与健康指标',
      subtitle: '监控所有渠道可用性、请求成功率及故障转移容灾情况。',
      totalChannels: '已配置渠道',
      activeChannels: '启用中渠道',
      totalRequests: '请求总吞吐',
      successRate: '请求成功率',
      failoverCount: '故障转移次数',
      primaryChannel: '当前主用渠道',
      recentFailovers: '最近故障转移追踪',
      noFailovers: '系统运行平稳，暂无故障转移事件。',
      viewAllLogs: '查看全部日志 ➔'
    },
    // 渠道管理
    channels: {
      title: '渠道与故障转移容灾管理',
      subtitle: '支持拖拽调整主备优先级、按模型别名透传重写、多 Key 自动轮询与智能容灾。',
      addChannel: '+ 新建渠道',
      editChannel: '编辑渠道',
      filterAll: '全部渠道',
      filterClaude: 'Claude 专用',
      filterCodex: 'Codex 专用',
      filterUniversal: '通用渠道',
      empty: '暂未配置任何渠道，请点击上方「+ 新建渠道」进行配置。',
      test: '测试连通性',
      testing: '测试中...',
      save: '保存渠道',
      cancel: '取消',
      deleteConfirm: '确定要删除该渠道吗？',
      keysLabel: 'API Key (支持多 Key，每行一个)',
      modelRewrite: '模型重写别名映射 (JSON 字典)',
      supportedModels: '支持的模型 (* 表示全部)',
      group: '所属分组',
      baseUrl: '接口 Base URL',
      name: '渠道名称',
      dragTip: '提示：可直接拖拽卡片调整渠道优先级排序'
    },
    // Token 统计
    tokens: {
      title: 'Token 消耗统计看板',
      totalTokens: '累计消耗 Total Tokens',
      totalTokensSub: '全渠道累计 Token 吞吐',
      promptTokens: 'Prompt 消耗 (Input)',
      completionTokens: 'Completion 消耗 (Output)',
      todayTokens: '今日消耗 (Today)',
      totalRequests: '统计记录请求数',
      dimChannel: '🏷️ 渠道维度汇总',
      dimKey: '🔑 渠道 + Key 细分明细',
      filterChannel: '筛选渠道:',
      allChannels: '全部渠道 (All Channels)',
      searchKey: '搜索 Key 前后缀...',
      refresh: '🔄 刷新',
      clear: '🗑️ 清空统计',
      clearConfirm: '确定要清空所有 Token 消耗历史记录吗？此操作无法撤销。',
      channelName: '渠道名称',
      group: '所属分组',
      tokensRatio: '总 Token 消耗 (占比)',
      requestCount: '请求次数',
      keyCount: 'Key 数量',
      lastActive: '最近活跃时间',
      viewKeys: '查看 Key 明细 ➔',
      empty: '暂无 Token 消耗数据。发送请求后将实时自动统计！',
      emptyKey: '未找到匹配的 Key 消耗数据'
    },
    // 日志
    logs: {
      title: '实时请求与故障转移追踪',
      subtitle: '监控来自客户端的每次请求链路，直观查看静默 Failover 切换过程与耗时。',
      all: '全部',
      onlyFailover: '⚡ 仅故障转移',
      onlyError: '❌ 仅错误 (4xx/5xx)',
      onlySuccess: '✅ 仅成功',
      autoRefresh: '自动刷新 (2s)',
      pausedRefresh: '已暂停刷新',
      refresh: '🔄 刷新',
      policy: '⚙️ 保留策略',
      clear: '🗑️ 清空日志',
      clearConfirm: '确定要清空所有代理请求日志吗？磁盘持久化文件也将被完全清空。',
      time: '时间',
      endpoint: '端点路径',
      model: '请求模型',
      trail: '故障转移链路 (Failover Trail)',
      finalChannel: '最终处理渠道',
      statusCode: '状态码',
      duration: '总耗时',
      action: '操作',
      details: '错误详情',
      hideDetails: '收起详情',
      empty: '当前筛选条件下暂无请求日志',
      policyModalTitle: '⚙️ 请求日志持久化与清理策略',
      persistenceTipTitle: '自动持久化存储',
      persistenceTipText: '所有请求日志均已实时落盘至 data/request_logs.json，服务重启数据依然完整保留！',
      enableAutoClean: '是否开启自动清理过期日志',
      enableAutoCleanHint: '开启后将按设定的天数与容量阈值定期自动淘汰最旧的历史记录。',
      retentionDays: '历史保留天数 (超过此天数自动清理)',
      maxCapacity: '最大记录条数 (超出自动丢弃最旧数据)',
      savePolicy: '💾 保存清理策略'
    },
    // 接管与设置弹窗
    settings: {
      title: '⚡ 客户端一键接管与系统配置',
      subtitle: '全自动无侵入接管本地开发客户端代理配置，无需手动修改复杂设置。',
      claudeTitle: 'Claude Code CLI 深度接管',
      claudeDesc: '接管 ~/.claude/settings.json 与环境变量，自动路由至网关 /claude/ 端口',
      codexTitle: 'Codex (ChatGPT) 深度接管',
      codexDesc: '修改 ~/.codex/config.toml，将 model_provider 优雅绑定至本地网关',
      vscodeTitle: 'VSCode 全局设置接管',
      vscodeDesc: '接管 VSCode 用户级 settings.json，代理相关扩展流量',
      continueTitle: 'Continue 插件接管',
      continueDesc: '修改 ~/.continue/config.json，将大模型请求指向网关',
      portTitle: '网关监听端口设置',
      injectBtn: '一键接管',
      restoreBtn: '一键还原',
      injectedBadge: '已接管',
      notInjectedBadge: '未接管',
      savedSuccess: '设置已生效',
      customProvider: '自定义 TOML Provider 名称'
    },
    // 渠道异常通知
    alerts: {
      title: '渠道异常通知',
      clearAll: '一键清空',
      empty: '当前所有渠道运行健康，暂无异常告警',
      dismiss: '关闭通知',
      dismissAll: '一键全部忽略',
      autoSwitched: '已自动切换备用渠道',
      occurredTimes: '连续发生'
    }
  },
  en: {
    // Nav & Header
    nav: {
      dashboard: 'Dashboard',
      channels: 'Channels',
      tokens: 'Token Analytics',
      logs: 'Real-time Logs',
      guide: 'Quick Start',
      settings: 'Takeover / Settings',
      running: 'Running',
      langLabel: 'EN / 中'
    },
    // Dashboard
    dashboard: {
      title: 'Gateway Overview & Health Metrics',
      subtitle: 'Monitor all upstream channel health, request success rates, and failover status.',
      totalChannels: 'Configured Channels',
      activeChannels: 'Active Channels',
      totalRequests: 'Total Requests',
      successRate: 'Success Rate',
      failoverCount: 'Failover Triggers',
      primaryChannel: 'Current Primary Channel',
      recentFailovers: 'Recent Failover Trails',
      noFailovers: 'System is running smoothly. No failover events detected.',
      viewAllLogs: 'View All Logs ➔'
    },
    // Channels
    channels: {
      title: 'Channel & Failover Management',
      subtitle: 'Drag & drop priority adjustment, model alias rewrites, multi-key rotation and smart failovers.',
      addChannel: '+ Add Channel',
      editChannel: 'Edit Channel',
      filterAll: 'All Channels',
      filterClaude: 'Claude Dedicated',
      filterCodex: 'Codex Dedicated',
      filterUniversal: 'Universal Channels',
      empty: 'No channels configured. Click "+ Add Channel" above to get started.',
      test: 'Test Channel',
      testing: 'Testing...',
      save: 'Save Channel',
      cancel: 'Cancel',
      deleteConfirm: 'Are you sure you want to delete this channel?',
      keysLabel: 'API Keys (One per line for rotation & fallback)',
      modelRewrite: 'Model Alias Rewrite (JSON dictionary)',
      supportedModels: 'Supported Models (* for all)',
      group: 'Target Group',
      baseUrl: 'Base URL',
      name: 'Channel Name',
      dragTip: 'Tip: Drag channel cards to reorder failover priority'
    },
    // Token Stats
    tokens: {
      title: 'Token Consumption Analytics',
      totalTokens: 'Total Tokens Consumed',
      totalTokensSub: 'Cumulative throughput across all channels',
      promptTokens: 'Prompt Tokens (Input)',
      completionTokens: 'Completion Tokens (Output)',
      todayTokens: 'Today Consumption (Today)',
      totalRequests: 'Recorded Requests',
      dimChannel: '🏷️ Channel Dimension',
      dimKey: '🔑 Channel + Key Breakdown',
      filterChannel: 'Filter Channel:',
      allChannels: 'All Channels',
      searchKey: 'Search Key prefix/suffix...',
      refresh: '🔄 Refresh',
      clear: '🗑️ Clear Statistics',
      clearConfirm: 'Are you sure you want to clear all token usage history? This action cannot be undone.',
      channelName: 'Channel Name',
      group: 'Group',
      tokensRatio: 'Total Tokens (Ratio)',
      requestCount: 'Requests',
      keyCount: 'Key Count',
      lastActive: 'Last Active',
      viewKeys: 'View Keys ➔',
      empty: 'No token usage data yet. It will be recorded automatically when requests arrive!',
      emptyKey: 'No matching Key usage data found'
    },
    // Logs
    logs: {
      title: 'Real-time Request & Failover Tracing',
      subtitle: 'Track client request journeys, live transparent failover switches, and latency.',
      all: 'All',
      onlyFailover: '⚡ Failover Only',
      onlyError: '❌ Errors (4xx/5xx)',
      onlySuccess: '✅ Success Only',
      autoRefresh: 'Auto Refresh (2s)',
      pausedRefresh: 'Refresh Paused',
      refresh: '🔄 Refresh',
      policy: '⚙️ Retention Policy',
      clear: '🗑️ Clear Logs',
      clearConfirm: 'Are you sure you want to clear all request logs? Persisted files will also be removed.',
      time: 'Time',
      endpoint: 'Endpoint',
      model: 'Model',
      trail: 'Failover Trail',
      finalChannel: 'Final Channel',
      statusCode: 'Status',
      duration: 'Duration',
      action: 'Action',
      details: 'Error Details',
      hideDetails: 'Hide Details',
      empty: 'No request logs matching current filter.',
      policyModalTitle: '⚙️ Log Persistence & Retention Policy',
      persistenceTipTitle: 'Disk Persistence Active',
      persistenceTipText: 'All request logs are flushed to data/request_logs.json in real time. Data survives restarts!',
      enableAutoClean: 'Enable Scheduled Auto-Cleanup',
      enableAutoCleanHint: 'Automatically purges oldest logs exceeding day or count thresholds.',
      retentionDays: 'Retention Days (Older logs will be purged)',
      maxCapacity: 'Max Capacity (Trims oldest when exceeded)',
      savePolicy: '💾 Save Policy'
    },
    // Takeover & Settings
    settings: {
      title: '⚡ Client Takeover & System Settings',
      subtitle: 'Zero-config non-intrusive takeover of local client proxy configurations.',
      claudeTitle: 'Claude Code CLI Takeover',
      claudeDesc: 'Modifies ~/.claude/settings.json to route requests through /claude/ gateway endpoint',
      codexTitle: 'Codex (ChatGPT) Takeover',
      codexDesc: 'Modifies ~/.codex/config.toml, seamlessly binding model_provider to local gateway',
      vscodeTitle: 'VSCode Global Settings Takeover',
      vscodeDesc: 'Injects into user settings.json to proxy AI extension traffic',
      continueTitle: 'Continue Extension Takeover',
      continueDesc: 'Modifies ~/.continue/config.json to point LLM requests to local gateway',
      portTitle: 'Gateway Port Configuration',
      injectBtn: 'Takeover',
      restoreBtn: 'Restore',
      injectedBadge: 'Active',
      notInjectedBadge: 'Inactive',
      savedSuccess: 'Settings applied successfully',
      customProvider: 'Custom TOML Provider Name'
    },
    // Channel Alerts
    alerts: {
      title: 'Channel Alerts',
      clearAll: 'Clear All',
      empty: 'All channels are healthy, no active alerts.',
      dismiss: 'Dismiss',
      dismissAll: 'Dismiss All',
      autoSwitched: 'Auto failover triggered',
      occurredTimes: 'Consecutive'
    }
  }
};

export function useI18n() {
  const t = computed(() => messages[currentLang.value] || messages.zh);
  return {
    lang: currentLang,
    t,
    setLanguage,
    toggleLanguage
  };
}
