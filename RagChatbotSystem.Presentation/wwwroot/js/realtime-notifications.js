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
        } else if (!existing) {
            const emptyMessage = list.querySelector(".helper-text");
            if (emptyMessage) emptyMessage.remove();

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

        const count = document.querySelector("[data-dataset-count]");
        if (count) {
            count.textContent = String(list.querySelectorAll("[data-dataset-id]").length);
        }
    }
})();
