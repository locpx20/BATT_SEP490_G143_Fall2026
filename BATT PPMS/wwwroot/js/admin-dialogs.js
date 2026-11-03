(() => {
    const dialog = document.getElementById('adminConfirmation');
    if (!dialog) return;
    const title = document.getElementById('adminConfirmationTitle');
    const message = document.getElementById('adminConfirmationMessage');
    const accept = dialog.querySelector('[data-admin-confirm-accept]');
    const cancel = dialog.querySelector('[data-admin-confirm-cancel]');
    const approved = new WeakSet();
    let submitted = new WeakSet();
    let pending = null;

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!form.matches('form[data-admin-confirm]')) return;
        if (submitted.has(form)) { event.preventDefault(); return; }
        if (approved.has(form)) {
            approved.delete(form);
            submitted.add(form);
            return;
        }
        event.preventDefault();
        if (dialog.open || event.submitter?.disabled) return;
        pending = { form, submitter: event.submitter, focus: document.activeElement };
        title.textContent = form.dataset.confirmTitle || 'Xác nhận thao tác';
        message.textContent = form.dataset.confirmMessage || 'Bạn có muốn thực hiện thao tác này?';
        accept.textContent = form.dataset.confirmLabel || 'Xác nhận';
        dialog.classList.toggle('is-danger', form.dataset.confirmDanger === 'true');
        dialog.showModal();
        cancel.focus();
    });

    cancel.addEventListener('click', () => dialog.close());
    dialog.addEventListener('click', event => {
        const rect = dialog.getBoundingClientRect();
        if (event.target === dialog && (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom)) dialog.close();
    });
    dialog.addEventListener('close', () => {
        if (dialog.open) return;
        const previous = pending;
        pending = null;
        if (previous?.focus?.isConnected) previous.focus.focus();
    });
    accept.addEventListener('click', () => {
        if (!pending) return;
        const action = pending;
        pending = null;
        dialog.close();
        approved.add(action.form);
        try { action.form.requestSubmit(action.submitter || undefined); }
        finally { approved.delete(action.form); }
    });
    document.addEventListener('click', event => {
        event.target.closest('[data-dismiss-admin-feedback]')?.closest('.admin-feedback')?.remove();
    });
    window.addEventListener('pageshow', () => { submitted = new WeakSet(); });
})();
