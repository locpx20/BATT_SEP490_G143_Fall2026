(() => {
    const bags = document.getElementById('qc-bag-count');
    if (!bags) return;

    const form = bags.form;
    const shift = form.elements.assignmentId;
    const weight = form.querySelector('[data-qc-bag-weight]');
    const exceptionField = form.querySelector('[data-qc-exception]');
    const reason = form.elements.exceptionReason;
    const format = new Intl.NumberFormat('vi-VN');

    function updateWeight() {
        const count = bags.valueAsNumber;
        weight.value = Number.isInteger(count) && count >= 1 && count <= Number(bags.max)
            ? ' · Tổng ' + format.format(count * Number(bags.dataset.kgPerBag)) + ' kg'
            : '';
    }

    function updateException() {
        const required = shift.selectedOptions[0]?.dataset.requiresException === 'true';
        exceptionField.hidden = !required;
        reason.disabled = !required;
        reason.required = required;
    }

    bags.addEventListener('input', updateWeight);
    shift.addEventListener('change', updateException);
    updateWeight();
    updateException();
})();
