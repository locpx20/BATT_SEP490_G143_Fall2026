// BATT PPMS - Main JS
// Sidebar toggle
document.addEventListener('DOMContentLoaded', function () {
    const toggle = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('sidebar');
    const mainWrapper = document.querySelector('.main-wrapper');

    if (toggle && sidebar) {
        toggle.addEventListener('click', function () {
            sidebar.classList.toggle('collapsed');
            if (mainWrapper) {
                mainWrapper.classList.toggle('sidebar-collapsed');
            }
        });
    }
});

// Giữ vị trí và các nhóm menu đang mở riêng cho từng vai trò khi chuyển trang.
document.addEventListener('DOMContentLoaded', function () {
    const role = document.body.dataset.role;
    if (!role) return;
    const nav = document.getElementById('sidebarNav');
    if (!nav) return;
    const storageKey = `batt-sidebar-state:${role}`;
    const groups = [...nav.querySelectorAll('details')];
    const groupKey = group => group.querySelector('summary span')?.textContent.trim()
        || group.querySelector('summary')?.textContent.trim() || '';
    try {
        const saved = JSON.parse(sessionStorage.getItem(storageKey) || 'null');
        if (saved && Array.isArray(saved.open)) {
            groups.forEach(group => {
                if (saved.open.includes(groupKey(group)) || group.querySelector('a.active')) group.open = true;
            });
            if (Number.isFinite(saved.scrollTop)) nav.scrollTop = saved.scrollTop;
        }
    } catch { /* Bộ nhớ trình duyệt có thể không khả dụng. */ }
    function saveSidebar() {
        try {
            sessionStorage.setItem(storageKey, JSON.stringify({
                scrollTop: nav.scrollTop,
                open: groups.filter(group => group.open).map(groupKey)
            }));
        } catch { /* Vẫn cho phép điều hướng nếu không lưu được. */ }
    }
    nav.addEventListener('click', event => {
        if (event.target.closest('a[href]')) saveSidebar();
    });
    window.addEventListener('pagehide', saveSidebar);
});
