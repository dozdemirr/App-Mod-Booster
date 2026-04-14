async function refreshErrorBanner() {
    try {
        const response = await fetch('/api/errors/current');
        const payload = await response.json();
        const banner = document.getElementById('error-banner');
        if (!banner) return;

        if (payload?.message) {
            banner.style.display = 'block';
            banner.textContent = payload.message;
        } else {
            banner.style.display = 'none';
            banner.textContent = '';
        }
    } catch {
        // no-op
    }
}

window.addEventListener('load', refreshErrorBanner);
setInterval(refreshErrorBanner, 12000);
