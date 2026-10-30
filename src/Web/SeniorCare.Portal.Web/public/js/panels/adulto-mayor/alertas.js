// Envío de emergencias (SOS) y pedidos de asistencia.
//
// Cada aviso se guarda primero en una cola persistente del navegador con un identificador
// propio y la hora real en que se pulsó. Solo se borra de la cola cuando el servidor confirma
// que lo registró (201, o 200 si ya lo tenía por un reintento). Si no hay red, la API falla
// o la sesión venció, el aviso queda "Pendiente de envío" y se reintenta solo.
//
// Límite conocido: la cola vive en este navegador. Si se pierde el equipo, se borran sus
// datos o se usa otro navegador, un aviso pendiente se pierde. Por eso, mientras haya algo
// pendiente, la pantalla avisa que el cuidador todavía NO lo recibió y que pida ayuda por
// otro medio.

const SOS_QUEUE_KEY = "seniorcare.sosQueue";
const SOS_RETRY_DELAYS_MS = [3_000, 5_000, 10_000, 20_000, 30_000, 60_000];
const SOS_REQUEST_TIMEOUT_MS = 15_000;
const SOS_CONFIRMED_VISIBLE_MS = 60_000;

const sosState = {
  memoryQueue: null,      // copia en memoria si el navegador no deja guardar
  storageFailed: false,
  sending: new Set(),
  retryTimer: null,
  lastConfirmed: null
};

const sosStatusLabels = {
  pending: "Pendiente de envío",
  sending: "Enviada · esperando confirmación del servidor",
  rejected: "No aceptada por el servidor"
};

async function sendAlert(type) {
  const emergency = type === "emergency";
  const residentId = currentResidentId();
  if (!residentId) {
    showToast("No se pudo identificar tu cuenta. Pedí ayuda por teléfono o a alguien cerca.", true);
    return;
  }

  const item = {
    clientRequestId: newRequestId(),
    type,
    residentId,
    residentName: state.resident?.fullName || state.session?.user?.displayName || "Persona adulta mayor",
    message: emergency ? "La persona adulta mayor activó el botón SOS." : "La persona adulta mayor solicitó asistencia.",
    occurredAt: new Date().toISOString(),
    status: "pending",
    attempts: 0,
    lastError: null,
    nextAttemptAt: 0
  };

  updateSosQueue((queue) => [...queue, item]);
  renderSosStatus();
  await deliverSos(item.clientRequestId);
}

async function deliverSos(clientRequestId) {
  if (sosState.sending.has(clientRequestId)) return;
  const item = readSosQueue().find((entry) => entry.clientRequestId === clientRequestId);
  if (!item || item.status === "rejected") return;

  const session = SeniorCareAuth.getSession();
  if (!session) {
    markSosFailed(item, "Tu sesión terminó. Volvé a ingresar para enviarla.", false);
    renderSosStatus();
    return;
  }

  sosState.sending.add(clientRequestId);
  updateSosItem(clientRequestId, { status: "sending", attempts: item.attempts + 1 });
  renderSosStatus();

  const attempt = { ...item, attempts: item.attempts + 1 };
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), SOS_REQUEST_TIMEOUT_MS);
  try {
    const response = await fetch("/api/emergencies/alerts", {
      method: "POST",
      headers: { Authorization: `Bearer ${session.token}`, "Content-Type": "application/json" },
      body: JSON.stringify({
        residentId: item.residentId,
        residentName: item.residentName,
        type: item.type,
        message: item.message,
        clientRequestId: item.clientRequestId,
        occurredAt: item.occurredAt
      }),
      signal: controller.signal
    });
    const payload = await readJsonResponse(response);

    if (response.status === 201 || response.status === 200) {
      confirmSos(item, payload);
    } else if (response.status === 401) {
      markSosFailed(attempt, "Tu sesión venció. Volvé a ingresar para enviarla.", false);
    } else if ([400, 403, 409].includes(response.status)) {
      // No se reintenta solo: el servidor la rechazó. Igual se conserva hasta que la persona decida.
      markSosFailed(attempt, payload?.message || "El servidor no aceptó el aviso.", true);
    } else {
      markSosFailed(attempt, "El servidor de SeniorCare no responde.", false);
    }
  } catch (error) {
    const reason = error?.name === "AbortError"
      ? "El servidor tardó demasiado en responder."
      : navigator.onLine ? "No hay conexión con el servidor de SeniorCare." : "Sin conexión a internet.";
    markSosFailed(attempt, reason, false);
  } finally {
    window.clearTimeout(timeout);
    sosState.sending.delete(clientRequestId);
    renderSosStatus();
    scheduleSosRetry();
  }
}

