const API = 'https://docutrack-production.up.railway.app/api';

let allDocs = [];
let allUsers = [];

const statusMap = {
  0: { label: 'Draft', cls: 'badge-draft' },
  1: { label: 'Under Review', cls: 'badge-review' },
  2: { label: 'Approved', cls: 'badge-approved' },
  3: { label: 'Rejected', cls: 'badge-rejected' },
  4: { label: 'Archived', cls: 'badge-archived' },
};

const settings = [
  'Dark Mode', 'Email Notifications', 'In-app Notifications',
  'Require Login for Every Session', 'Restrict Downloads for Sensitive Files',
  'Automatically Logout after Inactivity', 'Highlight Recently Updated Documents', 'Reduce Motion'
];

const settingsState = {};
settings.forEach(s => settingsState[s] = false);

if (localStorage.getItem('notifEmail')) {
    settingsState['Email Notifications'] = true;
}

// ─── SIDEBAR ───────────────────────────────────────────────
function toggleSidebar() {
  document.querySelector('.sidebar').classList.toggle('open');
  document.getElementById('sidebarOverlay').classList.toggle('open');
}

// ─── NAVIGATION ────────────────────────────────────────────
function navigate(page) {
  document.querySelector('.sidebar').classList.remove('open');
  document.getElementById('sidebarOverlay').classList.remove('open');
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
  document.getElementById('page-' + page).classList.add('active');
  event.currentTarget.classList.add('active');

  if (page === 'dashboard') loadDashboard();
  if (page === 'documents') loadDocumentsPage();
  if (page === 'routing') loadRoutingPage();
  if (page === 'users') loadUsersPage();
  if (page === 'settings') renderSettings();
    if (page === 'departments') loadDepartmentsPage();
    if (page === 'workflow') loadWorkflowPage();

}

// ─── TOAST ─────────────────────────────────────────────────
function showToast(msg, type = '') {
  const t = document.getElementById('toast');
  t.textContent = msg;
  t.className = 'toast show ' + type;
  setTimeout(() => t.className = 'toast', 3000);
}

// ─── HELPERS ───────────────────────────────────────────────
function statusBadge(status) {
  const s = statusMap[status] || { label: 'Unknown', cls: 'badge-draft' };
  return `<span class="badge ${s.cls}">${s.label}</span>`;
}

function timeAgo(dateStr) {
  if (!dateStr) return '—';
  const diff = Date.now() - new Date(dateStr).getTime();
  const m = Math.floor(diff / 60000);
  if (m < 1) return 'just now';
  if (m < 60) return m + 'm ago';
  const h = Math.floor(m / 60);
  if (h < 24) return h + 'h ago';
  return Math.floor(h / 24) + 'd ago';
}

function docTable(docs, limit) {
  if (!docs.length) return `<div class="empty-state"><p>No documents found.</p></div>`;
  const rows = (limit ? docs.slice(0, limit) : docs).map((d, i) => `
    <tr style="cursor:pointer" onclick="openDetail('${d.id}')">
      <td class="doc-id">DOC-${String(i + 1).padStart(3, '0')}</td>
      <td class="doc-title">${d.title || '(No title)'}</td>
      <td>${statusBadge(d.status)}</td>
      <td>${d.owner ? d.owner.fullName || d.owner.username : '—'}</td>
      <td>${d.createdAt ? new Date(d.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '—'}</td>
      <td>${timeAgo(d.updatedAt || d.createdAt)}</td>
    </tr>
  `).join('');
  return `<table>
    <thead><tr><th>ID</th><th>Title</th><th>Status</th><th>Owner</th><th>Created</th><th>Updated</th></tr></thead>
    <tbody>${rows}</tbody>
  </table>`;
}

// ─── DASHBOARD ─────────────────────────────────────────────
async function loadDashboard() {
  try {
    const res = await fetch(`${API}/Documents`);
    allDocs = await res.json();
    document.getElementById('stat-total').textContent = allDocs.length;
    document.getElementById('stat-review').textContent = allDocs.filter(d => d.status === 1).length;
    document.getElementById('stat-approved').textContent = allDocs.filter(d => d.status === 2).length;
    document.getElementById('stat-pending').textContent = allDocs.filter(d => d.status === 3).length;
    document.getElementById('dashboard-table-wrap').innerHTML = docTable(allDocs, 5);
  } catch (e) {
    document.getElementById('dashboard-table-wrap').innerHTML = `<div class="empty-state"><p>Could not connect to API. Make sure your backend is running on port 5225.</p></div>`;
  }
}

