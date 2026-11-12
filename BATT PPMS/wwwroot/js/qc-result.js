(() => {
    const modal = document.getElementById('qc-result');
    if (!modal) return;

    const shift = modal.querySelector('#ResultInput_RequestId');
    if (shift) {
        const lot = modal.querySelector('#qc-result-lot');
        const date = modal.querySelector('#qc-result-date');

        function showLot() {
            const option = shift.selectedOptions[0];
            lot.value = option?.dataset.lot ?? '';
            date.value = option?.dataset.date ?? '';
        }

        shift.addEventListener('change', showLot);
        modal.addEventListener('show.bs.modal', event => {
            const requestId = event.relatedTarget?.dataset.requestId;
            if (requestId) {
                shift.form.reset();
                shift.value = requestId;
                shift.dispatchEvent(new Event('change', { bubbles: true }));
            } else {
                showLot();
            }
        });
    }

    if (modal.dataset.reopen === 'true') {
        bootstrap.Modal.getOrCreateInstance(modal).show();
    }
})();
