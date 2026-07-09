(function () {
    if (!window.signalR) {
        showRealtimeToast("Realtime connection is unavailable. Refresh the page or check the network.");
        console.error("SignalR client library was not loaded.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", function (notification) {
        window.dispatchEvent(new CustomEvent("rag:notification", { detail: notification }));
        
        let msg = typeof notification === 'string' ? notification : 
                 (notification && notification.message ? notification.message : "Realtime notification received.");
                 
        if (window.showToastNotification) {
            window.showToastNotification(msg);
        } else {
            showRealtimeToast(msg);
        }
        console.info("Realtime notification:", notification);
    });

    connection.on("DatasetChanged", function (payload) {
        window.dispatchEvent(new CustomEvent("rag:dataset-changed", { detail: payload }));
        console.info("Dataset changed:", payload);
    });

    connection.on("DatasetAccessChanged", function (payload) {
        updateWorkspaceDatasetAccess(payload);
        window.dispatchEvent(new CustomEvent("rag:dataset-access-changed", { detail: payload }));
        console.info("Dataset access changed:", payload);
    });

    connection.on("AdminChanged", function (payload) {
        window.dispatchEvent(new CustomEvent("rag:admin-changed", { detail: payload }));
        console.info("Admin data changed:", payload);
    });

    connection.on("DocumentProgress", function (payload) {
        window.dispatchEvent(new CustomEvent("rag:document-progress", { detail: payload }));
        console.info("Document progress:", payload);
    });

    connection.on("ChatSessionChanged", function (payload) {
        window.dispatchEvent(new CustomEvent("rag:chat-session-changed", { detail: payload }));
        console.info("Chat session changed:", payload);
    });

    connection.on("ChatMessageSaved", function (payload) {
        window.dispatchEvent(new CustomEvent("rag:chat-message-saved", { detail: payload }));
        console.info("Chat message saved:", payload);
    });

    async function start() {
        try {
            await connection.start();
            window.ragRealtime = {
                connection,
                joinDataset: function (datasetId) {
                    return connection.invoke("JoinDatasetGroup", datasetId);
                },
                leaveDataset: function (datasetId) {
                    return connection.invoke("LeaveDatasetGroup", datasetId);
                },
                joinChatSession: function (sessionId) {
                    return connection.invoke("JoinChatSessionGroup", sessionId);
                },
                leaveChatSession: function (sessionId) {
                    return connection.invoke("LeaveChatSessionGroup", sessionId);
                }
            };

            const workspace = document.querySelector("[data-rag-app]");
            const selectedDatasetId = workspace && workspace.dataset.selectedDatasetId;
            if (selectedDatasetId) {
                await connection.invoke("JoinDatasetGroup", selectedDatasetId);
            }

            const selectedSessionId = workspace && workspace.dataset.selectedSessionId;
            if (selectedSessionId) {
                await connection.invoke("JoinChatSessionGroup", selectedSessionId);
            }

            window.dispatchEvent(new CustomEvent("rag:realtime-connected"));
        } catch (error) {
            console.warn("SignalR connection failed. Retrying soon.", error);
            setTimeout(start, 5000);
        }
    }

    start();

    function escapeHtml(str) {
        if (!str) return "";
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function showRealtimeToast(message) {
        const toast = document.createElement("div");
        toast.className = "toast-line";
        toast.textContent = message;
        document.body.appendChild(toast);

        setTimeout(function () {
            toast.remove();
        }, 4500);
    }

    function updateWorkspaceDatasetAccess(payload) {
        const list = document.querySelector("[data-dataset-list]");
        if (!list || !payload || !payload.datasetId) {
            return;
        }

        const datasetId = String(payload.datasetId);
        const existing = Array.from(list.querySelectorAll("[data-dataset-id]"))
            .find(function (element) { return element.dataset.datasetId === datasetId; });
        const accessRemoved = payload.action === "revoked" || payload.action === "unassigned";

        if (accessRemoved) {
            if (existing) existing.remove();

            const selectedDatasetId = document.querySelector("[data-rag-app]")?.dataset.selectedDatasetId;
            if (selectedDatasetId === datasetId) {
                window.location.assign("/");
                return;
            }
            updateDatasetListState(list);
        } else if (!existing) {
            const emptyMessage = list.querySelector(".helper-text") || list.querySelector(".col-12");
            if (emptyMessage) emptyMessage.remove();
            document.querySelector("[data-dataset-empty-state]")?.remove();

            if (list.tagName === "TBODY") {
                list.prepend(createSubjectDocsTableRow(payload));
                updateDatasetListState(list);
            } else if (list.classList.contains("row")) {
                // Subject Portal Grid Cards UI
                const wrapper = document.createElement("div");
                wrapper.className = "col-md-6 col-lg-4";
                wrapper.dataset.datasetId = datasetId;

                const isApprovedHtml = !payload.isApproved ? '<span class="badge" style="font-size: 0.75rem; border-radius: 4px; background: var(--warning-soft); color: var(--warning); font-weight: 600;">Pending</span>' : "";

                wrapper.innerHTML = `
                    <a href="?datasetId=${encodeURIComponent(datasetId)}" class="card h-100 border-0 shadow-soft text-decoration-none subject-portal-card" style="border-radius: 12px; background: var(--surface); border: 1px solid var(--line) !important; display: flex; flex-direction: column; transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);">
                        <div class="card-body p-4 d-flex flex-column h-100">
                            <div class="d-flex justify-content-between align-items-start mb-3">
                                <h4 class="fw-bold m-0 subject-card-title" style="color: var(--ink); line-height: 1.3;">${escapeHtml(payload.name || "Subject")}</h4>
                                ${isApprovedHtml}
                            </div>
                            <p class="text-muted small flex-grow-1" style="line-height: 1.5; margin-bottom: 20px; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden;">
                                ${escapeHtml(payload.description || "No description provided.")}
                            </p>
                            <div class="d-flex justify-content-between align-items-center mt-auto pt-3" style="border-top: 1px solid var(--line);">
                                <span class="text-muted small d-flex align-items-center gap-1">
                                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line><polyline points="10 9 9 9 8 9"></polyline></svg>
                                    ${payload.documentCount || 0} document(s)
                                </span>
                                <span class="small fw-bold enter-workspace-text" style="color: var(--accent); transition: all 0.2s;">
                                    Enter Workspace &rarr;
                                </span>
                            </div>
                        </div>
                    </a>
                `;
                list.prepend(wrapper);
            } else {
                // Sidebar List UI (Fallback)
                const wrapper = document.createElement("div");
                wrapper.className = "dataset-entry";
                wrapper.dataset.datasetId = datasetId;

                const link = document.createElement("a");
                link.className = "dataset-item";
                link.href = "/?datasetId=" + encodeURIComponent(datasetId);

                const name = document.createElement("span");
                name.className = "dataset-name";
                name.textContent = payload.name || "Subject";

                const meta = document.createElement("span");
                meta.className = "dataset-meta";
                meta.textContent = (payload.documentCount || 0) + " document(s)";

                link.append(name, meta);
                wrapper.appendChild(link);
                list.prepend(wrapper);
            }
        } else if (list.tagName === "TBODY") {
            updateSubjectDocsTableRow(existing, payload);
        }

        const count = document.querySelector("[data-dataset-count]");
        if (count) {
            const total = list.querySelectorAll("[data-dataset-id]").length;
            count.textContent = `${total} subject(s)`;
        }
    }

    function createSubjectDocsTableRow(payload) {
        const datasetId = String(payload.datasetId);
        const row = document.createElement("tr");
        row.dataset.datasetId = datasetId;
        row.innerHTML = `
            <td>
                <div class="dataset-name" style="font-weight: 700; color: var(--ink);">${escapeHtml(payload.name || "Subject")}</div>
                ${payload.description ? `<div class="dataset-description text-muted small text-truncate" style="max-width: 250px;" title="${escapeHtml(payload.description)}">${escapeHtml(payload.description)}</div>` : ""}
                <div class="dataset-created text-muted small" style="font-size: 0.75rem;">Assigned just now</div>
            </td>
            <td>${visibilityBadge(payload.isPublic)}</td>
            <td>${statusBadge(payload.isApproved)}</td>
            <td><span class="dataset-document-count" style="font-weight: 800; color: var(--ink-soft);">${payload.documentCount || 0}</span></td>
            <td style="text-align: right;">
                <div class="d-flex justify-content-end gap-2">
                    <a href="/Documents?datasetId=${encodeURIComponent(datasetId)}" class="btn btn-sm" style="border-radius: 6px; font-weight: 700; background: var(--accent-soft); color: var(--accent); border: 1px solid rgba(37,99,235,0.2);">
                        Manage Docs
                    </a>
                    <a href="/?datasetId=${encodeURIComponent(datasetId)}" class="btn btn-sm btn-outline-secondary" style="border-radius: 6px; font-weight: 700; border-color: var(--line); color: var(--ink-soft);">
                        Test Chat
                    </a>
                </div>
            </td>
        `;
        return row;
    }

    function updateSubjectDocsTableRow(row, payload) {
        const name = row.querySelector(".dataset-name");
        if (name && payload.name) name.textContent = payload.name;

        const description = row.querySelector(".dataset-description");
        if (description && payload.description) {
            description.textContent = payload.description;
            description.title = payload.description;
        }

        const documentCount = row.querySelector(".dataset-document-count");
        if (documentCount) documentCount.textContent = String(payload.documentCount || 0);

        const visibilityCell = row.children[1];
        if (visibilityCell) visibilityCell.innerHTML = visibilityBadge(payload.isPublic);

        const statusCell = row.children[2];
        if (statusCell) statusCell.innerHTML = statusBadge(payload.isApproved);
    }

    function updateDatasetListState(list) {
        const total = list.querySelectorAll("[data-dataset-id]").length;
        const table = document.querySelector("[data-dataset-table]");
        if (table) table.style.display = total > 0 ? "" : "none";

        if (total === 0 && !document.querySelector("[data-dataset-empty-state]")) {
            const container = table?.parentElement;
            if (container) {
                const empty = document.createElement("div");
                empty.className = "text-center py-5";
                empty.dataset.datasetEmptyState = "";
                empty.innerHTML = '<p class="text-muted">No subjects assigned or created yet.</p>';
                container.insertBefore(empty, table);
            }
        }
    }

    function visibilityBadge(isPublic) {
        return isPublic
            ? '<span class="dataset-visibility badge" style="background: var(--success-soft); color: var(--success); font-weight: 700; border-radius: 6px; padding: 4px 10px;">Public</span>'
            : '<span class="dataset-visibility badge" style="background: var(--bg-elevated); color: var(--muted); font-weight: 700; border-radius: 6px; padding: 4px 10px;">Private</span>';
    }

    function statusBadge(isApproved) {
        return isApproved
            ? '<span class="dataset-status badge" style="background: var(--info-soft); color: var(--info); font-weight: 700; border-radius: 6px; padding: 4px 10px;">Approved</span>'
            : '<span class="dataset-status badge" style="background: var(--warning-soft); color: var(--warning); font-weight: 700; border-radius: 6px; padding: 4px 10px;">Pending</span>';
    }
})();