// ─── DOCUMENTS ─────────────────────────────────────────────
async function loadDocumentsPage() {
  document.getElementById('docs-table-wrap').innerHTML = '<div class="loading">Loading...</div>';
  try {
    const res = await fetch(`${API}/Documents`);
    allDocs = await res.json();
    document.getElementById('docs-table-wrap').innerHTML = docTable(allDocs);
  } catch (e) {
    document.getElementById('docs-table-wrap').innerHTML = `<div class="empty-state"><p>Failed to load documents.</p></div>`;
  }
}

// ─── USERS ─────────────────────────────────────────────────
async function loadUsersPage() {
  document.getElementById('users-grid').innerHTML = '<div class="loading">Loading...</div>';
  try {
    const res = await fetch(`${API}/Users`);
    allUsers = await res.json();
    if (!allUsers.length) {
      document.getElementById('users-grid').innerHTML = `<div class="empty-state"><p>No users yet.</p></div>`;
      return;
    }
    document.getElementById('users-grid').innerHTML = allUsers.map(u => `
      <div class="user-card">
        <div class="user-card-avatar">${(u.fullName || u.username || '?')[0].toUpperCase()}</div>
        <div>
          <div class="user-card-name">${u.fullName || '—'}</div>
          <div class="user-card-email">${u.email || '—'}</div>
          <div class="user-card-username">@${u.username || '—'}</div>
        </div>
      </div>
    `).join('');
  } catch (e) {
    document.getElementById('users-grid').innerHTML = `<div class="empty-state"><p>Failed to load users.</p></div>`;
  }
}

// ─── ROUTING ───────────────────────────────────────────────
async function loadRoutingPage() {
  try {
    const [docsRes, usersRes] = await Promise.all([
      fetch(`${API}/Documents`),
      fetch(`${API}/Users`)
    ]);
    allDocs = await docsRes.json();
    allUsers = await usersRes.json();

    document.getElementById('route-doc-id').innerHTML = '<option value="">Select a document...</option>' + allDocs.map(d => `<option value="${d.id}">${d.title || d.id}</option>`).join('');
    document.getElementById('route-from-user').innerHTML = '<option value="">Select user...</option>' + allUsers.map(u => `<option value="${u.id}">${u.fullName || u.username}</option>`).join('');
    document.getElementById('route-to-user').innerHTML = '<option value="">Select user...</option>' + allUsers.map(u => `<option value="${u.id}">${u.fullName || u.username}</option>`).join('');
  } catch (e) {}
  loadAuditLog();
}

async function loadAuditLog() {
  try {
    const res = await fetch(`${API}/audit`);
    const logs = await res.json();
    if (!logs.length) {
      document.getElementById('audit-table-wrap').innerHTML = `<div class="empty-state"><p>No routing events yet.</p></div>`;
      return;
    }
    document.getElementById('audit-table-wrap').innerHTML = `<table>
      <thead><tr><th>Document ID</th><th>Status After</th><th>Note</th><th>Time</th></tr></thead>
      <tbody>${logs.map(l => `
        <tr>
          <td class="doc-id">${l.documentId.slice(0, 8)}...</td>
          <td>${statusBadge(l.statusAfter)}</td>
          <td>${l.note || '—'}</td>
          <td>${timeAgo(l.timestamp)}</td>
        </tr>
      `).join('')}</tbody>
    </table>`;
  } catch (e) {
    document.getElementById('audit-table-wrap').innerHTML = `<div class="empty-state"><p>Failed to load audit log.</p></div>`;
  }
}

