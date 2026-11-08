export async function postForm(form, action = form.action) {
    const url = new URL(action, location.href);
    if (url.origin !== location.origin) throw new Error(form.dataset.unavailable);
    const token = form.querySelector('[name="__RequestVerificationToken"]').value;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 45000);
    try {
        const response = await window.NextHttp.request(url, {
            method: "POST",
            headers: { RequestVerificationToken: token },
            body: new FormData(form),
            signal: controller.signal
        });
        const data = await response.json().catch(() => null);
        if (!data || (!response.ok && !data.kind)) throw new Error(form.dataset.unavailable);
        return data;
    } catch {
        throw new Error(form.dataset.unavailable);
    } finally {
        clearTimeout(timeout);
    }
}

export function navigateLocally(path) {
    const url = new URL(path, location.href);
    if (url.origin === location.origin) location.assign(url);
}
