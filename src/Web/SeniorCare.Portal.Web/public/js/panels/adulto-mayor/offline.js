async function api(path, options = {}) {
  try {
    return await SeniorCareAuth.api(path, options);
  } catch (error) {
    if (!navigator.onLine && options.queueWhenOffline && (options.method || "GET") !== "GET") {
      queueOfflineRequest(path, options);
      return { queued: true };
    }
    throw error;
  }
}

function queueOfflineRequest(path, options) {
  const queue = readJson(localStorage.getItem("seniorcare.offlineQueue")) || [];
  queue.push({ id: crypto.randomUUID(), path, method: options.method, body: options.body, createdAt: new Date().toISOString() });
  localStorage.setItem("seniorcare.offlineQueue", JSON.stringify(queue));
}

async function flushOfflineQueue() {
  if (!navigator.onLine) return;
  const queue = readJson(localStorage.getItem("seniorcare.offlineQueue")) || [];
  const pending = [];
  for (const item of queue) {
    try {
      await SeniorCareAuth.api(item.path, { method: item.method, body: item.body });
    } catch {
      pending.push(item);
    }
  }
  localStorage.setItem("seniorcare.offlineQueue", JSON.stringify(pending));
  if (queue.length > 0 && pending.length === 0) showToast("La información pendiente se sincronizó correctamente.");
}