async function submitRouting() {
  const docId = document.getElementById('route-doc-id').value;
  const fromUserId = document.getElementById('route-from-user').value;
  const toUserId = document.getElementById('route-to-user').value;
  const status = parseInt(document.getElementById('route-status').value);
  const note = document.getElementById('route-note').value;

  if (!docId) return showToast('Please select a document', 'error');

  try {
    const res = await fetch(`${API}/Documents/${docId}/routing`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fromUserId: fromUserId || null, toUserId: toUserId || null, note, statusAfter: status })
    });
    if (res.ok) {
      showToast('Document routed successfully!', 'success');
        sendPushToAll('Document Routed', 'A document status has been updated in DocuTrack.');
        // Send email notification if enabled
        const notifEmail = localStorage.getItem('notifEmail');
        if (notifEmail) {
            fetch(`${API}/push/email`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    toEmail: notifEmail,
                    subject: 'DocuTrack - Document Routed',
                    body: `<h2>Document Status Updated</h2><p>A document has been routed in DocuTrack.</p><p><strong>Note:</strong> ${note || 'No note provided'}</p><p>Login to DocuTrack to view the full details.</p><br/><a href="https://docutrack-production.up.railway.app">Open DocuTrack</a>`
                })
            });
        }
      loadAuditLog();
    } else {
      showToast('Failed to route document', 'error');
    }
  } catch (e) {
    showToast('API error', 'error');
  }
}

// ─── DEPARTMENTS ───────────────────────────────────────────
async function loadDepartmentsPage() {
  document.getElementById('dept-table-wrap').innerHTML = '<div class="loading">Loading...</div>';
  try {
    const res = await fetch(`${API}/Departments`);
    const depts = await res.json();
    if (!depts.length) {
      document.getElementById('dept-table-wrap').innerHTML = `<div class="empty-state"><p>No departments yet.</p></div>`;
      return;
    }
    document.getElementById('dept-table-wrap').innerHTML = `
      <table>
        <thead><tr><th>Name</th><th>Description</th><th>Members</th><th>Action</th></tr></thead>
        <tbody>${depts.map(d => `
          <tr>
            <td style="font-weight:600">${d.name || '—'}</td>
            <td style="color:var(--text-muted)">${d.description || '—'}</td>
            <td>${d.users ? d.users.length : 0} members</td>
            <td>
              <button onclick="deleteDept('${d.id}')" style="background:var(--red-bg);color:var(--red);border:none;padding:5px 12px;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer">Delete</button>
            </td>
          </tr>
        `).join('')}</tbody>
      </table>`;
  } catch (e) {
    document.getElementById('dept-table-wrap').innerHTML = `<div class="empty-state"><p>Failed to load departments.</p></div>`;
  }
}

function openNewDeptModal() {
  document.getElementById('newDeptModal').classList.add('open');
}

async function createDepartment() {
  const name = document.getElementById('dept-name').value;
  const description = document.getElementById('dept-desc').value;
  if (!name) return showToast('Name is required', 'error');
  try {
    const res = await fetch(`${API}/Departments`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, description })
    });
    if (res.ok) {
      showToast('Department created!', 'success');
      closeModal('newDeptModal');
      document.getElementById('dept-name').value = '';
      document.getElementById('dept-desc').value = '';
      loadDepartmentsPage();
    } else {
      showToast('Failed to create department', 'error');
    }
  } catch (e) {
    showToast('API error', 'error');
  }
}

async function deleteDept(id) {
  if (!confirm('Delete this department?')) return;
  try {
    const res = await fetch(`${API}/Departments/${id}`, { method: 'DELETE' });
    if (res.ok) {
      showToast('Department deleted!', 'success');
      loadDepartmentsPage();
    } else {
      showToast('Failed to delete', 'error');
    }
  } catch (e) {
    showToast('API error', 'error');
  }
}

// ─── SETTINGS ──────────────────────────────────────────────
function renderSettings() {
    document.getElementById('settings-list').innerHTML = settings.map((s, i) => {
        return `<div class="setting-item">
        <span class="setting-label">${s}</span>
        <button class="toggle ${settingsState[s] ? 'on' : ''}" id="toggle-${i}" data-name="${s}" data-index="${i}"></button>
      </div>`;
    }).join('');

    // Add event listeners after rendering
    document.querySelectorAll('.toggle').forEach(btn => {
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            const name = this.dataset.name;
            const index = parseInt(this.dataset.index);
            window.toggleSetting(name, index);
        });
    });
}

let inactivityTimer = null;

