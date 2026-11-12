(() => {
    const navigation = document.querySelector('[data-warehouse-inbox]');
    if (!navigation) return;
    const labels = {
        imports: 'phiếu nhập kho nguyên liệu chờ xử lý',
        consumption: 'phiếu tiêu hao nguyên liệu chờ xử lý',
        finishedReceipts: 'phiếu nhập thành phẩm chờ xử lý',
        plans: 'thông báo chưa đọc',
        finishedStock: 'thông báo kho thành phẩm chưa đọc',
        total: 'phiếu nguyên liệu chờ xử lý'
    };
    let refreshing = false;
    async function refresh() {
        if (document.hidden || refreshing) return;
        refreshing = true;
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 8000);
        try {
            const response = await fetch('/Warehouse/Index?handler=InboxCounts', {
                cache: 'no-store', signal: controller.signal
            });
            if (!response.ok || response.redirected) return;
            const counts = await response.json();
            for (const [key, label] of Object.entries(labels)) {
                if (!Number.isSafeInteger(counts[key]) || counts[key] < 0) continue;
                const badge = navigation.querySelector(`[data-warehouse-count="${key}"]`);
                if (!badge) continue;
                badge.textContent = String(counts[key]);
                badge.hidden = counts[key] === 0;
                badge.setAttribute('aria-label', `${counts[key]} ${label}`);
            }
        } catch {
            // Retain the last known counts until the next successful refresh.
        } finally {
            clearTimeout(timeout);
            refreshing = false;
        }
    }
    setInterval(refresh, 30000);
    document.addEventListener('visibilitychange', refresh);
    window.addEventListener('focus', refresh);
})();
