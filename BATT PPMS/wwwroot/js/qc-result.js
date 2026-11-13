(() => {
    const modal = document.getElementById('qc-result');
    if (!modal) return;

    const image = modal.querySelector('#qc-result-image');
    const imageName = modal.querySelector('#qc-result-image-name');
    function showImageName() {
        imageName.textContent = image.files[0]?.name ?? 'Chưa chọn ảnh';
    }
    image?.addEventListener('change', showImageName);

    const request = modal.querySelector('#ResultInput_RequestId');
    if (request) {
        const shift = modal.querySelector('#qc-result-shift');
        const lot = modal.querySelector('#qc-result-lot');
        const date = modal.querySelector('#qc-result-date');
        const submit = modal.querySelector('button[type="submit"]');
        const error = modal.querySelector('[data-qc-result-error]');
        const retryId = request.value;
        const lotOptions = [...request.options].slice(1);
        const fields = [...request.form.querySelectorAll('[name^="ResultInput."]')].filter(field => field !== request);
        const retryValues = fields.map(field => field.value);
        let currentId = retryId;

        function showLot() {
            const selected = request.selectedOptions[0];
            lot.value = request.value ? selected.textContent : '';
            date.value = selected?.dataset.date ?? '';
            submit.disabled = !request.value;
            if (request.value === currentId) return;
            currentId = request.value;
            fields.forEach((field, index) => {
                field.value = currentId && currentId === retryId ? retryValues[index] : '';
                field.dispatchEvent(new Event('change', { bubbles: true }));
            });
            image.value = '';
            showImageName();
            if (error) error.hidden = true;
        }

        function showShift(requestId = '') {
            for (const option of lotOptions) {
                option.hidden = option.disabled = option.dataset.shift !== shift.value;
            }
            request.disabled = !shift.value;
            request.value = requestId;
            request.dispatchEvent(new Event('change', { bubbles: true }));
        }

        shift.addEventListener('change', () => showShift());
        request.addEventListener('change', showLot);
        showShift(retryId);
        modal.addEventListener('show.bs.modal', event => {
            const requestId = event.relatedTarget?.dataset.requestId;
            if (!requestId) return;
            const selected = lotOptions.find(option => option.value === requestId);
            shift.value = selected?.dataset.shift ?? '';
            shift.dispatchEvent(new Event('change', { bubbles: true }));
            showShift(requestId);
        });
    }

    if (modal.dataset.reopen === 'true') {
        bootstrap.Modal.getOrCreateInstance(modal).show();
    }
})();
