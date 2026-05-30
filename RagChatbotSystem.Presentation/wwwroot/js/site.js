(() => {
  const root = document.querySelector("[data-rag-app]");
  if (!root) return;

  const state = {
    user: JSON.parse(localStorage.getItem("ragUser") || "null"),
    datasets: [],
    documents: [],
    sessions: [],
    selectedDatasetId: localStorage.getItem("ragDatasetId"),
    session: JSON.parse(localStorage.getItem("ragSession") || "null"),
    busy: false
  };

  const els = {
    userForm: root.querySelector("[data-user-form]"),
    userStatus: root.querySelector("[data-user-status]"),
    datasetForm: root.querySelector("[data-dataset-form]"),
    datasetList: root.querySelector("[data-dataset-list]"),
    datasetCount: root.querySelector("[data-dataset-count]"),
    uploadForm: root.querySelector("[data-upload-form]"),
    documentList: root.querySelector("[data-document-list]"),
    documentCount: root.querySelector("[data-document-count]"),
    sessionList: root.querySelector("[data-session-list]"),
    sessionCount: root.querySelector("[data-session-count]"),
    chatTitle: root.querySelector("[data-chat-title]"),
    chatStream: root.querySelector("[data-chat-stream]"),
    chatForm: root.querySelector("[data-chat-form]"),
    newSession: root.querySelector("[data-new-session]"),
    refresh: root.querySelector("[data-refresh]"),
    referenceEmpty: root.querySelector("[data-reference-empty]"),
    referenceContent: root.querySelector("[data-reference-content]")
  };

  const api = async (url, options = {}) => {
    const response = await fetch(url, {
      headers: options.body instanceof FormData ? {} : { "Content-Type": "application/json" },
      ...options
    });

    if (!response.ok) {
      let message = `Request failed with ${response.status}`;
      try {
        const error = await response.json();
        message = error.message || error.detail || message;
      } catch {
        message = await response.text() || message;
      }
      throw new Error(message);
    }

    if (response.status === 204) return null;
    return response.json();
  };

  const saveUser = (user) => {
    state.user = user;
    localStorage.setItem("ragUser", JSON.stringify(user));
    els.userStatus.textContent = `Using ${user.fullName} (${user.email})`;
  };

  const saveSession = (session) => {
    state.session = session;
    localStorage.setItem("ragSession", JSON.stringify(session));
    els.chatTitle.textContent = session.title;
  };

  const notify = (message) => {
    const toast = document.createElement("div");
    toast.className = "toast-line";
    toast.textContent = message;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 3400);
  };

  const escapeHtml = (value) => String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");

  const renderMarkdown = (input) => {
    const lines = escapeHtml(input || "").split(/\r?\n/);
    const html = [];
    let inList = false;

    for (const rawLine of lines) {
      const line = rawLine.trimEnd();
      if (!line.trim()) {
        if (inList) {
          html.push("</ul>");
          inList = false;
        }
        html.push("<br>");
        continue;
      }

      if (line.startsWith("### ")) {
        if (inList) {
          html.push("</ul>");
          inList = false;
        }
        html.push(`<h4>${line.slice(4)}</h4>`);
        continue;
      }

      if (line.startsWith("## ")) {
        if (inList) {
          html.push("</ul>");
          inList = false;
        }
        html.push(`<h3>${line.slice(3)}</h3>`);
        continue;
      }

      if (line.startsWith("# ")) {
        if (inList) {
          html.push("</ul>");
          inList = false;
        }
        html.push(`<h2>${line.slice(2)}</h2>`);
        continue;
      }

      const bullet = line.match(/^[-*]\s+(.+)/);
      if (bullet) {
        if (!inList) {
          html.push("<ul>");
          inList = true;
        }
        html.push(`<li>${inlineMarkdown(bullet[1])}</li>`);
        continue;
      }

      if (inList) {
        html.push("</ul>");
        inList = false;
      }
      html.push(`<p>${inlineMarkdown(line)}</p>`);
    }

    if (inList) html.push("</ul>");
    return html.join("");
  };

  const inlineMarkdown = (value) => value
    .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
    .replace(/`(.+?)`/g, "<code>$1</code>")
    .replace(/\[([^\]]+)\]\((https?:\/\/[^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');

  const formatDate = (value) => new Intl.DateTimeFormat("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit"
  }).format(new Date(value));

  const selectedDataset = () => state.datasets.find((item) => item.datasetId === state.selectedDatasetId);
  const selectedSession = () => state.sessions.find((item) => item.sessionId === state.session?.sessionId);

  const clearChat = () => {
    els.chatStream.innerHTML = `
      <div class="empty-state">
        <h3>Ask questions grounded in your uploaded documents.</h3>
        <p>Create or select a dataset, upload a document, then start a chat session.</p>
      </div>`;
  };

  const renderDatasets = () => {
    els.datasetList.innerHTML = "";
    els.datasetCount.textContent = state.datasets.length;

    if (!state.datasets.length) {
      els.datasetList.innerHTML = `<p class="helper-text">No datasets yet.</p>`;
      return;
    }

    const template = document.getElementById("dataset-item-template");
    for (const dataset of state.datasets) {
      const node = template.content.firstElementChild.cloneNode(true);
      node.classList.toggle("active", dataset.datasetId === state.selectedDatasetId);
      node.querySelector(".dataset-name").textContent = dataset.name;
      node.querySelector(".dataset-meta").textContent = `${dataset.documentCount} document(s)`;
      node.addEventListener("click", () => selectDataset(dataset.datasetId));
      els.datasetList.appendChild(node);
    }
  };

  const renderDocuments = () => {
    els.documentList.innerHTML = "";
    els.documentCount.textContent = state.documents.length;

    if (!state.selectedDatasetId) {
      els.documentList.innerHTML = `<p class="helper-text">Select a dataset to view documents.</p>`;
      return;
    }

    if (!state.documents.length) {
      els.documentList.innerHTML = `<p class="helper-text">No documents in this dataset.</p>`;
      return;
    }

    const template = document.getElementById("document-item-template");
    for (const documentItem of state.documents) {
      const node = template.content.firstElementChild.cloneNode(true);
      const status = documentItem.status || "Uploaded";
      node.querySelector(".document-name").textContent = documentItem.fileName;
      node.querySelector(".document-meta").textContent = `${documentItem.fileType || "file"} - ${Math.ceil(documentItem.fileSize / 1024)} KB`;
      node.querySelector(".status-pill").textContent = status;
      node.querySelector(".status-pill").classList.toggle("processing", status.toLowerCase() === "processing");
      node.querySelector(".status-pill").classList.toggle("failed", status.toLowerCase() === "failed");
      node.querySelector(".progress-fill").style.width = status.toLowerCase() === "completed" || status.toLowerCase() === "uploaded" ? "100%" : "55%";
      els.documentList.appendChild(node);
    }
  };

  const renderSessions = () => {
    els.sessionList.innerHTML = "";
    els.sessionCount.textContent = state.sessions.length;

    if (!state.selectedDatasetId) {
      els.sessionList.innerHTML = `<p class="helper-text">Select a dataset to view chat sessions.</p>`;
      return;
    }

    if (!state.sessions.length) {
      els.sessionList.innerHTML = `<p class="helper-text">No sessions yet. Press New session or send your first message.</p>`;
      return;
    }

    const template = document.getElementById("session-item-template");
    for (const session of state.sessions) {
      const node = template.content.firstElementChild.cloneNode(true);
      node.classList.toggle("active", session.sessionId === state.session?.sessionId);
      node.querySelector(".session-name").textContent = session.title || "Dataset chat";
      node.querySelector(".session-meta").textContent = `Updated ${formatDate(session.updatedAt)}`;
      node.addEventListener("click", () => selectSession(session.sessionId));
      els.sessionList.appendChild(node);
    }
  };

  const renderChatMessage = (message, options = {}) => {
    const wasEmpty = els.chatStream.querySelector(".empty-state");
    if (wasEmpty) els.chatStream.innerHTML = "";

    const wrapper = document.createElement("article");
    const role = (message.role || "").toLowerCase();
    wrapper.className = `message ${role === "user" ? "user" : "assistant"}`;
    wrapper.dataset.messageId = message.messageId;

    const bubble = document.createElement("div");
    bubble.className = "message-bubble";
    wrapper.appendChild(bubble);

    const meta = document.createElement("div");
    meta.className = "message-meta";
    meta.textContent = `${role === "user" ? "You" : "Assistant"} - ${formatDate(message.createdAt)}`;
    wrapper.appendChild(meta);

    if (options.typeText && role !== "user") {
      typeInto(bubble, message.content, () => loadCitations(message.messageId, wrapper));
    } else {
      bubble.innerHTML = renderMarkdown(message.content);
      if (role !== "user") loadCitations(message.messageId, wrapper);
    }

    els.chatStream.appendChild(wrapper);
    els.chatStream.scrollTop = els.chatStream.scrollHeight;
  };

  const typeInto = (element, text, done) => {
    let index = 0;
    const timer = setInterval(() => {
      index += 3;
      element.innerHTML = renderMarkdown(text.slice(0, index));
      els.chatStream.scrollTop = els.chatStream.scrollHeight;
      if (index >= text.length) {
        clearInterval(timer);
        element.innerHTML = renderMarkdown(text);
        done?.();
      }
    }, 14);
  };

  const loadCitations = async (messageId, messageElement) => {
    try {
      const citations = await api(`/api/chat-sessions/messages/${messageId}/citations`);
      if (!citations.length) return;

      const row = document.createElement("div");
      row.className = "citation-row";
      citations.forEach((citation, index) => {
        const chip = document.createElement("button");
        chip.type = "button";
        chip.className = "citation-chip";
        chip.textContent = `[${index + 1}] ${citation.fileName || citation.sourceLabel || "Source"}`;
        chip.addEventListener("click", () => showReference(citation));
        row.appendChild(chip);
      });
      messageElement.appendChild(row);
    } catch {
      // Citations are optional for a response.
    }
  };

  const showReference = (citation) => {
    els.referenceEmpty.hidden = true;
    els.referenceContent.hidden = false;
    els.referenceContent.innerHTML = `
      <div class="reference-card">
        <h3>${escapeHtml(citation.fileName || "Document source")}</h3>
        <p><strong>Page:</strong> ${citation.pageNumber || 1}</p>
        <p><strong>Source label:</strong> ${escapeHtml(citation.sourceLabel || "Referenced chunk")}</p>
        <p>${escapeHtml(citation.quoteText || "No quote text available.")}</p>
      </div>`;
  };

  const loadDatasets = async () => {
    state.datasets = await api("/api/datasets");
    if (state.selectedDatasetId && !state.datasets.some((item) => item.datasetId === state.selectedDatasetId)) {
      state.selectedDatasetId = null;
      localStorage.removeItem("ragDatasetId");
    }
    renderDatasets();
  };

  const loadDocuments = async () => {
    if (!state.selectedDatasetId) {
      state.documents = [];
      renderDocuments();
      return;
    }
    state.documents = await api(`/api/datasets/${state.selectedDatasetId}/documents`);
    renderDocuments();
  };

  const loadMessages = async () => {
    if (!state.session?.sessionId) {
      clearChat();
      return;
    }

    const messages = await api(`/api/chat-sessions/${state.session.sessionId}/messages`);
    els.chatStream.innerHTML = "";
    if (!messages.length) {
      clearChat();
      return;
    }
    messages.forEach((message) => renderChatMessage(message));
  };

  const loadSessions = async () => {
    if (!state.user || !state.selectedDatasetId) {
      state.sessions = [];
      renderSessions();
      return;
    }

    state.sessions = await api(`/api/chat-sessions?userId=${state.user.userId}&datasetId=${state.selectedDatasetId}`);

    if (state.session && state.session.datasetId === state.selectedDatasetId) {
      const freshSession = state.sessions.find((item) => item.sessionId === state.session.sessionId);
      if (freshSession) {
        saveSession(freshSession);
      } else {
        state.session = null;
        localStorage.removeItem("ragSession");
      }
    }

    renderSessions();
  };

  const selectDataset = async (datasetId) => {
    state.selectedDatasetId = datasetId;
    localStorage.setItem("ragDatasetId", datasetId);
    const dataset = selectedDataset();
    els.chatTitle.textContent = dataset ? dataset.name : "Dataset chat";

    if (!state.session || state.session.datasetId !== datasetId) {
      state.session = null;
      localStorage.removeItem("ragSession");
      clearChat();
    }

    renderDatasets();
    await loadDocuments();
    await loadSessions();
  };

  const selectSession = async (sessionId) => {
    const session = state.sessions.find((item) => item.sessionId === sessionId);
    if (!session) return;

    saveSession(session);
    renderSessions();
    await loadMessages();
  };

  const createSession = async () => {
    if (!state.user) throw new Error("Create or select a user first.");
    if (!state.selectedDatasetId) throw new Error("Select a dataset first.");

    const dataset = selectedDataset();
    const session = await api("/api/chat-sessions", {
      method: "POST",
      body: JSON.stringify({
        userId: state.user.userId,
        datasetId: state.selectedDatasetId,
        title: dataset ? dataset.name : "Dataset chat"
      })
    });
    saveSession(session);
    await loadSessions();
    renderSessions();
    clearChat();
    notify("New chat session created.");
  };

  els.userForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    const data = new FormData(els.userForm);
    const fullName = data.get("fullName").toString().trim();
    const email = data.get("email").toString().trim();

    try {
      const users = await api("/api/users");
      const existing = users.find((user) => user.email.toLowerCase() === email.toLowerCase());
      const user = existing || await api("/api/users", {
        method: "POST",
        body: JSON.stringify({ fullName, email, role: "User" })
      });
      saveUser(user);
      await loadDatasets();
      await loadSessions();
      notify("User is ready.");
    } catch (error) {
      notify(error.message);
    }
  });

  els.datasetForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!state.user) {
      notify("Create or select a user first.");
      return;
    }

    const data = new FormData(els.datasetForm);
    try {
      const dataset = await api("/api/datasets", {
        method: "POST",
        body: JSON.stringify({
          name: data.get("name").toString(),
          description: data.get("description").toString(),
          createdBy: state.user.userId
        })
      });
      els.datasetForm.reset();
      await loadDatasets();
      await selectDataset(dataset.datasetId);
      notify("Dataset created.");
    } catch (error) {
      notify(error.message);
    }
  });

  els.uploadForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!state.user || !state.selectedDatasetId) {
      notify("Select a user and dataset before uploading.");
      return;
    }

    const data = new FormData(els.uploadForm);
    data.append("uploadedBy", state.user.userId);

    try {
      await api(`/api/datasets/${state.selectedDatasetId}/documents`, {
        method: "POST",
        body: data
      });
      els.uploadForm.reset();
      await loadDocuments();
      await loadDatasets();
      await loadSessions();
      renderDatasets();
      notify("Document uploaded.");
    } catch (error) {
      notify(error.message);
    }
  });

  els.newSession.addEventListener("click", async () => {
    try {
      await createSession();
    } catch (error) {
      notify(error.message);
    }
  });

  els.chatForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (state.busy) return;

    const textarea = els.chatForm.elements.message;
    const content = textarea.value.trim();
    if (!content) return;

    try {
      state.busy = true;
      if (!state.session?.sessionId) await createSession();
      const userMessage = {
        messageId: crypto.randomUUID(),
        sessionId: state.session.sessionId,
        role: "User",
        content,
        createdAt: new Date().toISOString()
      };
      renderChatMessage(userMessage);
      textarea.value = "";

      const response = await api(`/api/chat-sessions/${state.session.sessionId}/messages`, {
        method: "POST",
        body: JSON.stringify({ content })
      });
      const assistantMessage = response.assistantMessage || response;
      renderChatMessage(assistantMessage, { typeText: true });
      await loadSessions();
    } catch (error) {
      notify(error.message);
    } finally {
      state.busy = false;
    }
  });

  els.refresh.addEventListener("click", async () => {
    try {
      await loadDatasets();
      await loadDocuments();
      await loadSessions();
      await loadMessages();
      notify("Workspace refreshed.");
    } catch (error) {
      notify(error.message);
    }
  });

  const boot = async () => {
    if (state.user) {
      els.userForm.elements.fullName.value = state.user.fullName;
      els.userForm.elements.email.value = state.user.email;
      els.userStatus.textContent = `Using ${state.user.fullName} (${state.user.email})`;
    }

    try {
      await loadDatasets();
      if (state.selectedDatasetId) {
        await selectDataset(state.selectedDatasetId);
      } else {
        renderDocuments();
        renderSessions();
      }
      await loadMessages();
    } catch (error) {
      notify(error.message);
    }
  };

  boot();
})();
