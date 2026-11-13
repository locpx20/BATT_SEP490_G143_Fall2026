(() => {
    const form = document.querySelector('.labor-form');
    if (!form) return;

    const date = form.querySelector('[data-plan-date]');
    const shifts = [...form.querySelectorAll('[data-plan-shift]')];
    function updateShift(shift) {
        const mode = shift.querySelector('[data-plan-mode]');
        const fields = shift.querySelector('[data-plan-fields]');
        const note = shift.querySelector('[data-plan-note]');
        const off = mode.querySelector('option[value="Off"]');
        if (off) {
            off.disabled = date.value !== form.dataset.today;
            if (off.disabled && mode.value === 'Off') mode.value = mode.options[0].value;
        }
        const working = mode.value === 'Work';
        const scheduled = working || mode.value === 'Off';
        shift.dataset.mode = mode.value;
        if (fields) {
            fields.hidden = fields.disabled = !working;
            fields.querySelectorAll('[data-plan-quantity]').forEach(input => input.required = working);
        }
        note.hidden = note.disabled = !scheduled;
    }
    shifts.forEach(shift => shift.querySelector('[data-plan-mode]')
        .addEventListener('change', () => updateShift(shift)));
    date.addEventListener('change', () => shifts.forEach(updateShift));
    shifts.forEach(updateShift);

    const pickers = [...form.querySelectorAll('[data-staff-picker]')];
    function refresh(picker) {
        const inputs = [...picker.querySelectorAll('input[type="checkbox"]')];
        const selected = inputs.filter(input => input.checked);
        const caption = picker.querySelector('[data-staff-caption]');
        caption.textContent = selected.length ? selected.map(input => input.dataset.workerName).join(', ') : 'Chọn công nhân';
        caption.title = caption.textContent;
        const count = picker.querySelector('[data-staff-count]');
        count.textContent = selected.length;
        count.hidden = !selected.length;
        if (inputs[0]) inputs[0].setCustomValidity(selected.length ? '' : 'Chọn ít nhất một công nhân cho máy/công đoạn này.');
    }
    pickers.forEach(picker => {
        refresh(picker);
        picker.addEventListener('change', () => refresh(picker));
        picker.querySelectorAll('input').forEach(input => input.addEventListener('invalid', () => { picker.open = true; }));
    });
    document.addEventListener('click', event => pickers.forEach(picker => { if (!picker.contains(event.target)) picker.open = false; }));
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const picker = pickers.find(item => item.open && item.contains(document.activeElement));
        if (!picker) return;
        event.preventDefault();
        event.stopPropagation();
        picker.open = false;
        picker.querySelector('summary').focus();
    }, true);

    const dialog = form.closest('dialog');
    if (dialog) {
        const trigger = document.querySelector('[data-plan-open]');
        function openDialog() {
            dialog.showModal();
            (dialog.querySelector('[data-plan-error]') ?? date).focus();
        }
        trigger.addEventListener('click', openDialog);
        dialog.querySelectorAll('[data-plan-close]').forEach(button =>
            button.addEventListener('click', () => dialog.close()));
        dialog.addEventListener('close', () => trigger.focus({ preventScroll: true }));
        if (dialog.dataset.openOnLoad === 'true') openDialog();
    }
})();
