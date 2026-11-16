(() => {
    const bags = document.getElementById('qc-bag-count');
    if (!bags) return;

    const form = bags.form;
    const shift = form.elements.assignmentId;
    const weight = form.querySelector('[data-qc-bag-weight]');
    const incident = form.elements.hasShiftIncident;
    const exceptionField = form.querySelector('[data-qc-exception]');
    const reason = form.elements.exceptionReason;
    const lot = form.querySelector('#qc-lot-preview');
    const format = new Intl.NumberFormat('vi-VN');
    let isRecheck = false;

    function updatePreview() {
        const count = bags.valueAsNumber;
        const validBags = Number.isInteger(count) && count >= 1 && count <= Number(bags.max);
        weight.value = validBags
            ? ' · Tổng ' + format.format(count * Number(bags.dataset.kgPerBag)) + ' kg'
            : '';
        const selected = shift.selectedOptions[0];
        const prefix = selected?.dataset.lotPrefix;
        const sequence = incident.value === 'true' ? 2 : 1;
        lot.value = selected?.dataset.recheckLot || (prefix && validBags ? `${prefix}${sequence}-${count}` : '');
        lot.parentElement.hidden = !lot.value;
    }

    function updateException() {
        const required = !isRecheck && incident.value === 'true';
        exceptionField.hidden = !required;
        reason.disabled = !required;
        reason.required = required;
        updatePreview();
    }

    function selectShift() {
        const selected = shift.selectedOptions[0];
        const recheck = Boolean(selected?.dataset.recheckLot);
        if (recheck || isRecheck) {
            bags.value = selected?.dataset.recheckBags || '';
            incident.value = 'false';
        }
        isRecheck = recheck;
        bags.disabled = recheck;
        incident.disabled = recheck;
        bags.closest('.closeout-field').hidden = recheck && !selected.dataset.recheckBags;
        incident.closest('.closeout-field').hidden = recheck;
        incident.dispatchEvent(new Event('change', { bubbles: true }));
    }

    bags.addEventListener('input', updatePreview);
    shift.addEventListener('change', selectShift);
    incident.addEventListener('change', updateException);
    selectShift();
})();