window.toggleSetting = function (name, index) {
    if (name === 'Email Notifications') {
        if (!settingsState[name]) {
            settingsState[name] = true;
            document.getElementById('emailModal').classList.add('open');
        } else {
            settingsState[name] = false;
            localStorage.removeItem('notifEmail');
            const btn = document.getElementById('toggle-' + index);
            if (btn) btn.classList.remove('on');
            showToast('Email notifications disabled', '');
        }
        return;
    }

    settingsState[name] = !settingsState[name];
    const btn = document.getElementById('toggle-' + index);
    if (btn) btn.classList.toggle('on', settingsState[name]);

// rest of the function stays the same...

    if (name === 'Dark Mode') {
        document.body.classList.toggle('dark-mode', settingsState[name]);
        showToast(settingsState[name] ? 'Dark mode on' : 'Dark mode off', 'success');
    }

    if (name === 'Automatically Logout after Inactivity') {
        if (settingsState[name]) {
            showToast('Auto logout enabled — 2 mins inactivity', 'success');
            resetInactivityTimer();
            document.addEventListener('mousemove', resetInactivityTimer);
            document.addEventListener('keypress', resetInactivityTimer);
            document.addEventListener('click', resetInactivityTimer);
        } else {
            clearTimeout(inactivityTimer);
            document.removeEventListener('mousemove', resetInactivityTimer);
            document.removeEventListener('keypress', resetInactivityTimer);
            document.removeEventListener('click', resetInactivityTimer);
            showToast('Auto logout disabled', '');
        }
    }

    if (name === 'Highlight Recently Updated Documents') {
        if (settingsState[name]) {
            highlightRecentDocs();
            showToast('Highlighting recently updated docs', 'success');
        } else {
            document.querySelectorAll('.recent-highlight').forEach(el => el.classList.remove('recent-highlight'));
            showToast('Highlight disabled', '');
        }
    }
}

function resetInactivityTimer() {
  clearTimeout(inactivityTimer);
  inactivityTimer = setTimeout(() => {
    if (settingsState['Automatically Logout after Inactivity']) {
      showToast('Logged out due to inactivity!', 'error');
      setTimeout(() => location.reload(), 2000);
    }
  }, 2 * 60 * 1000);
}

function highlightRecentDocs() {
  document.querySelectorAll('tr').forEach(row => {
    const timeCell = row.querySelector('td:last-child');
    if (timeCell && (timeCell.textContent.includes('ago') || timeCell.textContent.includes('now'))) {
      const text = timeCell.textContent;
      if (text.includes('m ago') || text.includes('just now') || text.includes('h ago')) {
        row.classList.add('recent-highlight');
      }
    }
  });
}

// ─── MODALS ────────────────────────────────────────────────
function openNewDocModal() {
  fetch(`${API}/Users`).then(r => r.json()).then(users => {
    allUsers = users;
    document.getElementById('doc-owner').innerHTML = '<option value="">Select owner...</option>' + users.map(u => `<option value="${u.id}">${u.fullName || u.username}</option>`).join('');
  }).catch(() => {});
  document.getElementById('newDocModal').classList.add('open');
}

function openNewUserModal() {
  document.getElementById('newUserModal').classList.add('open');
}

function closeModal(id) {
  document.getElementById(id).classList.remove('open');
}

async function createDocument() {
  const title = document.getElementById('doc-title').value;
  const content = document.getElementById('doc-content').value;
  const ownerId = document.getElementById('doc-owner').value;
  if (!title) return showToast('Title is required', 'error');
  try {
    const res = await fetch(`${API}/Documents`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content, ownerId: ownerId || null })
    });
    if (res.ok) {
      showToast('Document created!', 'success');
      closeModal('newDocModal');
      document.getElementById('doc-title').value = '';
      document.getElementById('doc-content').value = '';
      loadDashboard();
    } else {
      const err = await res.json();
      showToast(err.error || 'Failed to create document', 'error');
    }
  } catch (e) {
    showToast('API error', 'error');
  }
}

async function createUser() {
  const fullName = document.getElementById('user-fullname').value;
  const username = document.getElementById('user-username').value;
  const email = document.getElementById('user-email').value;
  if (!username) return showToast('Username is required', 'error');
  try {
    const res = await fetch(`${API}/Users`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fullName, username, email })
    });
    if (res.ok) {
      showToast('User created!', 'success');
      closeModal('newUserModal');
      document.getElementById('user-fullname').value = '';
      document.getElementById('user-username').value = '';
      document.getElementById('user-email').value = '';
      loadUsersPage();
    } else {
      showToast('Failed to create user', 'error');
    }
  } catch (e) {
    showToast('API error', 'error');
  }
}

