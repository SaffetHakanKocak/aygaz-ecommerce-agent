const messagesEl = document.getElementById("messages");
const formEl = document.getElementById("chat-form");
const inputEl = document.getElementById("message-input");
const sendButtonEl = document.getElementById("send-button");
const clearButtonEl = document.getElementById("clear-button");
const loadingEl = document.getElementById("loading");
const errorEl = document.getElementById("error");

let sessionId = localStorage.getItem("aygazSkSessionId") || null;
let isSending = false;

function showError(message) {
  errorEl.textContent = message;
  errorEl.classList.remove("hidden");
}

function hideError() {
  errorEl.classList.add("hidden");
  errorEl.textContent = "";
}

function setLoading(isLoading) {
  isSending = isLoading;
  loadingEl.classList.toggle("hidden", !isLoading);
  sendButtonEl.disabled = isLoading;
  inputEl.disabled = isLoading;
  clearButtonEl.disabled = isLoading;
}

function appendMessage(role, text, meta) {
  const wrapper = document.createElement("div");
  wrapper.className = `message ${role}`;

  const body = document.createElement("div");
  body.textContent = text;
  wrapper.appendChild(body);

  if (meta) {
    const metaEl = document.createElement("div");
    metaEl.className = "message-meta";
    metaEl.textContent = meta;
    wrapper.appendChild(metaEl);
  }

  messagesEl.appendChild(wrapper);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function formatLatency(durationMs) {
  return `${(durationMs / 1000).toFixed(2)} sn`;
}

function formatMeta(payload) {
  const parts = [];
  if (payload.selectedAgent) {
    parts.push(payload.selectedAgent);
  }
  if (typeof payload.durationMs === "number") {
    parts.push(formatLatency(payload.durationMs));
  }
  return parts.join(" · ");
}

async function readJsonPayload(response) {
  const contentType = response.headers.get("content-type") || "";
  if (contentType.includes("application/json")) {
    return response.json();
  }

  const text = await response.text();
  return {
    success: false,
    message: text || "İstek işlenirken bir hata oluştu."
  };
}

function resolveUserMessage(payload, responseOk) {
  if (responseOk) {
    return payload.message;
  }

  if (payload?.errorCode === "AI_PROVIDER_TEMPORARILY_UNAVAILABLE") {
    return payload.message
      || "Yapay zekâ servisi şu anda yoğun. Lütfen birkaç saniye sonra tekrar deneyin.";
  }

  return payload?.message || "İstek işlenirken bir hata oluştu.";
}

async function sendMessage(message) {
  if (isSending) {
    return;
  }

  hideError();
  appendMessage("user", message);
  setLoading(true);

  try {
    const response = await fetch("/api/chat", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message, sessionId })
    });

    const payload = await readJsonPayload(response);
    if (!response.ok) {
      throw new Error(resolveUserMessage(payload, false));
    }

    sessionId = payload.sessionId;
    localStorage.setItem("aygazSkSessionId", sessionId);
    appendMessage("assistant", payload.message, formatMeta(payload));
  } catch (error) {
    showError(error.message || "İstek işlenirken bir hata oluştu.");
  } finally {
    setLoading(false);
    inputEl.focus();
  }
}

formEl.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (isSending) {
    return;
  }

  const message = inputEl.value.trim();
  if (!message) {
    return;
  }

  inputEl.value = "";
  await sendMessage(message);
});

inputEl.addEventListener("keydown", (event) => {
  if (event.key === "Enter" && !event.shiftKey) {
    event.preventDefault();
    if (isSending) {
      return;
    }

    formEl.requestSubmit();
  }
});

clearButtonEl.addEventListener("click", async () => {
  if (isSending) {
    return;
  }

  hideError();
  messagesEl.innerHTML = "";

  if (!sessionId) {
    sessionId = crypto.randomUUID().replace(/-/g, "");
    localStorage.setItem("aygazSkSessionId", sessionId);
    return;
  }

  try {
    const response = await fetch("/api/chat/clear", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sessionId })
    });

    const payload = await readJsonPayload(response);
    if (!response.ok) {
      throw new Error(payload.message || "Oturum temizlenemedi.");
    }

    sessionId = payload.sessionId;
    localStorage.setItem("aygazSkSessionId", sessionId);
  } catch (error) {
    showError(error.message || "Oturum temizlenemedi.");
  }
});

appendMessage(
  "assistant",
  "Merhaba. Aygaz müşteri bilgileri için sorularınızı yazabilirsiniz."
);
