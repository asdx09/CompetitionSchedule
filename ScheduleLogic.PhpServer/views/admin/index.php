<?php /** @var \ScheduleLogic\Core\Csrf $csrf */ ?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>Admin - ScheduleLogic</title>
    <style>
        :root {
            --bg: #0f1420;
            --panel: #1a2233;
            --border: #2b3650;
            --text: #e6e9f0;
            --muted: #8b93a7;
            --ok: #3ecf8e;
            --bad: #e5484d;
            --accent: #4f7cff;
        }

        * {
            box-sizing: border-box;
        }

        body {
            margin: 0;
            padding: 32px;
            background: var(--bg);
            color: var(--text);
            font-family: system-ui, -apple-system, Segoe UI, sans-serif;
        }

        h1 {
            margin: 0 0 24px;
            font-size: 20px;
        }

        .topbar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 8px;
        }

            .topbar h1 {
                margin: 0;
            }

            .topbar button {
                width: auto;
                padding: 8px 14px;
            }

        h2 {
            font-size: 14px;
            text-transform: uppercase;
            letter-spacing: .05em;
            color: var(--muted);
            margin: 0 0 12px;
        }

        .stats {
            display: flex;
            gap: 16px;
            margin-bottom: 32px;
            flex-wrap: wrap;
        }

        .card {
            background: var(--panel);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 16px 20px;
            min-width: 160px;
        }

            .card .value {
                font-size: 28px;
                font-weight: 600;
            }

            .card .label {
                color: var(--muted);
                font-size: 13px;
                margin-top: 4px;
            }

        .dot {
            display: inline-block;
            width: 10px;
            height: 10px;
            border-radius: 50%;
            margin-right: 8px;
            vertical-align: middle;
        }

            .dot.ok {
                background: var(--ok);
            }

            .dot.bad {
                background: var(--bad);
            }

        .search-row {
            display: flex;
            gap: 8px;
            margin-bottom: 16px;
        }

        input[type=text] {
            flex: 1;
            background: var(--panel);
            border: 1px solid var(--border);
            border-radius: 6px;
            padding: 10px 12px;
            color: var(--text);
            font-size: 14px;
        }

        button {
            background: var(--accent);
            border: none;
            border-radius: 6px;
            padding: 10px 16px;
            color: white;
            font-size: 14px;
            cursor: pointer;
        }

            button.secondary {
                background: var(--panel);
                border: 1px solid var(--border);
            }

            button.danger {
                background: var(--bad);
            }

            button:disabled {
                opacity: .5;
                cursor: default;
            }

        table {
            width: 100%;
            border-collapse: collapse;
            background: var(--panel);
            border: 1px solid var(--border);
            border-radius: 8px;
            overflow: hidden;
        }

        th, td {
            text-align: left;
            padding: 10px 14px;
            font-size: 14px;
            border-bottom: 1px solid var(--border);
        }

        th {
            color: var(--muted);
            font-weight: 500;
            font-size: 12px;
            text-transform: uppercase;
        }

        tr:last-child td {
            border-bottom: none;
        }

        .badge {
            padding: 2px 8px;
            border-radius: 999px;
            font-size: 12px;
        }

            .badge.confirmed {
                background: rgba(62,207,142,.15);
                color: var(--ok);
            }

            .badge.pending {
                background: rgba(229,72,77,.15);
                color: var(--bad);
            }

        .row-actions {
            display: flex;
            gap: 8px;
        }

            .row-actions button {
                padding: 6px 10px;
                font-size: 13px;
            }

        .empty {
            color: var(--muted);
            padding: 24px;
            text-align: center;
        }

        .status-msg {
            margin-top: 12px;
            font-size: 13px;
            color: var(--muted);
        }
    </style>