// ─── DETAIL PANEL ──────────────────────────────────────────
async function openDetail(docId) {
  try {
    const res = await fetch(`${API}/Documents/${docId}`);
    const doc = await res.json();

    document.getElementById('detail-title').textContent = doc.title || 'Document Details';
    document.getElementById('detail-body').innerHTML = `
      <div class="detail-section">
        <div class="detail-section-title">Document Info</div>
        <div class="detail-field">
          <div class="detail-field-label">Title</div>
          <div class="detail-field-value">${doc.title || '—'}</div>
        </div>
        <div class="detail-field">
          <div class="detail-field-label">Content</div>
          <div class="detail-field-value" style="font-weight:400;line-height:1.6">${doc.content || '—'}</div>
        </div>
        <div class="detail-field">
          <div class="detail-field-label">Status</div>
          <div class="detail-field-value">${statusBadge(doc.status)}</div>
        </div>
        <div class="detail-field">
          <div class="detail-field-label">Owner</div>
          <div class="detail-field-value">${doc.owner ? doc.owner.fullName || doc.owner.username : '—'}</div>
        </div>
        <div class="detail-field">
          <div class="detail-field-label">Created</div>
          <div class="detail-field-value">${doc.createdAt ? new Date(doc.createdAt).toLocaleString() : '—'}</div>
        </div>
        <div class="detail-field">
          <div class="detail-field-label">Last Updated</div>
          <div class="detail-field-value">${doc.updatedAt ? new Date(doc.updatedAt).toLocaleString() : '—'}</div>
        </div>
        <div class="detail-section">
        <div class="detail-section-title">Workflow</div>
        <button onclick="triggerWorkflow('${doc.id}')" class="btn-primary" style="width:100%;justify-content:center">
          ⚡ Trigger Workflow
        </button>
      </div>
      </div>
      <div class="detail-section">
        <div class="detail-section-title">QR Code</div>
        <div style="font-size:12px;color:var(--text-muted);margin-bottom:10px">Scan to open this document</div>
        <div id="qr-code-container" style="width:140px;height:140px;"></div>
      </div>
      <div class="detail-section">
        <div class="detail-section-title">Routing History</div>
        <div class="timeline" id="detail-timeline">
          <div class="loading">Loading history...</div>
        </div>
      </div>
    `;

    setTimeout(() => {
      const qrContainer = document.getElementById('qr-code-container');
      qrContainer.innerHTML = '';
      new QRCode(qrContainer, {
        text: `${window.location.origin}/index.html#document/${doc.id}`,
        width: 140,
        height: 140,
        colorDark: '#4F46E5',
        colorLight: '#ffffff',
        correctLevel: QRCode.CorrectLevel.H
      });
    }, 100);

    document.getElementById('detailOverlay').classList.add('open');
    document.getElementById('detailPanel').classList.add('open');

    const histRes = await fetch(`${API}/Documents/${docId}/routing`);
    const history = await histRes.json();
    const timeline = document.getElementById('detail-timeline');

    if (!history.length) {
      timeline.innerHTML = `<div style="font-size:13px;color:var(--text-muted)">No routing history yet.</div>`;
    } else {
      timeline.innerHTML = history.map(h => `
        <div class="timeline-item">
          <div class="timeline-dot"></div>
          <div class="timeline-content">
            <div class="timeline-status">${statusBadge(h.statusAfter)}</div>
            <div class="timeline-note">${h.note || 'No note'}</div>
            <div class="timeline-time">${new Date(h.timestamp).toLocaleString()}</div>
          </div>
        </div>
      `).join('');
    }
  } catch (e) {
    showToast('Failed to load document', 'error');
  }
}

function closeDetail() {
  document.getElementById('detailOverlay').classList.remove('open');
  document.getElementById('detailPanel').classList.remove('open');
}

// ─── SEARCH ────────────────────────────────────────────────
function handleSearch(val) {
  if (!val) { loadDashboard(); return; }
  const filtered = allDocs.filter(d =>
    (d.title || '').toLowerCase().includes(val.toLowerCase()) ||
    (d.content || '').toLowerCase().includes(val.toLowerCase())
  );
  const wrap = document.getElementById('dashboard-table-wrap');
  if (wrap) wrap.innerHTML = docTable(filtered, 5);
}

