const API_BASE = '/api';

export async function request(path, options = {}) {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...options.headers
    },
    ...options
  });

  const json = await response.json();
  if (!response.ok || json.success === false) {
    throw new Error(json.message || `请求失败 HTTP ${response.status}`);
  }
  return json.data;
}

export const api = {
  // 渠道相关
  getChannels: () => request('/channels'),
  getChannel: (id) => request(`/channels/${id}`),
  createChannel: (data) => request('/channels', { method: 'POST', body: JSON.stringify(data) }),
  updateChannel: (id, data) => request(`/channels/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteChannel: (id) => request(`/channels/${id}`, { method: 'DELETE' }),
  toggleChannel: (id, isEnabled) => request(`/channels/${id}/toggle`, { method: 'POST', body: JSON.stringify({ isEnabled }) }),
  reorderChannels: (orderedIds) => request('/channels/reorder', { method: 'POST', body: JSON.stringify(orderedIds) }),
  testChannel: (data) => request('/channels/test', { method: 'POST', body: JSON.stringify(data) }),

  // 请求业务日志与概览
  getLogs: (limit = 100) => request(`/logs?limit=${limit}`),
  getPagedLogs: (page = 1, pageSize = 50, filter = 'all', keyword = '') => 
    request(`/logs/paged?page=${page}&pageSize=${pageSize}&filter=${encodeURIComponent(filter)}&keyword=${encodeURIComponent(keyword)}`),
  clearLogs: () => request('/logs', { method: 'DELETE' }),
  getSummary: () => request('/logs/summary'),
  getLogSettings: () => request('/logs/settings'),
  saveLogSettings: (data) => request('/logs/settings', { method: 'POST', body: JSON.stringify(data) }),

  // 系统配置与一键接管
  getSystemStatus: () => request('/system/status'),
  injectConfig: (target = 'all', port = 0, group = 'claude', providerName = 'gateway') => 
    request('/system/inject', { method: 'POST', body: JSON.stringify({ target, port, group, providerName }) }),
  restoreConfig: (target = 'all') => 
    request('/system/restore', { method: 'POST', body: JSON.stringify({ target }) }),
  setPort: (port) => 
    request('/system/port', { method: 'POST', body: JSON.stringify({ port }) }),

  // 系统级运行日志
  getSystemLogs: (limit = 200, level = '') => 
    request(`/system/logs?limit=${limit}${level ? `&level=${level}` : ''}`),
  clearSystemLogs: () => request('/system/logs', { method: 'DELETE' }),

  // Token 消耗统计
  getTokenSummary: () => request('/tokenstats/summary'),
  getChannelTokenStats: () => request('/tokenstats/channels'),
  getKeyTokenStats: (channelId = '') => 
    request(`/tokenstats/keys${channelId ? `?channelId=${encodeURIComponent(channelId)}` : ''}`),
  clearTokenStats: () => request('/tokenstats', { method: 'DELETE' }),

  // 渠道异常通知
  getNotifications: () => request('/notifications'),
  dismissNotification: (id) => request(`/notifications/${id}/dismiss`, { method: 'POST' }),
  clearAllNotifications: () => request('/notifications/clear', { method: 'POST' }),

  // 长任务主动提醒与桌面宠物
  getNotificationSettings: () => request('/notifications/settings'),
  saveNotificationSettings: (data) => request('/notifications/settings', { method: 'POST', body: JSON.stringify(data) }),
  getTaskStatus: () => request('/notifications/task-status'),
  testChime: () => request('/notifications/test-chime', { method: 'POST' })
};
