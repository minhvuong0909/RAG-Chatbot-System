// site.js - Client side scripting for RagChatbotSystem
// All business features have been migrated to server-side MVC Razor templates.
console.log("RagChatbotSystem MVC Workspace loaded successfully.");

// Toast Notification System
window.showToastNotification = function (message) {
    let container = document.getElementById("toast-container");
    if (!container) {
        container = document.createElement("div");
        container.id = "toast-container";
        container.style.position = "fixed";
        container.style.top = "20px";
        container.style.right = "20px";
        container.style.zIndex = "9999";
        container.style.display = "flex";
        container.style.flexDirection = "column";
        container.style.gap = "10px";
        document.body.appendChild(container);
    }

    const toast = document.createElement("div");
    toast.style.background = "var(--ink)";
    toast.style.color = "var(--surface)";
    toast.style.padding = "14px 20px";
    toast.style.borderRadius = "var(--radius)";
    toast.style.boxShadow = "var(--shadow)";
    toast.style.opacity = "0";
    toast.style.transform = "translateY(-20px)";
    toast.style.transition = "all 0.3s cubic-bezier(0.16, 1, 0.3, 1)";
    toast.style.fontWeight = "700";
    toast.style.fontSize = "0.95rem";
    toast.innerText = message;

    container.appendChild(toast);

    // Trigger animation
    setTimeout(() => {
        toast.style.opacity = "1";
        toast.style.transform = "translateY(0)";
    }, 10);

    // Auto remove
    setTimeout(() => {
        toast.style.opacity = "0";
        toast.style.transform = "translateY(-20px)";
        setTimeout(() => toast.remove(), 300);
    }, 3500);
};