// ─── NOTIFICATION DROPDOWN ─────────────────────────────────
async function toggleNotifDropdown() {
  const dropdown = document.getElementById('notifDropdown');
  dropdown.classList.toggle('open');
  document.getElementById('userDropdown').classList.remove('open');

  if (dropdown.classList.contains('open')) {
    try {
      const res = await fetch(`${API}/audit`);
      const logs = await res.json();
      const notifList = document.getElementById('notif-list');

      if (!logs.length) {
        notifList.innerHTML = `<div style="padding:16px;text-align:center;font-size:13px;color:var(--text-muted)">No notifications yet.</div>`;
        return;
      }

      notifList.innerHTML = logs.slice(0, 10).map(l => {
        const doc = allDocs.find(d => d.id === l.documentId);
        const docTitle = doc ? doc.title : 'Document';
        return `
          <div class="notif-item" onclick="openDetail('${l.documentId}');toggleNotifDropdown()">
            <div class="notif-item-title">${docTitle}</div>
            <div class="notif-item-desc">Status changed to ${statusMap[l.statusAfter]?.label || 'Unknown'}${l.note ? ' — ' + l.note : ''}</div>
            <div class="notif-item-time">${timeAgo(l.timestamp)}</div>
          </div>
        `;
      }).join('');

      document.getElementById('notif-dot').style.display = 'none';
    } catch (e) {
      document.getElementById('notif-list').innerHTML = `<div style="padding:16px;text-align:center;font-size:13px;color:var(--text-muted)">Failed to load.</div>`;
    }
  }
}

function clearNotifications() {
  document.getElementById('notif-list').innerHTML = `<div style="padding:16px;text-align:center;font-size:13px;color:var(--text-muted)">No notifications.</div>`;
  document.getElementById('notifDropdown').classList.remove('open');
}

// ─── USER DROPDOWN ─────────────────────────────────────────
function toggleUserDropdown() {
  const dropdown = document.getElementById('userDropdown');
  dropdown.classList.toggle('open');
  document.getElementById('notifDropdown').classList.remove('open');
}

function showProfile() {
  document.getElementById('userDropdown').classList.remove('open');
  showToast('Profile feature coming soon!', '');
}

function handleLogout() {
  document.getElementById('userDropdown').classList.remove('open');
  showToast('Logged out!', 'success');
}

// ─── PUSH NOTIFICATIONS ────────────────────────────────────
const API_PUBLIC_KEY = 'BNrYOZcMohfyAHmihLHiBUKvZ6n4qaLmxLOCwYlM6ZRp2iUzGf92Fcu6e0AEtKsBAzLTwntzU5GGQL7itlbXaec';

function urlBase64ToUint8Array(base64String) {
  const padding = '='.repeat((4 - base64String.length % 4) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) outputArray[i] = rawData.charCodeAt(i);
  return outputArray;
}

async function registerPush() {
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;
  try {
    const reg = await navigator.serviceWorker.register('/sw.js');
    console.log('Service worker registered');
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') return;
    const sub = await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(API_PUBLIC_KEY)
    });
    const subJson = sub.toJSON();
    await fetch(`${API}/push/subscribe`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ endpoint: subJson.endpoint, p256dh: subJson.keys.p256dh, auth: subJson.keys.auth })
    });
    showToast('Push notifications enabled!', 'success');
  } catch (e) {
    console.error('Push registration failed', e);
  }
}

