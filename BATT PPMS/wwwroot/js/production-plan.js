(() => {
    const form = document.querySelector('.labor-form');
    if (!form) return;

    form.querySelectorAll('[data-plan-shift]').forEach(shift => {
        const include = shift.querySelector('[data-plan-include]');
        const fields = shift.querySelector('[data-plan-fields]');
        function updateShift() {
            fields.disabled = !include.checked;
            fields.querySelectorAll('[data-plan-quantity]').forEach(input => input.required = include.checked);
        }
        include.addEventListener('change', updateShift);
        updateShift();
    });

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
})();
