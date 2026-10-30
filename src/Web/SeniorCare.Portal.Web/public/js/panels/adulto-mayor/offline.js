// Cola de operaciones no urgentes (confirmaciones de dosis). Las emergencias usan su propia
// cola con confirmación del servidor (alertas.js).
const OFFLINE_QUEUE_KEY = "seniorcare.offlineQueue";

async function api(path, options = {}) {
  try {
    return await SeniorCareAuth.api(path, options);
  } catch (error) {
    if (options.queueWhenOffline && (options.method || "GET") !== "GET" && isTransientFailure(error)) {
      queueOfflineRequest(path, options);
      return { queued: true };
    }
    throw error;
  }
}

// Sin red (fetch lanza TypeError) o servidor caído (5xx, p. ej. 502 de Nginx sin API).
function isTransientFailure(error) {
  return !navigator.onLine || error instanceof TypeError || (error?.status ?? 0) >= 500;
}

function queueOfflineRequest(path, options) {
  const queue = readOfflineQueue();
  queue.push({ id: newRequestId(), path, method: options.method, body: options.body, createdAt: new Date().toISOString() });
  try {
    localStorage.setItem(OFFLINE_QUEUE_KEY, JSON.stringify(queue));
  } catch {
    // Sin almacenamiento la operación no se puede guardar; quien llamó ya avisa que quedó pendiente.
  }
}

function readOfflineQueue() {
  try {
    const parsed = readJson(localStorage.getItem(OFFLINE_QUEUE_KEY));
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

// Versiones anteriores guardaban los SOS en esta cola sin identificador ni hora real
// enviada al servidor. Se pasan a la cola de emergencias conservando la hora en que se pulsó.
function migrateLegacyAlerts() {
  const queue = readOfflineQueue();
  const legacy = queue.filter((item) => item.path === "/api/emergencies/alerts" && item.body);
  if (legacy.length === 0) return;
  updateSosQueue((sos) => [
    ...sos,
    ...legacy
      .filter((item) => !sos.some((entry) => entry.clientRequestId === item.id))
      .map((item) => ({
        clientRequestId: item.id || newRequestId(),
        type: item.body.type,
        residentId: item.body.residentId,
        residentName: item.body.residentName,
        message: item.body.message,
        occurredAt: item.createdAt || new Date().toISOString(),
        status: "pending",
        attempts: 0,
        lastError: null,
        nextAttemptAt: 0
      }))
  ]);
  try {
    localStorage.setItem(OFFLINE_QUEUE_KEY, JSON.stringify(queue.filter((item) => !legacy.includes(item))));
  } catch {
    // Si no se puede reescribir, el identificador evita duplicarlos en la próxima migración.
  }
}

async function flushOfflineQueue() {
  const queue = readOfflineQueue().filter((item) => item.path !== "/api/emergencies/alerts");
  if (queue.length === 0) return;
  const pending = [];
  let rejected = 0;
  for (const item of queue) {
    try {
      await SeniorCareAuth.api(item.path, { method: item.method, body: item.body });
    } catch (error) {
      // Lo que el servidor rechaza (fecha fuera de plazo, horario inexistente) no se reintenta.
      if (isTransientFailure(error)) pending.push(item);
      else rejected += 1;
    }
  }
  try {
    localStorage.setItem(OFFLINE_QUEUE_KEY, JSON.stringify(pending));
  } catch {
    return;
  }
  if (rejected > 0) {
    showToast("Una confirmación guardada sin conexión ya no se pudo registrar. Avisá a tu cuidador.", true);
  } else if (pending.length === 0) {
    showToast("Las confirmaciones pendientes se enviaron correctamente.");
  }
}
