const expenseTableBody = document.querySelector('#expenseTable tbody');
const errorBanner = document.getElementById('errorBanner');

async function fetchJson(url, options = undefined) {
  const response = await fetch(url, options);
  if (!response.ok) {
    throw new Error(await response.text());
  }
  if (response.status === 204) return null;
  return response.json();
}

function populateSelect(select, items, includeAll = true, allText = 'All') {
  select.innerHTML = '';
  if (includeAll) {
    const option = document.createElement('option');
    option.value = '';
    option.textContent = allText;
    select.appendChild(option);
  }
  for (const item of items) {
    const option = document.createElement('option');
    option.value = String(item.id);
    option.textContent = item.name;
    select.appendChild(option);
  }
}

function pounds(amountMinor) {
  return `£${(amountMinor / 100).toFixed(2)}`;
}

function buildQuery() {
  const params = new URLSearchParams();
  const status = document.getElementById('filterStatus').selectedOptions[0]?.text;
  const userId = document.getElementById('filterUser').value;
  const categoryId = document.getElementById('filterCategory').value;

  if (status && status !== 'All statuses') params.set('status', status);
  if (userId) params.set('userId', userId);
  if (categoryId) params.set('categoryId', categoryId);

  return params.toString();
}

async function loadErrorBanner() {
  const details = await fetchJson('/api/system/error');
  if (!details || !details.message) {
    errorBanner.classList.add('d-none');
    return;
  }

  errorBanner.textContent = `${details.message} (at ${new Date(details.timestamp).toLocaleString()})`;
  errorBanner.classList.remove('d-none');
}

async function loadExpenses() {
  const query = buildQuery();
  const url = query ? `/api/expenses?${query}` : '/api/expenses';
  const expenses = await fetchJson(url);

  expenseTableBody.innerHTML = '';
  for (const expense of expenses) {
    const row = document.createElement('tr');
    row.innerHTML = `
      <td>${expense.expenseId}</td>
      <td>${expense.userName}</td>
      <td>${expense.categoryName}</td>
      <td>${expense.statusName}</td>
      <td>${pounds(expense.amountMinor)}</td>
      <td>${expense.expenseDate}</td>
      <td>
        <button class="btn btn-sm btn-outline-success me-1" data-id="${expense.expenseId}" data-status="Approved">Approve</button>
        <button class="btn btn-sm btn-outline-danger" data-id="${expense.expenseId}" data-status="Rejected">Reject</button>
      </td>`;
    expenseTableBody.appendChild(row);
  }
}

document.getElementById('refreshBtn').addEventListener('click', async () => {
  await loadExpenses();
  await loadErrorBanner();
});

document.getElementById('createExpenseForm').addEventListener('submit', async (event) => {
  event.preventDefault();

  await fetchJson('/api/expenses', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      userId: Number(document.getElementById('userId').value),
      categoryId: Number(document.getElementById('categoryId').value),
      amountMinor: Number(document.getElementById('amountMinor').value),
      expenseDate: document.getElementById('expenseDate').value,
      description: document.getElementById('description').value
    })
  });

  await loadExpenses();
});

expenseTableBody.addEventListener('click', async (event) => {
  const button = event.target;
  if (!(button instanceof HTMLButtonElement)) return;

  const id = Number(button.dataset.id);
  const newStatus = button.dataset.status;
  if (!id || !newStatus) return;

  await fetchJson('/api/expenses/status', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ expenseId: id, newStatus, reviewedByUserId: Number(document.getElementById('userId').value || 2) })
  });

  await loadExpenses();
});

async function bootstrap() {
  const [categories, statuses, users] = await Promise.all([
    fetchJson('/api/lookup/categories'),
    fetchJson('/api/lookup/statuses'),
    fetchJson('/api/lookup/users')
  ]);

  populateSelect(document.getElementById('categoryId'), categories, false);
  populateSelect(document.getElementById('userId'), users, false);
  populateSelect(document.getElementById('filterCategory'), categories, true, 'All categories');
  populateSelect(document.getElementById('filterUser'), users, true, 'All users');
  populateSelect(document.getElementById('filterStatus'), statuses, true, 'All statuses');

  document.getElementById('expenseDate').valueAsDate = new Date();

  await loadExpenses();
  await loadErrorBanner();
}

bootstrap().catch(async () => {
  await loadErrorBanner();
});
