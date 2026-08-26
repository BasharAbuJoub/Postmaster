const PREFIX = window.PM_PREFIX || '';

async function apiCall(path, method = 'GET') {
  const res = await fetch(PREFIX + '/api' + path, { method });
  if (res.status === 204) return null;
  if (!res.ok) throw new Error(await res.text() || res.statusText);
  return res.json();
}

const STATUS_META = {
  Pending:    { label: 'Pending',    cls: 'bg-blue-950 text-blue-400 ring-1 ring-blue-800' },
  Processing: { label: 'Processing', cls: 'bg-amber-950 text-amber-400 ring-1 ring-amber-800' },
  Succeeded:  { label: 'Succeeded',  cls: 'bg-emerald-950 text-emerald-400 ring-1 ring-emerald-800' },
  Failed:     { label: 'Failed',     cls: 'bg-red-950 text-red-400 ring-1 ring-red-800' },
  Dead:       { label: 'Dead',       cls: 'bg-neutral-800 text-neutral-400 ring-1 ring-neutral-700' },
  Cancelled:  { label: 'Cancelled',  cls: 'bg-neutral-800 text-neutral-500 ring-1 ring-neutral-700' },
};

const SIDEBAR_ITEMS = [
  { key: 'all', label: 'All',        value: '',  badgeCls: null },
  { key: '0',   label: 'Pending',    value: '0', badgeCls: 'bg-blue-950 text-blue-400' },
  { key: '1',   label: 'Processing', value: '1', badgeCls: 'bg-amber-950 text-amber-400' },
  { key: '2',   label: 'Succeeded',  value: '2', badgeCls: 'bg-emerald-950 text-emerald-400' },
  { key: '3',   label: 'Failed',     value: '3', badgeCls: 'bg-red-950 text-red-400' },
  { key: '4',   label: 'Dead',       value: '4', badgeCls: 'bg-neutral-800 text-neutral-400' },
  { key: '5',   label: 'Cancelled',  value: '5', badgeCls: 'bg-neutral-800 text-neutral-500' },
];

const STATS_KEYS = {
  all: 'total', '0': 'pending', '1': 'processing',
  '2': 'succeeded', '3': 'failed', '4': 'dead', '5': 'cancelled',
};

const TERMINAL_STATUSES = new Set(['Succeeded', 'Dead', 'Cancelled']);
const REFRESH_INTERVALS = [5_000, 10_000, 30_000, 60_000];

function fmt(dt) {
  if (!dt) return '—';
  return new Date(dt).toLocaleString();
}

const PRETTY_LIMIT  = 50_000;
const DISPLAY_LIMIT = 100_000;

function tryPrettyJson(str) {
  if (!str || str.length > PRETTY_LIMIT) return { value: str, isJson: false };
  try { return { value: JSON.stringify(JSON.parse(str), null, 2), isJson: true }; }
  catch { return { value: str, isJson: false }; }
}

function escapeHtml(str) {
  return str
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');
}

function highlightJson(str) {
  const escaped = escapeHtml(str);
  const tokenPattern = /("(?:\\u[\da-fA-F]{4}|\\[^u]|[^\\"])*"\s*:|"(?:\\u[\da-fA-F]{4}|\\[^u]|[^\\"])*"|\btrue\b|\bfalse\b|\bnull\b|-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)/g;
  return escaped.replace(tokenPattern, token => {
    let cls = 'json-number';
    if (token.startsWith('"')) cls = token.trimEnd().endsWith(':') ? 'json-key' : 'json-string';
    else if (token === 'true' || token === 'false') cls = 'json-boolean';
    else if (token === 'null') cls = 'json-null';
    return `<span class="${cls}">${token}</span>`;
  });
}

function shellQuote(value) {
  return `'${String(value).replaceAll("'", `'"'"'`)}'`;
}

function buildCurl(message) {
  let headers = {};
  try {
    const parsed = JSON.parse(message.headers || '{}');
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) headers = parsed;
  } catch { }

  const hasHeader = name => Object.keys(headers).some(key => key.toLowerCase() === name.toLowerCase());
  if (message.correlationId && !hasHeader('X-Correlation-Id'))
    headers['X-Correlation-Id'] = message.correlationId;
  if (message.payload && !hasHeader('Content-Type'))
    headers['Content-Type'] = 'application/json';

  const parts = [
    'curl',
    `  --request ${shellQuote(message.method)}`,
    `  --url ${shellQuote(message.url)}`,
    ...Object.entries(headers).map(([key, value]) => `  --header ${shellQuote(`${key}: ${value}`)}`),
  ];

  if (message.payload) parts.push(`  --data-raw ${shellQuote(message.payload)}`);
  return parts.join(' \\\n');
}