</head>
<body>
    <div class="topbar">
        <h1>ScheduleLogic - Admin</h1>
        <form method="post" action="/admin/logout">
            <?= $csrf->field() ?>
            <button type="submit" class="secondary">Log out</button>
        </form>
    </div>

    <div class="stats" id="stats">
        <div class="card"><div class="value">-</div><div class="label">Users</div></div>
        <div class="card"><div class="value">-</div><div class="label">Database</div></div>
        <div class="card"><div class="value">-</div><div class="label">Solver</div></div>
    </div>

    <h2>Users</h2>
    <div class="search-row">
        <input type="text" id="searchInput" placeholder="Search by username, e-mail or ID..." />
        <button id="searchBtn">Search</button>
    </div>

    <table>
        <thead>
            <tr>
                <th>ID</th>
                <th>Username</th>
                <th>E-mail</th>
                <th>Registration</th>
                <th>Status</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody id="userRows"></tbody>
    </table>
    <div id="emptyMsg" class="empty" style="display:none;">No results found.</div>
    <div id="statusMsg" class="status-msg"></div>

    <script>
        const statsEl = document.getElementById('stats');
        const searchInput = document.getElementById('searchInput');
        const searchBtn = document.getElementById('searchBtn');
        const userRows = document.getElementById('userRows');
        const emptyMsg = document.getElementById('emptyMsg');
        const statusMsg = document.getElementById('statusMsg');

        function setStatus(text, isError) {
            statusMsg.textContent = text || '';
            statusMsg.style.color = isError ? 'var(--bad)' : 'var(--muted)';
        }

        async function loadStats() {
            try {
                const res = await fetch('/admin/api/stats');
                if (!res.ok) throw new Error('stats: ' + res.status);
                const s = await res.json();

                statsEl.innerHTML = `
                    <div class="card">
                        <div class="value">${s.userCount}</div>
                        <div class="label">Users</div>
                    </div>
                    <div class="card">
                        <div class="value"><span class="dot ${s.databaseAvailable ? 'ok' : 'bad'}"></span>${s.databaseAvailable ? 'Available' : 'Unavailable'}</div>
                        <div class="label">Database</div>
                    </div>
                    <div class="card">
                        <div class="value"><span class="dot ${s.solverAvailable ? 'ok' : 'bad'}"></span>${s.solverAvailable ? 'Available' : 'Unavailable'}</div>
                        <div class="label">Solver</div>
                    </div>
                `;
            } catch (err) {
                setStatus('Failed to load status: ' + err.message, true);
            }
        }

        function escapeHtml(str) {
            const div = document.createElement('div');
            div.textContent = str ?? '';
            return div.innerHTML;
        }

        async function loadUsers(query) {
            try {
                const url = '/admin/api/users' + (query ? '?q=' + encodeURIComponent(query) : '');
                const res = await fetch(url);
                if (!res.ok) throw new Error('users: ' + res.status);
                const users = await res.json();

                userRows.innerHTML = '';
                emptyMsg.style.display = users.length === 0 ? 'block' : 'none';

                for (const u of users) {
                    const tr = document.createElement('tr');
                    const regDate = new Date(u.registrationDate).toLocaleDateString('en-US');

                    tr.innerHTML = `
                        <td>${u.userId}</td>
                        <td>${escapeHtml(u.username)}</td>
                        <td>${escapeHtml(u.email)}</td>
                        <td>${regDate}</td>
                        <td>
                            <span class="badge ${u.emailConfirmed ? 'confirmed' : 'pending'}">
                                ${u.emailConfirmed ? 'Confirmed' : 'Pending'}
                            </span>
                        </td>
                        <td class="row-actions">
                            <button class="secondary" data-action="confirm" data-id="${u.userId}" ${u.emailConfirmed ? 'disabled' : ''}>
                                Confirm
                            </button>
                            <button class="danger" data-action="delete" data-id="${u.userId}" data-username="${escapeHtml(u.username)}">
                                Delete
                            </button>
                        </td>
                    `;
                    userRows.appendChild(tr);
                }
            } catch (err) {
                setStatus('Failed to load users: ' + err.message, true);
            }
        }

        userRows.addEventListener('click', async (e) => {
            const btn = e.target.closest('button[data-action]');
            if (!btn) return;

            const id = btn.dataset.id;
            const action = btn.dataset.action;

            if (action === 'confirm') {
                btn.disabled = true;
                try {
                    const res = await fetch(`/admin/api/users/${id}/confirm`, { method: 'POST' });
                    if (!res.ok) throw new Error('confirm: ' + res.status);
                    setStatus(`#${id} e-mail address confirmed.`);
                    loadUsers(searchInput.value.trim());
                } catch (err) {
                    setStatus('Confirmation failed: ' + err.message, true);
                    btn.disabled = false;
                }
            }

            if (action === 'delete') {
                const username = btn.dataset.username;
                if (!confirm(`Are you sure you want to delete the "${username}" (#${id}) account? This action cannot be undone.`)) return;

                btn.disabled = true;
                try {
                    const res = await fetch(`/admin/api/users/${id}`, { method: 'DELETE' });
                    if (!res.ok) throw new Error('delete: ' + res.status);
                    setStatus(`#${id} account deleted.`);
                    loadUsers(searchInput.value.trim());
                } catch (err) {
                    setStatus('Deletion failed: ' + err.message, true);
                    btn.disabled = false;
                }
            }
        });

        searchBtn.addEventListener('click', () => loadUsers(searchInput.value.trim()));
        searchInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') loadUsers(searchInput.value.trim());
        });

        loadStats();
        loadUsers('');
    </script>
</body>
</html>