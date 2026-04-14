const chat = document.getElementById("chat");
const form = document.getElementById("chat-form");
const input = document.getElementById("message");

const escapeHtml = (value) => value
  .replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;")
  .replaceAll('"', "&quot;")
  .replaceAll("'", "&#39;");

const formatListText = (escapedText) => {
  const lines = escapedText.split("\n");
  let html = "";
  let inUl = false;
  let inOl = false;

  for (const line of lines) {
    if (/^\d+\.\s+/.test(line)) {
      if (!inOl) { html += "<ol>"; inOl = true; }
      if (inUl) { html += "</ul>"; inUl = false; }
      html += `<li>${line.replace(/^\d+\.\s+/, "")}</li>`;
      continue;
    }

    if (/^[-*]\s+/.test(line)) {
      if (!inUl) { html += "<ul>"; inUl = true; }
      if (inOl) { html += "</ol>"; inOl = false; }
      html += `<li>${line.replace(/^[-*]\s+/, "")}</li>`;
      continue;
    }

    if (inUl) { html += "</ul>"; inUl = false; }
    if (inOl) { html += "</ol>"; inOl = false; }
    html += `${line}<br>`;
  }

  if (inUl) html += "</ul>";
  if (inOl) html += "</ol>";
  return html.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
};

const addMessage = (text, role) => {
  const messageDiv = document.createElement("div");
  messageDiv.className = `bubble ${role}`;
  const escaped = escapeHtml(text ?? "");
  const formatted = formatListText(escaped);
  messageDiv.innerHTML = formatted;
  chat.appendChild(messageDiv);
  chat.scrollTop = chat.scrollHeight;
};

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const message = input.value.trim();
  if (!message) return;
  addMessage(message, "user");
  input.value = "";

  const response = await fetch("/api/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ message })
  });
  const payload = await response.json();
  addMessage(payload.reply ?? "No response.", "assistant");
});