async function copyText(text) {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return;
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.select();
  document.execCommand('copy');
  textarea.remove();
}

function statusCodeCls(code) {
  if (code >= 500) return 'text-red-400';
  if (code >= 400) return 'text-amber-400';
  if (code >= 200 && code < 300) return 'text-emerald-400';
  return 'text-neutral-500';
}

function buildSectionMeta(m) {
  const parts = [];
  if (m.responseStatusCode != null)
    parts.push(`<span class="${statusCodeCls(m.responseStatusCode)} font-semibold">${m.responseStatusCode}</span>`);
  if (m.elapsedMs != null)
    parts.push(`<span class="text-neutral-400">${m.elapsedMs} ms</span>`);
  return parts.join('<span class="text-neutral-600 mx-1">·</span>');
}

function buildSections(m) {
  const out = [];
  const add = (id, label, raw, isError = false, meta = '') => {
    if (!raw) return;
    const formatted = tryPrettyJson(raw);
    const value = formatted.value;
    const truncated = value.length > DISPLAY_LIMIT;
    const text = truncated ? value.slice(0, DISPLAY_LIMIT) : value;
    out.push({
      id, label, value, meta, isError, isJson: formatted.isJson, showFull: false,
      html: formatted.isJson ? highlightJson(value) : escapeHtml(value),
      textHtml: formatted.isJson ? highlightJson(text) : escapeHtml(text),
      text,
      truncated,
      preClass: isError ? 'break-all max-h-48' : 'max-h-96',
      kbShown: (DISPLAY_LIMIT / 1000).toFixed(0),
      kbTotal: (value.length / 1000).toFixed(0),
    });
  };
  add('error',    'Error',         m.errorMessage, true);
  add('headers',  'Headers',       m.headers);
  add('payload',  'Payload',       m.payload);
  add('response', 'Response Body', m.responseBody, false, buildSectionMeta(m));
  add('metadata', 'Metadata',      m.metadata);
  return out;
}

