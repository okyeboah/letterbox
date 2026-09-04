const state = {
  tab: 'email',
  search: '',
  items: [],
  selectedId: null,
  detail: null,
  view: 'rendered',
  sourceCache: null,
};

const listEl = document.getElementById('list');
const detailEl = document.getElementById('detail');
const searchEl = document.getElementById('search');
const autoEl = document.getElementById('auto');
const emptyDetail = '<p class="empty">Select a message to see it.</p>';

async function api(path, init) {
  const response = await fetch(path, init);
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  const type = response.headers.get('content-type') || '';
  return type.includes('json') ? response.json() : response.text();
}

async function loadList() {
  const params = new URLSearchParams({ type: state.tab });
  if (state.search) params.set('search', state.search);
  try {
    state.items = await api(`/api/messages?${params}`);
    renderList();
  } catch {
    // The next poll retries a transient failure.
  }
}

function renderList() {
  if (state.items.length === 0) {
    listEl.innerHTML = '<p class="empty">No messages captured.</p>';
    return;
  }
  listEl.innerHTML = state.items.map(rowHtml).join('');
}

function rowHtml(message) {
  const time = new Date(message.receivedAt).toLocaleTimeString();
  return `
    <article class="row${message.id === state.selectedId ? ' selected' : ''}" data-id="${message.id}">
      <div class="row-top">
        <span class="from">${escapeHtml(message.from || '')}</span>
        <span class="arrow">&#8594;</span>
        <span class="to">${escapeHtml(message.to || '')}</span>
      </div>
      <div class="row-title">${escapeHtml(message.title || '')}</div>
      <div class="row-meta">
        <span class="time">${time}</span>
        ${otpBadges(message.otpCodes || [])}
        <button class="row-delete" data-id="${message.id}" title="Delete">&#10005;</button>
      </div>
      <div class="preview">${escapeHtml(message.preview || '')}</div>
    </article>`;
}

async function select(id) {
  state.selectedId = id;
  state.view = 'rendered';
  state.sourceCache = null;
  renderList();
  try {
    state.detail = await api(`/api/messages/${id}`);
  } catch {
    detailEl.innerHTML = '<p class="empty">The message is gone.</p>';
    return;
  }
  state.detail.channel === 'email' ? renderEmailDetail() : renderSmsDetail();
}

function renderEmailDetail() {
  const d = state.detail;
  const attachments = (d.attachments || []).map(a =>
    `<a class="attachment" href="/api/messages/${d.id}/attachments/${a.id}" download="${escapeHtml(a.fileName)}">${escapeHtml(a.fileName)} (${formatSize(a.size)})</a>`).join('');
  const otp = (d.otpCodes || []).length ? `<div class="meta">${otpBadges(d.otpCodes)}</div>` : '';
  detailEl.innerHTML = `
    <div class="detail-head">
      <h2>${escapeHtml(d.subject || '(no subject)')}</h2>
      <div class="meta">${escapeHtml(d.headerFrom || '')} &#8594; ${escapeHtml(d.headerTo || '')}</div>
      <div class="meta dim">envelope: ${escapeHtml(d.envelopeFrom || '')} &#8594; ${escapeHtml((d.envelopeTo || []).join(', '))}</div>
      <div class="meta dim">${new Date(d.receivedAt).toLocaleString()}</div>
      ${otp}
      ${attachments ? `<div class="meta">${attachments}</div>` : ''}
      <div class="views">
        <button class="view-tab active" data-view="rendered">Rendered</button>
        <button class="view-tab" data-view="plain">Plain</button>
        <button class="view-tab" data-view="source">Source</button>
      </div>
    </div>
    <div id="view-pane"></div>`;
  renderEmailView();
}

async function renderEmailView() {
  const pane = document.getElementById('view-pane');
  const d = state.detail;
  if (state.view === 'rendered') {
    if (!d.htmlBody) {
      pane.innerHTML = '<p class="empty">This message has no HTML body.</p>';
      return;
    }
    pane.innerHTML = '<iframe id="body-frame" sandbox=""></iframe>';
    document.getElementById('body-frame').srcdoc = d.htmlBody;
  } else if (state.view === 'plain') {
    pane.innerHTML = `<pre>${escapeHtml(d.plainBody || '(no plain body)')}</pre>`;
  } else {
    if (state.sourceCache === null) state.sourceCache = await api(`/api/messages/${d.id}/raw`);
    pane.innerHTML = `<pre>${escapeHtml(state.sourceCache)}</pre>`;
  }
}

function renderSmsDetail() {
  const d = state.detail;
  const fields = Object.entries(d.fields || {})
    .map(([key, value]) => `<tr><th>${escapeHtml(key)}</th><td>${escapeHtml(value)}</td></tr>`).join('');
  detailEl.innerHTML = `
    <div class="detail-head">
      <h2>SMS</h2>
      <div class="meta dim">${escapeHtml(d.url || '')}</div>
      <div class="meta dim">${escapeHtml(d.contentType || '')}</div>
      <div class="meta dim">${new Date(d.receivedAt).toLocaleString()}</div>
      <div class="meta">${otpBadges(d.otpCodes || [])}</div>
    </div>
    <table class="fields">${fields}</table>
    <h3>Raw body</h3>
    <pre>${escapeHtml(d.rawBody || '')}</pre>`;
}

function otpBadges(codes) {
  return codes.map(code => `<span class="otp" data-copy="${escapeHtml(code)}" title="Click to copy">${escapeHtml(code)}</span>`).join('');
}

listEl.addEventListener('click', async event => {
  const deleteButton = event.target.closest('.row-delete');
  if (deleteButton) {
    event.stopPropagation();
    await api(`/api/messages/${deleteButton.dataset.id}`, { method: 'DELETE' }).catch(() => {});
    if (state.selectedId === deleteButton.dataset.id) {
      state.selectedId = null;
      state.detail = null;
      detailEl.innerHTML = emptyDetail;
    }
    loadList();
    return;
  }
  const row = event.target.closest('.row');
  if (row) select(row.dataset.id);
});

detailEl.addEventListener('click', async event => {
  const tab = event.target.closest('.view-tab');
  if (tab) {
    state.view = tab.dataset.view;
    detailEl.querySelectorAll('.view-tab').forEach(button => button.classList.toggle('active', button === tab));
    await renderEmailView().catch(() => {});
    return;
  }
  const badge = event.target.closest('.otp');
  if (badge) navigator.clipboard?.writeText(badge.dataset.copy).catch(() => {});
});

document.querySelectorAll('.tab').forEach(button =>
  button.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(candidate => candidate.classList.toggle('active', candidate === button));
    state.tab = button.dataset.tab;
    state.selectedId = null;
    state.detail = null;
    detailEl.innerHTML = emptyDetail;
    loadList();
  }));

let searchTimer;
searchEl.addEventListener('input', () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    state.search = searchEl.value.trim();
    loadList();
  }, 300);
});

document.getElementById('refresh').addEventListener('click', loadList);

document.getElementById('clear').addEventListener('click', async () => {
  if (!confirm('Delete all captured messages?')) return;
  await api('/api/messages', { method: 'DELETE' }).catch(() => {});
  state.selectedId = null;
  state.detail = null;
  detailEl.innerHTML = emptyDetail;
  loadList();
});

setInterval(() => {
  if (autoEl.checked && document.visibilityState === 'visible') loadList();
}, 2000);

function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function escapeHtml(text) {
  return String(text).replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
}

loadList();
