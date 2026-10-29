document.addEventListener('DOMContentLoaded', () => {
    const triggers = [...document.querySelectorAll('[data-topbar-panel]')];
    const content = document.getElementById('notificationsContent');
    let loading = false;

    function closePanels() {
        triggers.forEach(trigger => {
            trigger.setAttribute('aria-expanded', 'false');
            document.getElementById(trigger.dataset.topbarPanel).hidden = true;
        });
    }

    function showState(message, retry = false) {
        const state = document.createElement('div');
        state.className = 'notification-state';
        state.textContent = message;
        if (retry) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'notification-retry';
            button.textContent = 'Thử lại';
            button.addEventListener('click', loadNotifications);
            state.append(button);
        }
        content.replaceChildren(state);
    }

    async function loadNotifications() {
        if (!content || loading) return;
        loading = true;
        content.setAttribute('aria-busy', 'true');
        showState('Đang tải thông báo…');
        try {
            const response = await fetch(content.dataset.url, { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
            if (response.status === 401) {
                showState('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                return;
            }
            if (!response.ok) throw new Error('Unable to load notifications');
            const data = await response.json();
            if (!Array.isArray(data.items)) throw new Error('Invalid notifications');
            if (!data.items.length) {
                showState('Bạn chưa có thông báo cần chú ý.');
                return;
            }
            const items = data.items.map(item => {
                const link = document.createElement('a');
                link.className = 'notification-item';
                link.href = item.url;
                const title = document.createElement('strong');
                title.textContent = item.title;
                const message = document.createElement('p');
                message.textContent = item.message;
                link.append(title, message);
                return link;
            });
            const footer = document.createElement('a');
            footer.className = 'notification-footer';
            footer.href = '/Admin/Users/Index';
            footer.textContent = 'Quản lý tài khoản';
            content.replaceChildren(...items, footer);
        } catch {
            showState('Chưa tải được thông báo. Vui lòng thử lại.', true);
        } finally {
            loading = false;
            content.setAttribute('aria-busy', 'false');
        }
    }

    triggers.forEach(trigger => {
        trigger.addEventListener('click', () => {
            const wasOpen = trigger.getAttribute('aria-expanded') === 'true';
            closePanels();
            if (wasOpen) return;
            const panel = document.getElementById(trigger.dataset.topbarPanel);
            trigger.setAttribute('aria-expanded', 'true');
            panel.hidden = false;
            if (panel.id === 'notificationsPanel') loadNotifications();
        });
    });
    document.addEventListener('click', event => {
        if (!event.target.closest('.topbar-dropdown')) closePanels();
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const active = triggers.find(trigger => trigger.getAttribute('aria-expanded') === 'true');
        closePanels();
        if (active) active.focus();
    });
    document.addEventListener('focusin', event => {
        if (!event.target.closest('.topbar-dropdown')) closePanels();
    });
});
