const apiBase = window.APP_API_BASE || '';
const messages = document.getElementById('messages');
const form = document.getElementById('form');
const input = document.getElementById('input');

function escapeHtml(text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function formatMessage(text) {
  const escaped = escapeHtml(text);
  const lines = escaped.split('\n');
  const out = [];
  let inUl = false;
  let inOl = false;

  for (const line of lines) {
    if (/^\d+\.\s+/.test(line)) {
      if (inUl) { out.push('</ul>'); inUl = false; }
      if (!inOl) { out.push('<ol>'); inOl = true; }
      out.push(`<li>${line.replace(/^\d+\.\s+/, '')}</li>`);
      continue;
    }

    if (/^[-*]\s+/.test(line)) {
      if (inOl) { out.push('</ol>'); inOl = false; }
      if (!inUl) { out.push('<ul>'); inUl = true; }
      out.push(`<li>${line.replace(/^[-*]\s+/, '')}</li>`);
      continue;
    }

    if (inOl) { out.push('</ol>'); inOl = false; }
    if (inUl) { out.push('</ul>'); inUl = false; }
    out.push(line.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>') + '<br>');
  }

  if (inOl) out.push('</ol>');
  if (inUl) out.push('</ul>');
  return out.join('');
}

function addMessage(text, role) {
  const div = document.createElement('div');
  div.className = `message ${role}`;
  div.innerHTML = formatMessage(text);
  messages.appendChild(div);
}

form.addEventListener('submit', async (e) => {
  e.preventDefault();
  const text = input.value.trim();
  if (!text) return;

  addMessage(text, 'user');
  input.value = '';

  const res = await fetch(`${apiBase}/api/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message: text })
  });

  if (!res.ok) {
    addMessage(`Request failed: ${res.status}`, 'bot');
    return;
  }

  const payload = await res.json();
  addMessage(payload.reply, 'bot');
});

addMessage('Chat UI ready. It uses backend APIs and function-calling orchestration.', 'bot');
