document.querySelectorAll('.owner-status-control').forEach(control => {
    const select = control.querySelector('select');
    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.className = 'owner-status-trigger';
    trigger.id = `${select.id}-trigger`;
    trigger.setAttribute('aria-haspopup', 'listbox');
    trigger.setAttribute('aria-expanded', 'false');
    const list = document.createElement('div');
    list.className = 'owner-status-options';
    list.id = `${select.id}-options`;
    list.setAttribute('role', 'listbox');
    list.setAttribute('aria-label', 'Trạng thái');
    list.hidden = true;
    trigger.setAttribute('aria-controls', list.id);
    const choices = Array.from(select.options, option => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'owner-status-option';
        button.dataset.value = option.value;
        button.textContent = option.textContent;
        button.setAttribute('role', 'option');
        button.tabIndex = -1;
        button.addEventListener('click', () => {
            select.value = option.value;
            update();
            close(true);
        });
        list.append(button);
        return button;
    });
    function update() {
        trigger.textContent = select.selectedOptions[0].textContent;
        trigger.dataset.value = select.value;
        choices.forEach(button => button.setAttribute('aria-selected', String(button.dataset.value === select.value)));
    }
    function close(restoreFocus = false) {
        list.hidden = true;
        trigger.setAttribute('aria-expanded', 'false');
        if (restoreFocus) trigger.focus();
    }
    function open(index = select.selectedIndex) {
        list.hidden = false;
        trigger.setAttribute('aria-expanded', 'true');
        choices[Math.max(0, index)].focus();
    }
    trigger.addEventListener('click', () => list.hidden ? open() : close());
    trigger.addEventListener('keydown', event => {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            open();
        }
    });
    list.addEventListener('keydown', event => {
        const index = choices.indexOf(document.activeElement);
        if (event.key === 'Escape') { event.preventDefault(); close(true); }
        if (event.key === 'Tab') close();
        if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
            event.preventDefault();
            const next = event.key === 'Home' ? 0 : event.key === 'End' ? choices.length - 1
                : (index + (event.key === 'ArrowDown' ? 1 : -1) + choices.length) % choices.length;
            choices[next].focus();
        }
    });
    document.addEventListener('click', event => {
        if (!control.contains(event.target)) close();
    });
    control.append(trigger, list);
    const label = document.querySelector(`label[for="${select.id}"]`);
    if (label) label.htmlFor = trigger.id;
    select.hidden = true;
    update();
});
