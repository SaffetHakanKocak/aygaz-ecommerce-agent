const messagesEl = document.getElementById("messages");
const formEl = document.getElementById("chat-form");
const inputEl = document.getElementById("message-input");
const sendButtonEl = document.getElementById("send-button");
const clearButtonEl = document.getElementById("clear-button");
const loadingEl = document.getElementById("loading");
const errorEl = document.getElementById("error");
const suggestionButtons = document.querySelectorAll(".suggestion");

let sessionId = localStorage.getItem("aygazChatSessionId") || null;

function setLoading(isLoading) {
  loadingEl.classList.toggle("hidden", !isLoading);
  sendButtonEl.disabled = isLoading;
  clearButtonEl.disabled = isLoading;
  inputEl.disabled = isLoading;
}

function showError(message) {
  if (!message) {
    errorEl.classList.add("hidden");
    errorEl.textContent = "";
    return;
  }

  errorEl.textContent = message;
  errorEl.classList.remove("hidden");
}

function appendMessage(role, text, meta) {
  const bubble = document.createElement("div");
  bubble.className = `message ${role}`;
  bubble.textContent = text;

  if (meta) {
    const metaEl = document.createElement("span");
    metaEl.className = "message-meta";
    metaEl.textContent = meta;
    bubble.appendChild(metaEl);
  }

  messagesEl.appendChild(bubble);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

async function sendMessage(message) {
  const trimmed = message.trim();
  if (!trimmed) {
    return;
  }

  showError("");
  appendMessage("user", trimmed);
  inputEl.value = "";
  setLoading(true);

  try {
    const response = await fetch("/api/chat", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        message: trimmed,
        sessionId
      })
    });

    const payload = await response.json();

    if (!response.ok) {
      throw new Error(payload.message || "İstek başarısız oldu.");
    }

    sessionId = payload.sessionId;
    localStorage.setItem("aygazChatSessionId", sessionId);

    appendMessage("assistant", payload.message, `Scope: ${payload.scope}`);
  } catch (error) {
    showError(error.message || "Beklenmeyen bir hata oluştu.");
  } finally {
    setLoading(false);
  }
}

async function clearChat() {
  showError("");
  messagesEl.innerHTML = "";

  if (!sessionId) {
    return;
  }

  setLoading(true);

  try {
    const response = await fetch("/api/chat/clear", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ sessionId })
    });

    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.message || "Oturum temizlenemedi.");
    }

    sessionId = payload.sessionId;
    localStorage.setItem("aygazChatSessionId", sessionId);
  } catch (error) {
    showError(error.message || "Oturum temizlenemedi.");
  } finally {
    setLoading(false);
  }
}

formEl.addEventListener("submit", (event) => {
  event.preventDefault();
  sendMessage(inputEl.value);
});

inputEl.addEventListener("keydown", (event) => {
  if (event.key === "Enter" && !event.shiftKey) {
    event.preventDefault();
    sendMessage(inputEl.value);
  }
});

clearButtonEl.addEventListener("click", clearChat);

suggestionButtons.forEach((button) => {
  button.addEventListener("click", () => {
    const prompt = button.dataset.prompt || "";
    inputEl.value = prompt;
    sendMessage(prompt);
  });
});

appendMessage(
  "assistant",
  "Merhaba. Aygaz sentetik e-ticaret demo agent'ına hoş geldiniz. Müşteri, sipariş, stok, satış veya politika sorularınızı yazabilirsiniz."
);
