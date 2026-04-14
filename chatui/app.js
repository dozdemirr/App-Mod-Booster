function escapeHtml(value) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function formatMessage(rawText) {
  const escaped = escapeHtml(rawText || '');
  const lines = escaped.split('\n');
  let html = '';
  let inUl = false;
  let inOl = false;

  for (const line of lines) {
    const numbered = line.match(/^\d+\.\s+(.+)/);
    const bulleted = line.match(/^[-*]\s+(.+)/);

    if (numbered) {
      if (!inOl) { html += '<ol>'; inOl = true; }
      if (inUl) { html += '</ul>'; inUl = false; }
      html += `<li>${numbered[1]}</li>`;
      continue;
    }

    if (bulleted) {
      if (!inUl) { html += '<ul>'; inUl = true; }
      if (inOl) { html += '</ol>'; inOl = false; }
      html += `<li>${bulleted[1]}</li>`;
      continue;
    }

    if (inUl) { html += '</ul>'; inUl = false; }
    if (inOl) { html += '</ol>'; inOl = false; }
    html += `${line}<br>`;
  }

  if (inUl) html += '</ul>';
  if (inOl) html += '</ol>';
  return html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
}

function appendMessage(role, text) {
  const container = document.getElementById('messages');
  const messageDiv = document.createElement('div');
  messageDiv.className = `msg ${role}`;
  messageDiv.innerHTML = formatMessage(text);
  container.appendChild(messageDiv);
  container.scrollTop = container.scrollHeight;
}

document.getElementById('chat-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const prompt = document.getElementById('prompt');
  const text = prompt.value.trim();
  if (!text) return;

  appendMessage('user', text);
  prompt.value = '';

  try {
    const response = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: text })
    });
    const payload = await response.json();
    appendMessage('bot', payload.response ?? 'No response.');
  } catch (error) {
    appendMessage('bot', `Unable to contact chat API. ${error}`);
  }
});

appendMessage('bot', 'Hi! I can query expenses, create new expenses, and approve/reject items using real API functions.');
