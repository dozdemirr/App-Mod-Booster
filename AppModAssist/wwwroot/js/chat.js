const chatMessages = document.getElementById('chatMessages');
const chatForm = document.getElementById('chatForm');
const chatInput = document.getElementById('chatInput');

function escapeHtml(text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function formatMessage(raw) {
  const escaped = escapeHtml(raw);
  const lines = escaped.split('\n');
  let html = '';
  let inUl = false;
  let inOl = false;

  for (const line of lines) {
    if (/^\d+\.\s+/.test(line)) {
      if (inUl) { html += '</ul>'; inUl = false; }
      if (!inOl) { html += '<ol>'; inOl = true; }
      html += `<li>${line.replace(/^\d+\.\s+/, '')}</li>`;
      continue;
    }

    if (/^[-*]\s+/.test(line)) {
      if (inOl) { html += '</ol>'; inOl = false; }
      if (!inUl) { html += '<ul>'; inUl = true; }
      html += `<li>${line.replace(/^[-*]\s+/, '')}</li>`;
      continue;
    }

    if (inUl) { html += '</ul>'; inUl = false; }
    if (inOl) { html += '</ol>'; inOl = false; }

    const strong = line.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
    html += `${strong}<br>`;
  }

  if (inUl) html += '</ul>';
  if (inOl) html += '</ol>';

  return html;
}

function addMessage(text, role) {
  const bubble = document.createElement('div');
  bubble.className = `chat-bubble ${role}`;
  bubble.innerHTML = formatMessage(text);
  chatMessages.appendChild(bubble);
  chatMessages.scrollTop = chatMessages.scrollHeight;
}

chatForm.addEventListener('submit', async (event) => {
  event.preventDefault();

  const text = chatInput.value.trim();
  if (!text) return;

  addMessage(text, 'user');
  chatInput.value = '';

  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message: text })
  });

  if (!response.ok) {
    addMessage(`Chat request failed (${response.status}).`, 'bot');
    return;
  }

  const payload = await response.json();
  addMessage(payload.reply, 'bot');
});

addMessage('Welcome! Ask me to list, create, approve, or reject expenses. If GenAI is not deployed, I will explain what to run.', 'bot');
