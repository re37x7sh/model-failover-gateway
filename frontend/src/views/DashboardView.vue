<template>
  <div class="dashboard-view">
    <!-- 顶部状态栏 -->
    <div class="stats-grid">
      <div class="glass-card stat-card">
        <div class="stat-header">
          <span class="stat-title">活跃渠道 / 总渠道</span>
          <span class="stat-icon">⚡</span>
        </div>
        <div class="stat-value">
          <span class="highlight">{{ summary.activeChannels }}</span>
          <span class="divider">/</span>
          <span>{{ summary.totalChannels }}</span>
        </div>
        <div class="stat-footer">
          <span class="status-dot active"></span>
          <span>首选: {{ summary.currentPrimaryChannelName || '无激活渠道' }}</span>
        </div>
      </div>

      <div class="glass-card stat-card">
        <div class="stat-header">
          <span class="stat-title">总代理请求量</span>
          <span class="stat-icon">📈</span>
        </div>
        <div class="stat-value">{{ summary.totalRequests }}</div>
        <div class="stat-footer text-muted">
          <span>成功: {{ summary.successfulRequests }} | 失败: {{ summary.failedRequests }}</span>
        </div>
      </div>

      <div class="glass-card stat-card glow-warning">
        <div class="stat-header">
          <span class="stat-title">自动故障转移次数</span>
          <span class="stat-icon">🛡️</span>
        </div>
        <div class="stat-value warning-text">{{ summary.totalFailovers }}</div>
        <div class="stat-footer">
          <span class="badge badge-warning">欠费/限流静默救急</span>
        </div>
      </div>

      <div class="glass-card stat-card">
        <div class="stat-header">
          <span class="stat-title">请求可用率</span>
          <span class="stat-icon">🎯</span>
        </div>
        <div class="stat-value success-text">{{ summary.successRate }}%</div>
        <div class="stat-footer">
          <span class="badge badge-success">SLA 高可用保障</span>
        </div>
      </div>
    </div>

    <!-- 中间核心区：工作原理与快速操作 -->
    <div class="quick-overview-grid">
      <div class="glass-card banner-card">
        <div class="banner-content">
          <div class="banner-title">
            <span class="lightning-icon">⚡</span>
            <span>无感故障转移已就绪</span>
          </div>
          <p class="banner-desc">
            VSCode 插件（Claude Code / CodeX / Continue）配置固定指向本地网关后，<strong>再也不需要重启或重新加载窗口</strong>。
            当任何渠道返回 402（余额不足）、429（限流）或网络异常时，网关将在字节流级别以 100% 完整 Header 自动重试下一优先级渠道。
          </p>
          <div class="banner-actions">
            <button class="btn btn-primary" @click="$emit('navigate', 'channels')">
              <span>⚡ 管理渠道与优先级</span>
            </button>
            <button class="btn btn-secondary" @click="$emit('navigate', 'playground')">
              <span>🧪 打开调试沙箱</span>
            </button>
            <button class="btn btn-secondary" @click="$emit('navigate', 'guide')">
              <span>🚀 查看插件接入指引</span>
            </button>
          </div>
        </div>
      </div>

      <div class="glass-card activity-card">
        <div class="card-header">
          <h3 class="card-title">最近代理活动</h3>
          <button class="btn btn-secondary btn-sm" @click="$emit('navigate', 'logs')">查看全部</button>
        </div>

        <div v-if="recentLogs.length === 0" class="empty-state">
          <span>暂无请求记录，请通过 VSCode 发起一次对话试试</span>
        </div>

        <div v-else class="activity-list">
          <div v-for="log in recentLogs.slice(0, 5)" :key="log.id" class="activity-item">
            <div class="activity-main">
              <div class="activity-top">
                <span class="activity-method">{{ log.requestMethod }}</span>
                <span class="activity-path">{{ log.requestPath }}</span>
                <span v-if="log.isFailover" class="badge badge-warning">Failover</span>
              </div>
              <div class="activity-meta">
                <span class="activity-channel">渠道: {{ log.finalChannel || '全部失败' }}</span>
                <span class="activity-duration">{{ log.durationMs }}ms</span>
              </div>
            </div>
            <div class="activity-status">
              <span :class="['badge', log.statusCode >= 200 && log.statusCode < 400 ? 'badge-success' : 'badge-danger']">
                {{ log.statusCode }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  summary: {
    type: Object,
    required: true
  },
  recentLogs: {
    type: Array,
    default: () => []
  }
});

defineEmits(['navigate']);
</script>

<style scoped>
.dashboard-view {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 16px;
}

.stat-card {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.stat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.stat-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-muted);
}

.stat-icon {
  font-size: 18px;
}

.stat-value {
  font-size: 30px;
  font-weight: 800;
  letter-spacing: -0.03em;
  font-family: var(--font-mono);
  display: flex;
  align-items: baseline;
  gap: 6px;
}

.stat-value .highlight {
  color: var(--accent-primary);
}

.stat-value .divider {
  color: var(--text-dim);
  font-size: 20px;
}

.warning-text {
  color: var(--warning);
}

.success-text {
  color: var(--success);
}

.stat-footer {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--text-muted);
}

.quick-overview-grid {
  display: grid;
  grid-template-columns: 1.3fr 1fr;
  gap: 20px;
}

@media (max-width: 960px) {
  .quick-overview-grid {
    grid-template-columns: 1fr;
  }
}

.banner-card {
  padding: 28px;
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.15), rgba(168, 85, 247, 0.08));
  border-color: rgba(99, 102, 241, 0.25);
}

.banner-title {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 20px;
  font-weight: 700;
  margin-bottom: 12px;
}

.lightning-icon {
  color: #fbbf24;
  font-size: 22px;
}

.banner-desc {
  color: var(--text-muted);
  font-size: 14px;
  line-height: 1.7;
  margin-bottom: 24px;
}

.banner-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.activity-card {
  padding: 20px;
  display: flex;
  flex-direction: column;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.card-title {
  font-size: 15px;
  font-weight: 600;
}

.empty-state {
  padding: 40px 20px;
  text-align: center;
  color: var(--text-dim);
  font-size: 13px;
}

.activity-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.activity-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 14px;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
}

.activity-top {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  margin-bottom: 4px;
}

.activity-method {
  font-weight: 700;
  font-family: var(--font-mono);
  color: var(--accent-primary);
  font-size: 11px;
}

.activity-path {
  font-family: var(--font-mono);
  color: var(--text-main);
}

.activity-meta {
  display: flex;
  gap: 12px;
  font-size: 11px;
  color: var(--text-muted);
}

.activity-duration {
  font-family: var(--font-mono);
}
</style>
