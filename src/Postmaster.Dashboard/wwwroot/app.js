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

function fmt(dt) {
  if (!dt) return '—';
  return new Date(dt).toLocaleString();
}

const PRETTY_LIMIT  = 50_000;
const DISPLAY_LIMIT = 100_000;

function tryPrettyJson(str) {
  if (!str) return str;
  if (str.length > PRETTY_LIMIT) return str;
  try { return JSON.stringify(JSON.parse(str), null, 2); }
  catch { return str; }
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
    const value = tryPrettyJson(raw);
    const truncated = value.length > DISPLAY_LIMIT;
    out.push({
      id, label, value, meta, isError, showFull: false,
      text: truncated ? value.slice(0, DISPLAY_LIMIT) : value,
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

    // Stats
    stats: null,

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

      setInterval(() => this._fetchStats(), 30_000);

      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') this._fetchStats();
      });

      window.addEventListener('popstate', () => {
        const mid = this._matchDetailPath();
        if (mid) { this.view = 'detail'; this._fetchDetail(mid); }
        else     { this.view = 'list';   this._fetchMessages(); }
      });
    },

    _matchDetailPath() {
      const escaped = PREFIX.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const m = window.location.pathname.match(new RegExp(escaped + '/messages/([^/]+)$'));
      return m ? m[1] : null;
    },

    // ── Stats ─────────────────────────────────────────────────────────────────

    async _fetchStats() {
      try { this.stats = await apiCall('/stats'); } catch { }
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

    async _fetchMessages() {
      this.listLoading = true;
      this.listError = null;
      this.messages = [];

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
