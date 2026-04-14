async function getJson(url, options) {
    const response = await fetch(url, options);
    if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
    }
    return await response.json();
}

function penceToGbp(amountMinor) {
    return `£${(amountMinor / 100).toFixed(2)}`;
}

async function loadLookups() {
    const [users, categories, statuses] = await Promise.all([
        getJson('/api/lookups/users'),
        getJson('/api/lookups/categories'),
        getJson('/api/lookups/statuses')
    ]);

    const userSelect = document.getElementById('userId');
    const userFilter = document.getElementById('userFilter');
    const managerSelect = document.getElementById('managerUserId');
    for (const user of users) {
        userSelect.add(new Option(`${user.userName} (${user.roleName})`, user.userId));
        userFilter.add(new Option(user.userName, user.userId));
        if ((user.roleName || '').toLowerCase() === 'manager') {
            managerSelect.add(new Option(user.userName, user.userId));
        }
    }
    if (managerSelect.options.length === 0 && users.length > 0) {
        managerSelect.add(new Option(users[0].userName, users[0].userId));
    }

    const categorySelect = document.getElementById('categoryId');
    for (const category of categories) {
        categorySelect.add(new Option(category.categoryName, category.categoryId));
    }

    const statusFilter = document.getElementById('statusFilter');
    for (const status of statuses) {
        statusFilter.add(new Option(status.statusName, status.statusId));
    }
}

async function loadExpenses() {
    const statusId = document.getElementById('statusFilter').value;
    const userId = document.getElementById('userFilter').value;

    const query = new URLSearchParams();
    if (statusId) query.set('statusId', statusId);
    if (userId) query.set('userId', userId);

    const expenses = await getJson(`/api/expenses?${query.toString()}`);
    const tbody = document.getElementById('expenses-body');
    tbody.innerHTML = '';

    for (const item of expenses) {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${item.expenseId}</td>
            <td>${item.userName}</td>
            <td>${item.categoryName}</td>
            <td>${item.statusName}</td>
            <td>${penceToGbp(item.amountMinor)}</td>
            <td>${item.expenseDate}</td>
            <td>${item.description ?? ''}</td>
            <td>
                <button class="btn btn-secondary" data-action="approve" data-id="${item.expenseId}">Approve</button>
                <button class="btn btn-secondary" data-action="reject" data-id="${item.expenseId}">Reject</button>
            </td>`;
        tbody.appendChild(tr);
    }
}

async function reviewExpense(expenseId, approve) {
    const managerUserId = Number(document.getElementById('managerUserId').value);
    await getJson(`/api/expenses/${expenseId}/review`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ managerUserId, approve })
    });
    await loadExpenses();
}

async function createExpense(event) {
    event.preventDefault();

    const payload = {
        userId: Number(document.getElementById('userId').value),
        categoryId: Number(document.getElementById('categoryId').value),
        amountMinor: Math.round(Number(document.getElementById('amountGbp').value) * 100),
        expenseDate: document.getElementById('expenseDate').value,
        description: document.getElementById('description').value
    };

    await getJson('/api/expenses', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    event.target.reset();
    await loadExpenses();
}

window.addEventListener('click', async (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;
    const action = target.dataset.action;
    const id = Number(target.dataset.id);
    if (action === 'approve' && id) await reviewExpense(id, true);
    if (action === 'reject' && id) await reviewExpense(id, false);
});

window.addEventListener('load', async () => {
    document.getElementById('expenseDate').valueAsDate = new Date();
    document.getElementById('create-expense-form').addEventListener('submit', createExpense);
    document.getElementById('reload-btn').addEventListener('click', loadExpenses);
    document.getElementById('statusFilter').addEventListener('change', loadExpenses);
    document.getElementById('userFilter').addEventListener('change', loadExpenses);
    await loadLookups();
    await loadExpenses();
});