function confirmSos(item, alert) {
  updateSosQueue((queue) => queue.filter((entry) => entry.clientRequestId !== item.clientRequestId));
  sosState.lastConfirmed = {
    type: item.type,
    occurredAt: alert?.createdAt || item.occurredAt,
    receivedAt: alert?.receivedAt || new Date().toISOString(),
    delayed: Boolean(alert?.delayed),
    shownUntil: Date.now() + SOS_CONFIRMED_VISIBLE_MS
  };
  const what = item.type === "emergency" ? "Emergencia" : "Solicitud de asistencia";
  const message = `${what} confirmada por el servidor. Tu cuidador ya la puede ver.`;
  showToast(message);
  speak(message);
  window.setTimeout(renderSosStatus, SOS_CONFIRMED_VISIBLE_MS + 500);
}

function markSosFailed(item, reason, rejected) {
  const attempts = Math.max(item.attempts, 1);
  const delay = SOS_RETRY_DELAYS_MS[Math.min(attempts - 1, SOS_RETRY_DELAYS_MS.length - 1)];
  updateSosItem(item.clientRequestId, {
    status: rejected ? "rejected" : "pending",
    lastError: reason,
    nextAttemptAt: Date.now() + delay
  });
  const what = item.type === "emergency" ? "Tu emergencia" : "Tu solicitud";
  showToast(`${what} todavía NO llegó a tu cuidador. ${reason}`, true);
  // La voz se repite solo en el primer fallo para no saturar con cada reintento.
  if (attempts <= 1) {
    speak(`${what} todavía no llegó a tu cuidador. Si es urgente, pedí ayuda por teléfono o a alguien cerca.`);
  }
}

// Reintenta lo pendiente de esta cuenta. Se llama al iniciar, al volver la conexión,
// al volver a la pantalla y con un temporizador de respaldo.
async function flushSosQueue({ force = false } = {}) {
  const residentId = currentResidentId();
  const due = readSosQueue().filter((item) =>
    item.residentId === residentId
    && item.status !== "rejected"
    && (force || !item.nextAttemptAt || item.nextAttemptAt <= Date.now()));
  for (const item of due) {
    await deliverSos(item.clientRequestId);
  }
  renderSosStatus();
  scheduleSosRetry();
}

function scheduleSosRetry() {
  window.clearTimeout(sosState.retryTimer);
  const residentId = currentResidentId();
  const pending = readSosQueue().filter((item) => item.residentId === residentId && item.status !== "rejected");
  if (pending.length === 0) return;
  const next = Math.min(...pending.map((item) => item.nextAttemptAt || 0));
  const wait = Math.min(Math.max(next - Date.now(), 1_000), 60_000);
  sosState.retryTimer = window.setTimeout(() => flushSosQueue(), wait);
}

