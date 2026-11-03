(() => {
    let active = null;
    document.querySelectorAll('select').forEach((select, index) => {
        if (select.multiple || select.size > 1 || select.closest('.batt-select')) return;
        if (!select.id) select.id = `batt-select-${index}`;
        const originalStyle = getComputedStyle(select);
        const wrapper = document.createElement('div');
        wrapper.className = 'batt-select';
        wrapper.style.setProperty('--select-height', `${select.offsetHeight || parseFloat(originalStyle.minHeight) || 54}px`);
        wrapper.style.setProperty('--select-font-size', originalStyle.fontSize);
        select.before(wrapper);
        wrapper.append(select);

        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.id = `${select.id}-trigger`;
        trigger.className = 'batt-select-trigger';
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.setAttribute('aria-expanded', 'false');
        const caption = document.createElement('span');
        caption.className = 'batt-select-label';
        trigger.append(caption);
        const menu = document.createElement('div');
        menu.id = `${select.id}-menu`;
        menu.className = 'batt-select-menu';
        menu.hidden = true;
        menu.style.setProperty('--select-font-size', originalStyle.fontSize);
        menu.setAttribute('role', 'listbox');
        trigger.setAttribute('aria-controls', menu.id);
        const label = select.labels?.[0] || select.closest('.form-group')?.querySelector('label');
        if (label) {
            if (!label.id) label.id = `${select.id}-label`;
            label.htmlFor = trigger.id;
            trigger.setAttribute('aria-labelledby', `${label.id} ${trigger.id}-value`);
            caption.id = `${trigger.id}-value`;
            menu.setAttribute('aria-labelledby', label.id);
        } else {
            trigger.setAttribute('aria-label', select.getAttribute('aria-label') || select.name);
            menu.setAttribute('aria-label', select.getAttribute('aria-label') || select.name);
        }
        const options = Array.from(select.options, option => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'batt-select-option';
            button.setAttribute('role', 'option');
            button.tabIndex = -1;
            button.disabled = option.disabled;
            const text = document.createElement('span');
            text.className = 'batt-select-option-label';
            text.textContent = option.textContent;
            button.append(text);
            button.addEventListener('click', () => {
                select.value = option.value;
                refresh();
                close(true);
                select.dispatchEvent(new Event('change', { bubbles: true }));
            });
            menu.append(button);
            return button;
        });
        function refresh() {
            caption.textContent = select.selectedOptions[0]?.textContent || 'Chọn một mục';
            trigger.title = caption.textContent;
            trigger.disabled = select.disabled;
            trigger.removeAttribute('aria-invalid');
            options.forEach((option, i) => option.setAttribute('aria-selected', String(i === select.selectedIndex)));
        }
        function position() {
            if (menu.hidden) return;
            const rect = trigger.getBoundingClientRect();
            const below = innerHeight - rect.bottom - 16;
            const above = rect.top - 16;
            const placeAbove = below < 220 && above > below;
            menu.style.maxHeight = `${Math.max(44, Math.min(320, placeAbove ? above : below))}px`;
            const width = Math.min(rect.width, innerWidth - 24);
            menu.style.width = `${width}px`;
            menu.style.left = `${Math.max(12, Math.min(rect.left, innerWidth - width - 12))}px`;
            menu.style.top = `${placeAbove ? rect.top - menu.offsetHeight - 8 : rect.bottom + 8}px`;
        }
        function close(restoreFocus = false) {
            menu.hidden = true;
            trigger.setAttribute('aria-expanded', 'false');
            if (active?.menu === menu) active = null;
            if (restoreFocus) trigger.focus();
        }
        function open() {
            if (select.disabled) return;
            active?.close();
            refresh();
            menu.hidden = false;
            trigger.setAttribute('aria-expanded', 'true');
            active = { menu, wrapper, close, position };
            position();
            (options[select.selectedIndex] || options.find(option => !option.disabled))?.focus({ preventScroll: true });
        }
        trigger.addEventListener('click', () => menu.hidden ? open() : close());
        trigger.addEventListener('keydown', event => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); open(); }
        });
        menu.addEventListener('keydown', event => {
            if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(true); }
            if (event.key === 'Tab') close(true);
            if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
                event.preventDefault();
                const available = options.filter(option => !option.disabled);
                const index = available.indexOf(document.activeElement);
                const next = event.key === 'Home' ? 0 : event.key === 'End' ? available.length - 1
                    : (index + (event.key === 'ArrowDown' ? 1 : -1) + available.length) % available.length;
                available[next]?.focus({ preventScroll: true });
            }
        });
        select.addEventListener('change', refresh);
        select.addEventListener('invalid', event => { event.preventDefault(); trigger.setAttribute('aria-invalid', 'true'); trigger.focus(); });
        select.form?.addEventListener('reset', () => setTimeout(refresh, 0));
        wrapper.append(trigger);
        (select.closest('.modal') || document.body).append(menu);
        select.hidden = true;
        refresh();
    });
    document.addEventListener('click', event => {
        if (active && !active.wrapper.contains(event.target) && !active.menu.contains(event.target)) active.close();
    });
    document.addEventListener('focusin', event => {
        if (active && !active.wrapper.contains(event.target) && !active.menu.contains(event.target)) active.close();
    });
    document.addEventListener('scroll', () => active?.position(), true);
    window.addEventListener('resize', () => active?.position());
})();