async function sendPushToAll(title, message) {
  try {
    await fetch(`${API}/push/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, message })
    });
  } catch (e) {
    console.error('Push send failed', e);
  }
}

// ─── CLOSE DROPDOWNS ON OUTSIDE CLICK ─────────────────────
document.addEventListener('click', function (e) {
  if (!e.target.closest('.notif-btn') && !e.target.closest('.notif-dropdown')) {
    document.getElementById('notifDropdown').classList.remove('open');
  }
  if (!e.target.closest('.user-chip')) {
    document.getElementById('userDropdown').classList.remove('open');
  }
});

document.querySelectorAll('.modal-overlay').forEach(o => {
  o.addEventListener('click', e => { if (e.target === o) o.classList.remove('open'); });
});
// ─── WORKFLOW ──────────────────────────────────────────────
async function loadWorkflowPage() {
    document.getElementById('workflow-rules-wrap').innerHTML = '<div class="loading">Loading...</div>';
    try {
        const [rulesRes, usersRes] = await Promise.all([
            fetch(`${API}/workflow`),
            fetch(`${API}/Users`)
        ]);
        const rules = await rulesRes.json();
        allUsers = await usersRes.json();

        document.getElementById('workflow-user').innerHTML = '<option value="">Select user...</option>' + allUsers.map(u => `<option value="${u.id}">${u.fullName || u.username}</option>`).join('');

        if (!rules.length) {
            document.getElementById('workflow-rules-wrap').innerHTML = `<div class="empty-state"><p>No workflow rules yet. Create one above!</p></div>`;
            return;
        }

        document.getElementById('workflow-rules-wrap').innerHTML = `
      <table>
        <thead><tr><th>Name</th><th>Trigger Status</th><th>Next Status</th><th>Assign To</th><th>Note</th><th>Active</th><th>Action</th></tr></thead>
        <tbody>${rules.map(r => `
          <tr>
            <td style="font-weight:600">${r.name}</td>
            <td>${statusBadge(r.triggerStatus)}</td>
            <td>${statusBadge(r.nextStatus)}</td>
            <td>${r.assignToUser ? r.assignToUser.fullName || r.assignToUser.username : '—'}</td>
            <td style="color:var(--text-muted)">${r.note || '—'}</td>
            <td>
              <button onclick="toggleRule('${r.id}')" style="background:${r.isActive ? 'var(--green-bg)' : 'var(--gray-bg)'};color:${r.isActive ? '#065F46' : 'var(--gray)'};border:none;padding:5px 12px;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer">
                ${r.isActive ? 'Active' : 'Inactive'}
              </button>
            </td>
            <td>
              <button onclick="deleteRule('${r.id}')" style="background:var(--red-bg);color:var(--red);border:none;padding:5px 12px;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer">Delete</button>
            </td>
          </tr>
        `).join('')}</tbody>
      </table>`;
    } catch (e) {
        document.getElementById('workflow-rules-wrap').innerHTML = `<div class="empty-state"><p>Failed to load workflow rules.</p></div>`;
    }
}

async function createWorkflowRule() {
    const name = document.getElementById('workflow-name').value;
    const triggerStatus = parseInt(document.getElementById('workflow-trigger').value);
    const nextStatus = parseInt(document.getElementById('workflow-next').value);
    const assignToUserId = document.getElementById('workflow-user').value;
    const note = document.getElementById('workflow-note').value;

    if (!name) return showToast('Name is required', 'error');

    try {
        const res = await fetch(`${API}/workflow`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, triggerStatus, nextStatus, assignToUserId: assignToUserId || null, note })
        });
        if (res.ok) {
            showToast('Workflow rule created!', 'success');
            document.getElementById('workflow-name').value = '';
            document.getElementById('workflow-note').value = '';
            loadWorkflowPage();
        } else {
            showToast('Failed to create rule', 'error');
        }
    } catch (e) {
        showToast('API error', 'error');
    }
}

async function toggleRule(id) {
    try {
        await fetch(`${API}/workflow/${id}/toggle`, { method: 'PATCH' });
        loadWorkflowPage();
    } catch (e) {
        showToast('API error', 'error');
    }
}

async function deleteRule(id) {
    if (!confirm('Delete this rule?')) return;
    try {
        const res = await fetch(`${API}/workflow/${id}`, { method: 'DELETE' });
        if (res.ok) {
            showToast('Rule deleted!', 'success');
            loadWorkflowPage();
        }
    } catch (e) {
        showToast('API error', 'error');
    }
}

async function triggerWorkflow(docId) {
    try {
        const res = await fetch(`${API}/workflow/trigger/${docId}`, { method: 'POST' });
        const data = await res.json();
        showToast(data.message, 'success');
        sendPushToAll('Workflow Triggered', data.message);
    } catch (e) {
        showToast('API error', 'error');
    }
}
function cancelEmailNotif() {
    settingsState['Email Notifications'] = false;
    const btn = document.getElementById('toggle-Email-Notifications');
    if (btn) btn.classList.remove('on');
    closeModal('emailModal');
}

function saveEmailNotif() {
    const email = document.getElementById('notif-email-input').value;
    if (!email) return showToast('Email is required', 'error');
    localStorage.setItem('notifEmail', email);
    showToast('Email notifications enabled for ' + email, 'success');
    closeModal('emailModal');
}
// ─── INITIAL LOAD ──────────────────────────────────────────
loadDashboard();
renderSettings();
registerPush();
