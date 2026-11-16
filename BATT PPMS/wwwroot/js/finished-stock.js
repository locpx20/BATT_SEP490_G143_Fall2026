document.querySelectorAll('[data-stock-dialog]').forEach(button => {
    button.addEventListener('click', () => {
        document.getElementById(button.dataset.stockDialog)?.showModal();
    });
});
