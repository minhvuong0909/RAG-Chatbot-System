(function () {
    if (!window.signalR) {
        showRealtimeToast("Không thể kết nối cập nhật thời gian thực. Vui lòng tải lại trang hoặc kiểm tra mạng.");
        console.error("Thư viện SignalR chưa được tải.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", function (notification) {
        window.dispatchEvent(new CustomEvent("rag:notification", { detail: notification }));
        
        let msg = typeof notification === 'string' ? notification : 
                 (notification && notification.message ? notification.message : "Hệ thống vừa có cập nhật mới.");
                 
        if (window.showToastNotification) {
            window.showToastNotification(msg);
        } else {
            showRealtimeToast(msg);
        }
        console.info("Realtime notification:", notification);
    });

    connection.on("DatasetChanged", function (payload) {
        updateWorkspaceDatasetAccess(payload);
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
        showDocumentProgressToast(payload);
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

    connection.on("CreditBalanceChanged", function (payload) {
        const balance = payload && (payload.balance || payload.Balance);
        if (balance) {
            updateCreditElements(balance);
        }
        window.dispatchEvent(new CustomEvent("rag:credit-balance-changed", { detail: payload }));
        console.info("Credit balance changed:", payload);
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
            console.warn("Kết nối SignalR thất bại, hệ thống sẽ tự thử lại.", error);
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

    function showDocumentProgressToast(payload) {
        if (!payload || !payload.action || !payload.fileName) {
            return;
        }

        const action = String(payload.action).toLowerCase();
        let message = "";
        if (action === "uploaded") {
            message = `Tài liệu "${payload.fileName}" vừa được tải lên và đang xử lý.`;
        } else if (action === "completed") {
            message = `Tài liệu "${payload.fileName}" đã xử lý xong và sẵn sàng để tra cứu.`;
        } else if (action === "failed") {
            message = `Không thể xử lý tài liệu "${payload.fileName}".`;
        } else if (action === "deleted") {
            message = `Tài liệu "${payload.fileName}" đã được xóa khỏi môn học.`;
        }

        if (!message) {
            return;
        }

        if (window.showToastNotification) {
            window.showToastNotification(message);
        } else {
            showRealtimeToast(message);
        }
    }

    function updateWorkspaceDatasetAccess(payload) {
        const list = document.querySelector("[data-dataset-list]");
        if (!list || !payload || !payload.datasetId) {
            return;
        }

        const datasetId = String(payload.datasetId);
        const existing = Array.from(list.querySelectorAll("[data-dataset-id]"))
            .find(function (element) { return element.dataset.datasetId === datasetId; });
        const accessRemoved = payload.action === "revoked" || payload.action === "unassigned" || payload.action === "archived" || payload.action === "deleted";

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

                const isApprovedHtml = !payload.isApproved ? '<span class="badge" style="font-size: 0.75rem; border-radius: 4px; background: var(--warning-soft); color: var(--warning); font-weight: 600;">Chờ duyệt</span>' : "";

                wrapper.innerHTML = `
                    <a href="?datasetId=${encodeURIComponent(datasetId)}" class="card h-100 border-0 shadow-soft text-decoration-none subject-portal-card" style="border-radius: 12px; background: var(--surface); border: 1px solid var(--line) !important; display: flex; flex-direction: column; transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);">
                        <div class="card-body p-4 d-flex flex-column h-100">
                            <div class="d-flex justify-content-between align-items-start mb-3">
                                <h4 class="fw-bold m-0 subject-card-title" style="color: var(--ink); line-height: 1.3;">${escapeHtml(payload.name || "Subject")}</h4>
                                ${isApprovedHtml}
                            </div>
                            <p class="dataset-description text-muted small flex-grow-1" style="line-height: 1.5; margin-bottom: 20px; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden;">
                                ${escapeHtml(payload.description || "Chưa có mô tả.")}
                            </p>
                            <div class="d-flex justify-content-between align-items-center mt-auto pt-3" style="border-top: 1px solid var(--line);">
                                <span class="dataset-document-count text-muted small d-flex align-items-center gap-1">
                                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line><polyline points="10 9 9 9 8 9"></polyline></svg>
                                    ${payload.documentCount || 0} tài liệu
                                </span>
                                <span class="small fw-bold enter-workspace-text" style="color: var(--accent); transition: all 0.2s;">
                                    Vào không gian học tập &rarr;
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
                meta.textContent = (payload.documentCount || 0) + " tài liệu";

                link.append(name, meta);
                wrapper.appendChild(link);
                list.prepend(wrapper);
            }
        } else if (existing) {
            if (list.tagName === "TBODY") {
                updateSubjectDocsTableRow(existing, payload);
            } else {
                updateWorkspaceDatasetCard(existing, payload);
            }
        }

        const count = document.querySelector("[data-dataset-count]");
        if (count) {
            const total = list.querySelectorAll("[data-dataset-id]").length;
            count.textContent = `${total} môn học`;
        }
    }

    function createSubjectDocsTableRow(payload) {
        const datasetId = String(payload.datasetId);
        const row = document.createElement("tr");
        row.dataset.datasetId = datasetId;
        row.innerHTML = `
            <td class="py-3">
                <div class="dataset-name fw-bold" style="color: var(--ink);">${escapeHtml(payload.name || "Môn học")}</div>
                ${payload.description ? `<div class="dataset-description text-muted small text-truncate" style="max-width: 250px;" title="${escapeHtml(payload.description)}">${escapeHtml(payload.description)}</div>` : ""}
                <div class="dataset-created text-muted small" style="font-size: 0.75rem;">Vừa được phân công</div>
            </td>
            <td class="py-3">${visibilityBadge(payload.isPublic)}</td>
            <td class="py-3"><span class="dataset-document-count fw-semibold" style="color: var(--ink-soft);">${payload.documentCount || 0}</span></td>
            <td class="py-3 text-end">
                <div class="d-flex justify-content-end gap-2">
                    <a href="/?datasetId=${encodeURIComponent(datasetId)}" class="btn btn-sm btn-outline-primary" style="border-radius: 6px; font-weight: 600; border-color: var(--accent); color: var(--accent);">
                        Trò chuyện
                    </a>
                    <a href="/Documents?datasetId=${encodeURIComponent(datasetId)}" class="btn btn-sm btn-outline-secondary" style="border-radius: 6px; font-weight: 600; border-color: var(--line); color: var(--ink-soft);">
                        Tài liệu
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

        const documentCell = row.children[2];
        if (documentCell) {
            const count = documentCell.querySelector(".dataset-document-count");
            if (count) count.textContent = String(payload.documentCount || 0);
        }
    }

    function updateCreditElements(balance) {
        const total = balance.totalCredits ?? balance.TotalCredits ?? 0;
        const free = balance.freeCredits ?? balance.FreeCredits ?? 0;
        const paid = balance.paidCredits ?? balance.PaidCredits ?? 0;
        const values = {
            creditTotalText: total,
            creditTotalPortal: total,
            creditFreeText: free,
            creditFreePortal: free,
            creditPaidText: paid,
            creditPaidPortal: paid
        };
        Object.entries(values).forEach(function ([id, value]) {
            const element = document.getElementById(id);
            if (element) element.textContent = Number(value).toLocaleString("vi-VN");
        });
    }

    function updateWorkspaceDatasetCard(wrapper, payload) {
        const name = wrapper.querySelector(".subject-card-title, .dataset-name");
        const description = wrapper.querySelector(".dataset-description");
        const documentCount = wrapper.querySelector(".dataset-document-count");

        if (name && payload.name) name.textContent = payload.name;
        if (description) description.textContent = payload.description || "Chưa có mô tả.";
        if (documentCount) {
            const icon = documentCount.querySelector("svg");
            documentCount.textContent = `${payload.documentCount || 0} tài liệu`;
            if (icon) documentCount.prepend(icon);
        }
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
                empty.innerHTML = '<p class="text-muted">Chưa có môn học nào được phân công hoặc tạo mới.</p>';
                container.insertBefore(empty, table);
            }
        }
    }

    function visibilityBadge(isPublic) {
        return isPublic
            ? '<span class="dataset-visibility badge" style="background: var(--success-soft); color: var(--success); font-weight: 600; border-radius: 4px; padding: 4px 8px;">Công khai</span>'
            : '<span class="dataset-visibility badge" style="background: var(--bg-elevated); color: var(--muted); font-weight: 600; border-radius: 4px; padding: 4px 8px;">Riêng tư</span>';
    }

    function statusBadge(isApproved) {
        return isApproved
            ? '<span class="dataset-status badge" style="background: var(--info-soft); color: var(--info); font-weight: 600; border-radius: 4px; padding: 4px 8px;">Đã duyệt</span>'
            : '<span class="dataset-status badge" style="background: var(--warning-soft); color: var(--warning); font-weight: 600; border-radius: 4px; padding: 4px 8px;">Chờ duyệt</span>';
    }
})();