function renderSosStatus() {
  const container = elements.sosStatus;
  if (!container) return;
  const residentId = currentResidentId();
  const pending = readSosQueue().filter((item) => item.residentId === residentId);

  if (pending.length > 0) {
    const contact = state.resident?.emergencyContactName && state.resident?.emergencyContactPhone
      ? ` Contacto de emergencia: ${state.resident.emergencyContactName}, ${state.resident.emergencyContactPhone}.`
      : "";
    const storageWarning = sosState.storageFailed
      ? '<p class="sos-status__detail"><strong>Este equipo no pudo guardar el aviso:</strong> si cerrás esta pantalla se pierde.</p>'
      : "";
    container.className = "sos-status sos-status--pending";
    container.innerHTML = `
      <h2>${pending.some((item) => item.type === "emergency") ? "Emergencia sin confirmar" : "Aviso sin confirmar"}</h2>
      <p>Tu cuidador todavía <strong>NO</strong> recibió este aviso. SeniorCare sigue intentando enviarlo solo.</p>
      <p><strong>Si es urgente, llamá por teléfono o pedí ayuda a alguien cerca.</strong>${escapeHtml(contact)}</p>
      <ul class="sos-status__list">${pending.map((item) => `
        <li>
          <strong>${item.type === "emergency" ? "SOS" : "Asistencia"} de las ${escapeHtml(formatClock(item.occurredAt))}</strong>
          · <span data-sos-state="${escapeHtml(item.status)}">${escapeHtml(sosStatusLabels[item.status] || sosStatusLabels.pending)}</span>
          ${item.lastError ? `<span class="sos-status__detail">${escapeHtml(item.lastError)}</span>` : ""}
          ${item.status === "rejected"
            ? `<button type="button" class="sos-status__dismiss" data-sos-dismiss="${escapeHtml(item.clientRequestId)}">Ya pedí ayuda de otra forma · Quitar aviso</button>`
            : ""}
        </li>`).join("")}
      </ul>
      ${storageWarning}
      <button type="button" class="sos-status__retry" data-sos-retry>Reintentar ahora</button>`;
    container.hidden = false;
    return;
  }

  const confirmed = sosState.lastConfirmed;
  if (confirmed && confirmed.shownUntil > Date.now()) {
    const what = confirmed.type === "emergency" ? "Emergencia" : "Solicitud de asistencia";
    const delay = confirmed.delayed
      // Sin punto final: la hora ya termina en "a. m." / "p. m.".
      ? ` La pediste a las ${formatClock(confirmed.occurredAt)} y llegó a las ${formatClock(confirmed.receivedAt)}`
      : "";
    container.className = "sos-status sos-status--confirmed";
    container.innerHTML = `<h2>${what} confirmada por el servidor</h2>
      <p>Tu cuidador ya la puede ver en su panel.${escapeHtml(delay)}</p>`;
    container.hidden = false;
    return;
  }

  container.hidden = true;
  container.innerHTML = "";
}

function bindSosStatusEvents() {
  elements.sosStatus?.addEventListener("click", (event) => {
    if (event.target.closest("[data-sos-retry]")) {
      flushSosQueue({ force: true });
      return;
    }
    const dismiss = event.target.closest("[data-sos-dismiss]");
    if (dismiss) {
      // Solo avisos rechazados por el servidor, y solo por decisión de la persona.
      const id = dismiss.dataset.sosDismiss;
      updateSosQueue((queue) => queue.filter((item) => !(item.clientRequestId === id && item.status === "rejected")));
      renderSosStatus();
    }
  });
  window.addEventListener("storage", (event) => {
    if (event.key === SOS_QUEUE_KEY) renderSosStatus();
  });
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") flushSosQueue();
  });
  // Respaldo por si el navegador no avisa del regreso de la conexión.
  window.setInterval(() => flushSosQueue(), 30_000);
}

// Al recargar, lo que quedó "enviando" se trata como pendiente: el reintento lleva el mismo
// identificador y el servidor no lo duplica.
function recoverInterruptedSos() {
  updateSosQueue((queue) => queue.map((item) =>
    item.status === "sending" ? { ...item, status: "pending", nextAttemptAt: 0 } : item));
}

function currentResidentId() {
  return state.resident?.id || state.session?.user?.residentId || null;
}

function readSosQueue() {
  if (sosState.memoryQueue) return sosState.memoryQueue;
  try {
    const parsed = readJson(localStorage.getItem(SOS_QUEUE_KEY));
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function updateSosQueue(change) {
  const next = change(readSosQueue());
  try {
    localStorage.setItem(SOS_QUEUE_KEY, JSON.stringify(next));
    sosState.memoryQueue = null;
    sosState.storageFailed = false;
  } catch {
    // Almacenamiento lleno o bloqueado: se sigue intentando desde memoria y se avisa.
    sosState.memoryQueue = next;
    sosState.storageFailed = true;
  }
}

function updateSosItem(clientRequestId, changes) {
  updateSosQueue((queue) => queue.map((item) =>
    item.clientRequestId === clientRequestId ? { ...item, ...changes } : item));
}

// crypto.randomUUID solo existe en contextos seguros (https o localhost); el quiosco puede
// abrir el panel por http en la red local, así que se arma un UUID v4 con getRandomValues.
function newRequestId() {
  if (window.crypto?.randomUUID) return window.crypto.randomUUID();
  const bytes = window.crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((value) => value.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

async function readJsonResponse(response) {
  const contentType = response.headers.get("content-type") || "";
  if (!contentType.includes("application/json")) return null;
  try { return await response.json(); } catch { return null; }
}

function formatClock(value) {
  return new Intl.DateTimeFormat("es-GT", { hour: "numeric", minute: "2-digit" }).format(new Date(value));
}
