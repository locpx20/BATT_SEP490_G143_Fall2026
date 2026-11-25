(() => {
    const updateNotice = document.querySelector('[data-labor-update-notice]');
    if (updateNotice) {
        window.setTimeout(() => {
            updateNotice.classList.add('is-expiring');
            window.setTimeout(() => updateNotice.remove(), 220);
        }, 10000);
    }

    function initializePlanForm(form) {
    if (!form) return;

    const date = form.querySelector('[data-plan-date]');
    const shifts = [...form.querySelectorAll('[data-plan-shift]')];
    const save = form.querySelector('button[type="submit"]');
    const lookupError = form.querySelector('[data-plan-lookup-error]');
    const contextMessage = form.querySelector('[data-plan-context-message]');
    const contextContent = form.querySelector('[data-plan-context-content]');
    const contextTotalKg = form.querySelector('[data-plan-total-kg]');
    const contextMachines = form.querySelector('[data-plan-machines]');
    const quantityFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });
    let contextRequest;
    function contextRow(list, name, value, description, state) {
        const row = document.createElement('li');
        const info = document.createElement('div');
        const title = document.createElement('strong');
        title.textContent = name;
        info.append(title);
        if (description) {
            const detail = document.createElement('small');
            detail.textContent = description;
            info.append(detail);
        }
        const badge = document.createElement('span');
        badge.className = state ? `labor-context-state ${state}` : 'labor-context-quantity';
        badge.textContent = value;
        row.append(info, badge);
        list.append(row);
    }
    async function loadPlanContext() {
        if (!form.dataset.planContextUrl || !contextMessage || !contextContent) return;
        contextRequest?.abort();
        const request = new AbortController();
        contextRequest = request;
        contextMessage.hidden = true;
        contextContent.hidden = true;
        try {
            const response = await fetch(form.dataset.planContextUrl, { cache: 'no-store', signal: request.signal });
            if (!response.ok || response.redirected) throw new Error();
            const data = await response.json();
            if (!Array.isArray(data.stages)) throw new Error();
            contextTotalKg.textContent = data.totalKg == null ? 'Chưa có dữ liệu kg' : `${quantityFormat.format(data.totalKg)} kg`;
            contextMachines.replaceChildren();
            for (const stage of data.stages) {
                if (!stage.machines.length) {
                    contextRow(contextMachines, stage.name, 'Chưa có dữ liệu', null, 'is-unknown');
                    continue;
                }
                for (const machine of stage.machines) {
                    contextRow(contextMachines, stage.name, machine.status, null,
                        machine.stopped ? 'is-stopped' : machine.status === 'Cần bảo trì' || machine.repair ? 'is-warning' : 'is-active');
                }
            }
            contextContent.hidden = false;
        } catch (error) {
            if (error.name === 'AbortError') return;
            contextMessage.textContent = 'Không tải được tồn kho và tình trạng máy. Vui lòng mở lại phiếu để thử lại.';
            contextMessage.hidden = false;
        }
    }
    function updateShift(shift) {
        const mode = shift.querySelector('[data-plan-mode]');
        const fields = shift.querySelector('[data-plan-fields]');
        const note = shift.querySelector('[data-plan-note]');
        const off = mode.querySelector('option[value="Off"]');
        if (off) {
            off.disabled = date.value !== form.dataset.today;
            if (off.disabled && mode.value === 'Off') mode.value = mode.options[0].value;
        }
        const working = !mode.disabled && mode.value === 'Work';
        const scheduled = !mode.disabled && (working || mode.value === 'Off');
        shift.dataset.mode = mode.disabled ? '' : mode.value;
        if (fields) {
            fields.hidden = fields.disabled = !working;
            fields.querySelectorAll('[data-plan-quantity]').forEach(input => input.required = working);
        }
        note.hidden = note.disabled = !scheduled;
    }

    function setScheduledShifts(scheduled) {
        const canEdit = date.value === form.dataset.today;
        const occupiedNames = new Set(scheduled.map(name => name.toLowerCase()));
        let hasAvailableShift = false;
        for (const shift of shifts) {
            const occupied = occupiedNames.has(shift.dataset.planShift.toLowerCase());
            hasAvailableShift ||= !occupied;
            const mode = shift.querySelector('[data-plan-mode]');
            shift.querySelector('[data-plan-choice]').hidden = occupied;
            shift.querySelector('[data-plan-existing]').hidden = !occupied;
            shift.querySelector('[data-plan-edit]').hidden = !canEdit;
            shift.querySelector('[data-plan-readonly]').hidden = canEdit;
            mode.disabled = occupied;
            if (occupied) mode.value = '';
            updateShift(shift);
        }
        save.disabled = !hasAvailableShift;
    }

    let lookup;
    async function loadScheduledShifts() {
        lookup?.abort();
        lookupError.hidden = true;
        save.disabled = true;
        for (const shift of shifts) {
            shift.querySelector('[data-plan-choice]').hidden = true;
            shift.querySelector('[data-plan-existing]').hidden = true;
            shift.querySelector('[data-plan-mode]').disabled = true;
            updateShift(shift);
        }
        if (!date.value || !date.validity.valid) return;

        const request = new AbortController();
        lookup = request;
        const url = new URL(form.dataset.scheduledUrl, location.href);
        url.searchParams.set('date', date.value);
        try {
            const response = await fetch(url, { cache: 'no-store', signal: request.signal });
            if (!response.ok || response.redirected) throw new Error();
            const scheduled = await response.json();
            if (!Array.isArray(scheduled)) throw new Error();
            if (!request.signal.aborted) setScheduledShifts(scheduled);
        } catch {
            if (!request.signal.aborted) lookupError.hidden = false;
        }
    }

    shifts.forEach(shift => shift.querySelector('[data-plan-mode]')
        .addEventListener('change', () => updateShift(shift)));
    if (form.dataset.scheduledUrl) date.addEventListener('change', loadScheduledShifts);
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
    if (dialog?.id === 'labor-edit-dialog') {
        form.querySelector('[data-plan-edit-back]')?.setAttribute('hidden', '');
        const cancel = form.querySelector('[data-plan-edit-cancel]');
        cancel?.removeAttribute('hidden');
        cancel?.addEventListener('click', () => dialog.close());
        loadPlanContext();
    } else if (dialog) {
        const trigger = document.querySelector('[data-plan-open]');
        function openDialog() {
            dialog.showModal();
            loadPlanContext();
            (dialog.querySelector('[data-plan-error]') ?? date).focus();
        }
        trigger.addEventListener('click', openDialog);
        dialog.querySelectorAll('[data-plan-close]').forEach(button =>
            button.addEventListener('click', () => dialog.close()));
        dialog.addEventListener('close', () => trigger.focus({ preventScroll: true }));
        if (dialog.dataset.openOnLoad === 'true') openDialog();
    } else {
        form.querySelector('[data-plan-edit-cancel]')?.setAttribute('hidden', '');
        loadPlanContext();
    }
    }

    initializePlanForm(document.querySelector('.labor-form'));

    const editDialog = document.getElementById('labor-edit-dialog');
    if (!editDialog) return;
    const editContent = editDialog.querySelector('[data-edit-dialog-content]');
    let editTrigger = null;
    editDialog.querySelector('[data-edit-dialog-close]').addEventListener('click', () => editDialog.close());
    editDialog.addEventListener('close', () => editTrigger?.focus({ preventScroll: true }));

    function showEditMessage(message) {
        const paragraph = document.createElement('p');
        paragraph.className = 'labor-message labor-message-error';
        paragraph.setAttribute('role', 'alert');
        paragraph.textContent = message;
        editContent.replaceChildren(paragraph);
    }

    function setupEditForm(html) {
        editContent.innerHTML = html;
        const form = editContent.querySelector('.labor-form');
        if (!form) { showEditMessage('Không tải được phiếu chỉnh sửa. Vui lòng thử lại.'); return; }
        window.BattSelect?.init(editContent);
        initializePlanForm(form);
        form.addEventListener('submit', async event => {
            event.preventDefault();
            const submit = form.querySelector('button[type="submit"]');
            submit.disabled = true;
            try {
                const response = await fetch(form.action, { method: 'POST', body: new FormData(form),
                    headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                if (!response.ok || response.redirected) throw new Error();
                if ((response.headers.get('content-type') || '').includes('application/json')) {
                    const result = await response.json();
                    if (!result.success) throw new Error();
                    window.location.reload();
                } else {
                    setupEditForm(await response.text());
                    editContent.querySelector('[data-plan-error]')?.focus();
                }
            } catch {
                submit.disabled = false;
                let message = form.querySelector('[data-plan-edit-request-error]');
                if (!message) {
                    message = document.createElement('p');
                    message.className = 'labor-message labor-message-error';
                    message.setAttribute('role', 'alert');
                    message.dataset.planEditRequestError = '';
                    form.querySelector('.labor-form-body').prepend(message);
                }
                message.textContent = 'Không thể lưu phân công. Vui lòng kiểm tra lại và thử lại.';
            }
        });
    }

    document.addEventListener('click', async event => {
        const link = event.target.closest('a[data-open-plan-edit], a[data-plan-edit]');
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        editTrigger = link;
        const createDialog = document.getElementById('labor-create-dialog');
        if (createDialog?.open) createDialog.close();
        const loading = document.createElement('p');
        loading.className = 'labor-message';
        loading.setAttribute('role', 'status');
        loading.textContent = 'Đang tải phiếu chỉnh sửa...';
        editContent.replaceChildren(loading);
        editDialog.showModal();
        try {
            const url = new URL(link.href, location.href);
            url.searchParams.set('handler', 'EditForm');
            const response = await fetch(url, { cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error();
            setupEditForm(await response.text());
            editContent.querySelector('[data-plan-date]')?.focus();
        } catch {
            showEditMessage('Không tải được phiếu chỉnh sửa. Ca có thể không còn được phép chỉnh sửa.');
        }
    });
})();