document.addEventListener('alpine:init', () => {
  Alpine.data('dashboard', () => ({
    sidebarItems: SIDEBAR_ITEMS,

    // Theme
    theme: localStorage.getItem('postmaster-theme') === 'light' ? 'light' : 'dark',

    // Stats
    stats: null,
    statsLoading: false,

    // Refresh
    refreshTimer: null,
    autoRefresh: localStorage.getItem('postmaster-auto-refresh') !== 'false',
    refreshInterval: REFRESH_INTERVALS.includes(Number(localStorage.getItem('postmaster-refresh-interval')))
      ? Number(localStorage.getItem('postmaster-refresh-interval'))
      : 5_000,
    lastUpdatedAt: null,

    // List
    view: 'list',
    listLoading: true,
    listError: null,
    messages: [],
    totalCount: 0,
    totalPages: 1,
    page: 1,
    hasPrev: false,
    hasNext: false,
    activeStatus: 'all',
    filters: { status: '', channel: '', from: '', to: '', correlationId: '', metadata: '' },

    // Detail
    detailLoading: false,
    detailNotFound: false,
    detail: null,
    detailSections: [],
    copiedSectionId: null,
    copiedCurl: false,

    // ── Init ──────────────────────────────────────────────────────────────────

    init() {
      const id = this._matchDetailPath();
      if (id) {
        this.view = 'detail';
        this._fetchDetail(id);
      } else {
        this._fetchMessages();
      }
      this._fetchStats();

      this._startRefresh();

      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
          if (this.autoRefresh) this._refreshDashboard();
          this._startRefresh();
        } else {
          this._stopRefresh();
        }
      });

      window.addEventListener('popstate', () => {
        const mid = this._matchDetailPath();
        if (mid) { this.view = 'detail'; this._fetchDetail(mid); }
        else     { this.view = 'list';   this._fetchMessages(); }
      });
    },

    destroy() {
      this._stopRefresh();
    },

    _startRefresh() {
      if (!this.autoRefresh || this.refreshTimer != null || document.visibilityState !== 'visible') return;
      this.refreshTimer = setInterval(() => this._refreshDashboard(), this.refreshInterval);
    },

    _stopRefresh() {
      if (this.refreshTimer == null) return;
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    },

    _refreshDashboard() {
      if (!this.autoRefresh || document.visibilityState !== 'visible') return;
      this._fetchStats();
      if (this.view === 'list') this._refreshList();
      else this._refreshDetail();
    },

    toggleAutoRefresh() {
      this.autoRefresh = !this.autoRefresh;
      localStorage.setItem('postmaster-auto-refresh', this.autoRefresh);
      this._stopRefresh();
      if (this.autoRefresh) {
        this._refreshDashboard();
        this._startRefresh();
      }
    },

    updateRefreshInterval() {
      localStorage.setItem('postmaster-refresh-interval', this.refreshInterval);
      this._stopRefresh();
      this._startRefresh();
    },

    get refreshActive() {
      return this.statsLoading || (this.view === 'list' ? this.listLoading : this.detailLoading);
    },

    get lastUpdatedLabel() {
      return this.lastUpdatedAt ? `Updated ${this.lastUpdatedAt.toLocaleTimeString()}` : 'Not updated yet';
    },

    _matchDetailPath() {
      const escaped = PREFIX.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const m = window.location.pathname.match(new RegExp(escaped + '/messages/([^/]+)$'));
      return m ? m[1] : null;
    },

    // ── Stats ─────────────────────────────────────────────────────────────────

    async _fetchStats() {
      if (this.statsLoading) return;
      this.statsLoading = true;
      try {
        this.stats = await apiCall('/stats');
        this.lastUpdatedAt = new Date();
      }
      catch { }
      finally { this.statsLoading = false; }
    },

    statCount(key) {
      if (!this.stats) return '—';
      return this.stats[STATS_KEYS[key]] ?? '—';
    },

    get successRate() {
      return this.stats ? this.stats.successRate.toFixed(1) + '%' : '—';
    },

    get avgElapsed() {
      if (!this.stats) return '—';
      return this.stats.averageElapsedMs > 0 ? Math.round(this.stats.averageElapsedMs) + ' ms' : '—';
    },

    toggleTheme() {
      this.theme = this.theme === 'dark' ? 'light' : 'dark';
      localStorage.setItem('postmaster-theme', this.theme);
    },

    // ── Navigation ────────────────────────────────────────────────────────────

    showList(push = true) {
      this.view = 'list';
      if (push) history.pushState({}, '', PREFIX + '/');
      this._fetchMessages();
      this._fetchStats();
    },

    showDetail(id, push = true) {
      this.view = 'detail';
      if (push) history.pushState({}, '', PREFIX + '/messages/' + id);
      this._fetchDetail(id);
      this._fetchStats();
    },

    filterByStatus(value) {
      this.page = 1;
      this.filters.status = value;
      this.activeStatus = value === '' ? 'all' : value;
      if (this.view !== 'list') {
        this.view = 'list';
        history.pushState({}, '', PREFIX + '/');
      }
      this._fetchMessages();
      this._fetchStats();
    },

    applyFilters() {
      this.page = 1;
      this._fetchMessages();
      this._fetchStats();
    },

    clearFilters() {
      this.page = 1;
      this.filters = { status: '', channel: '', from: '', to: '', correlationId: '', metadata: '' };
      this.activeStatus = 'all';
      this._fetchMessages();
    },

    changePage(delta) {
      this.page = Math.max(1, this.page + delta);
      this._fetchMessages();
    },

    get pageInfo() {
      return `Page ${this.page} of ${this.totalPages} (${this.totalCount} total)`;
    },

    sidebarBtnCls(key) {
      return key === this.activeStatus
        ? 'border-indigo-500 bg-indigo-950 text-indigo-400'
        : 'border-transparent text-neutral-400 hover:bg-neutral-800';
    },

    sidebarCountCls(item) {
      if (item.key === 'all') return 'text-sm font-semibold text-neutral-500 tabular-nums';
      return `text-sm font-medium rounded-md px-1.5 py-0.5 tabular-nums ${item.badgeCls}`;
    },

    // ── Messages list ─────────────────────────────────────────────────────────

    async _fetchMessages(isRefresh = false) {
      this.listLoading = true;
      this.listError = null;
      if (!isRefresh) this.messages = [];

      const params = new URLSearchParams();
      if (this.filters.status) params.set('status', this.filters.status);
      if (this.filters.channel.trim()) params.set('channel', this.filters.channel.trim());
      if (this.filters.from) params.set('from', new Date(this.filters.from).toISOString());
      if (this.filters.to) params.set('to', new Date(this.filters.to).toISOString());
      if (this.filters.correlationId.trim()) params.set('correlationId', this.filters.correlationId.trim());
      if (this.filters.metadata.trim()) params.set('metadata', this.filters.metadata.trim());
      params.set('page', this.page);
      params.set('pageSize', 10);

      try {
        const data = await apiCall('/messages?' + params.toString());
        this.totalCount = data.totalCount;
        this.totalPages = data.totalPages || 1;
        this.hasPrev = data.hasPreviousPage;
        this.hasNext = data.hasNextPage;
        this.messages = data.items.map((m, i) => ({
          ...m,
          _index: (data.page - 1) * data.pageSize + i + 1,
          _statusMeta: STATUS_META[m.status] ?? { label: m.status ?? 'Unknown', cls: 'bg-neutral-800 text-neutral-400' },
          _fmtCreated: fmt(m.createdAt),
        }));
      } catch (e) {
        this.listError = e.message;
      } finally {
        this.listLoading = false;
      }
    },

    _refreshList() {
      if (document.visibilityState !== 'visible'
        || this.view !== 'list'
        || this.listLoading) return;

      this._fetchMessages(true);
    },

    // ── Detail ────────────────────────────────────────────────────────────────

    async _fetchDetail(id) {
      this.detailLoading = true;
      this.detailNotFound = false;
      this.detail = null;
      this.detailSections = [];

      try {
        const m = await apiCall('/messages/' + id);
        if (!m) { this.detailNotFound = true; return; }
        this.detail = {
          ...m,
          _statusMeta: STATUS_META[m.status] ?? { label: m.status ?? 'Unknown', cls: 'bg-neutral-800 text-neutral-400' },
          _fmtCreated:   fmt(m.createdAt),
          _fmtNext:      fmt(m.nextAttemptAt),
          _fmtProcessed: fmt(m.processedAt),
          _elapsedLabel: m.elapsedMs != null ? m.elapsedMs + ' ms' : '—',
          _canReset:  m.status !== 'Processing',
          _canCancel: m.status === 'Pending' || m.status === 'Failed',
        };
        this.detailSections = buildSections(m);
      } catch {
        this.detailNotFound = true;
      } finally {
        this.detailLoading = false;
      }
    },

    _refreshDetail() {
      if (document.visibilityState !== 'visible'
        || this.view !== 'detail'
        || this.detailLoading
        || !this.detail
        || TERMINAL_STATUSES.has(this.detail.status)) return;

      this._fetchDetail(this.detail.id);
    },

    async copySection(section) {
      try {
        await copyText(section.value);

        this.copiedSectionId = section.id;
        setTimeout(() => {
          if (this.copiedSectionId === section.id) this.copiedSectionId = null;
        }, 2_000);
      } catch (e) {
        alert('Copy failed: ' + e.message);
      }
    },

    async copyCurl() {
      try {
        await copyText(buildCurl(this.detail));
        this.copiedCurl = true;
        setTimeout(() => { this.copiedCurl = false; }, 2_000);
      } catch (e) {
        alert('Copy cURL failed: ' + e.message);
      }
    },

    async resetMessage() {
      try {
        await apiCall('/messages/' + this.detail.id + '/reset', 'POST');
        await this._fetchDetail(this.detail.id);
        this._fetchStats();
      } catch (e) {
        alert('Reset failed: ' + e.message);
      }
    },

    async cancelMessage() {
      if (!confirm('Cancel this message? This cannot be undone.')) return;
      try {
        await apiCall('/messages/' + this.detail.id + '/cancel', 'POST');
        await this._fetchDetail(this.detail.id);
        this._fetchStats();
      } catch (e) {
        alert('Cancel failed: ' + e.message);
      }
    },
  }));
});
