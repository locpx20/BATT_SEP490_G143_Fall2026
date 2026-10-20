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
