let activeCall = null;

const WAIT_POLL_MS = 3000;
const WAIT_TIMEOUT_MS = 5 * 60 * 1000;

let resendTimer = null;

function formatCountdown(seconds) {
  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${String(seconds % 60).padStart(2, "0")}`;
}

// Bloquea "Reenviar notificación" mientras corre la cuenta regresiva (5 min).
function setResendCooldown(seconds) {
  const button = document.querySelector("#resendNotice");
  clearInterval(resendTimer);
  resendTimer = null;
  let remaining = Math.max(0, Math.ceil(Number(seconds) || 0));

  const paint = () => {
    button.disabled = remaining > 0;
    button.textContent = remaining > 0
      ? `Reenviar en ${formatCountdown(remaining)}`
      : "Reenviar notificación";
  };

  paint();
  if (remaining <= 0) return;
  resendTimer = setInterval(() => {
    remaining -= 1;
    paint();
    if (remaining <= 0) {
      clearInterval(resendTimer);
      resendTimer = null;
    }
  }, 1000);
}

async function resendFamilyNotice() {
  if (!activeCall?.room?.id) return;
  const button = document.querySelector("#resendNotice");
  button.disabled = true;
  try {
    const result = await api(`/api/communication/video-rooms/${activeCall.room.id}/notify-family`, { method: "POST" });
    speak("Le avisamos otra vez a tu familiar.");
    showToast(`Le enviamos otro aviso a tu familiar (${result?.sentTo || "correo registrado"}).`);
    setResendCooldown(result?.retryAfterSeconds ?? 300);
  } catch (error) {
    showToast(error.message, true);
    // Se vuelve a leer cuánto falta, por si ya había un aviso reciente.
    try {
      const current = await api(`/api/communication/video-rooms/${activeCall.room.id}`);
      setResendCooldown(current?.notifyRetryAfterSeconds ?? 0);
    } catch (refreshError) {
      setResendCooldown(0);
    }
  }
}

function setCallScreen({ title, hint, showLink, showEnter, endLabel }) {
  document.querySelector("#mainContent").classList.add("in-call");
  document.querySelector("#callPanel").classList.remove("is-hidden");
  document.querySelector("#callTitle").textContent = title;
  document.querySelector("#callHint").textContent = hint;
  document.querySelector("#callOpen").classList.toggle("is-hidden", !showLink);
  document.querySelector("#callEnter").classList.toggle("is-hidden", !showEnter);
  document.querySelector("#endCall").textContent = endLabel;
}

function showWaitingScreen() {
  setCallScreen({
    title: "Esperando a tu familiar…",
    hint: "Le avisamos por correo. La videollamada empezará cuando se conecte.",
    showLink: false,
    showEnter: false,
    endLabel: "Cancelar · Volver al menú principal"
  });
  document.querySelector("#endCall").focus();
}

function showInCallScreen(room, { blocked = false } = {}) {
  document.querySelector("#callOpen").href = room.roomUrl;
  setCallScreen({
    title: blocked ? "¡Tu familia ya está conectada!" : "Videollamada en curso",
    hint: blocked
      ? "Presiona el botón para entrar a la llamada."
      : "La llamada se abrió en otra ventana. Cuando termines, vuelve aquí.",
    showLink: !blocked,
    showEnter: blocked,
    endLabel: "Terminé · Volver al menú principal"
  });
  document.querySelector(blocked ? "#callEnter" : "#endCall").focus();
}

// Llamada de Jitsi dentro de la misma pantalla (iframe), con el botón de
// volver al menú siempre visible arriba.
function showEmbeddedCall(room) {
  const panel = document.querySelector("#callPanel");
  const name = state.resident?.fullName || "";
  const settings = [
    "config.prejoinConfig.enabled=false",
    "config.prejoinPageEnabled=false",
    `userInfo.displayName=${encodeURIComponent(JSON.stringify(name))}`
  ].join("&");

  panel.classList.add("call-panel--embedded");
  setCallScreen({
    title: "Videollamada con tu familia",
    hint: "",
    showLink: false,
    showEnter: false,
    endLabel: "Terminé · Volver al menú principal"
  });
  document.querySelector("#callFrameWrap").classList.remove("is-hidden");
  document.querySelector("#callFrame").src = `${room.roomUrl}#${settings}`;
  document.querySelector("#endCall").focus();
}

function resetEmbeddedCall() {
  document.querySelector("#callPanel").classList.remove("call-panel--embedded");
  document.querySelector("#callFrameWrap").classList.add("is-hidden");
  const frame = document.querySelector("#callFrame");
  frame.src = "about:blank";
  frame.hidden = false;
  document.querySelector("#callJaas")?.remove();
}

// Jitsi alojado por 8x8 (JaaS): se incrusta con su script oficial (IFrame API).
function loadJaasScript(appId) {
  if (window.JitsiMeetExternalAPI) return Promise.resolve();
  return new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = `https://8x8.vc/${encodeURIComponent(appId)}/external_api.js`;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("No se pudo cargar la videollamada."));
    document.head.appendChild(script);
  });
}

async function showJaasCall(room) {
  const name = state.resident?.fullName || "";
  document.querySelector("#callPanel").classList.add("call-panel--embedded");
  setCallScreen({
    title: "Videollamada con tu familia",
    hint: "",
    showLink: false,
    showEnter: false,
    endLabel: "Terminé · Volver al menú principal"
  });

  const wrap = document.querySelector("#callFrameWrap");
  document.querySelector("#callFrame").hidden = true;
  const host = document.createElement("div");
  host.id = "callJaas";
  host.className = "call-jaas";
  wrap.appendChild(host);
  wrap.classList.remove("is-hidden");
  document.querySelector("#endCall").focus();

  try {
    await loadJaasScript(room.jaasAppId);
    if (!activeCall) return;
    const api = new window.JitsiMeetExternalAPI("8x8.vc", {
      roomName: `${room.jaasAppId}/${room.roomCode}`,
      jwt: room.jaasToken,
      parentNode: host,
      width: "100%",
      height: "100%",
      configOverwrite: {
        prejoinConfig: { enabled: false },
        prejoinPageEnabled: false,
        disableDeepLinking: true
      },
      userInfo: { displayName: name }
    });
    activeCall.jitsi = api;
    api.addListener("readyToClose", () => endVideoCall());
  } catch (error) {
    if (!activeCall) return;
    resetEmbeddedCall();
    showToast("Abriendo la videollamada en otra ventana.", true);
    openCallWindow(room, true);
  }
}

function openCallWindow(room, fromGesture) {
  const popup = window.open(room.roomUrl, "_blank");
  if (!popup) {
    showInCallScreen(room, { blocked: !fromGesture });
    if (fromGesture) showToast("Usa el enlace para abrir la videollamada.", true);
    return;
  }
  activeCall.popup = popup;
  showInCallScreen(room);
  clearInterval(activeCall.timer);
  activeCall.timer = setInterval(() => {
    if (popup.closed) endVideoCall();
  }, 1000);
}

function waitForFamily(room) {
  showWaitingScreen();
  speak("Esperando a tu familiar. Le avisamos por correo.");
  const startedAt = Date.now();
  activeCall.timer = setInterval(async () => {
    if (!activeCall) return;
    try {
      const current = await api(`/api/communication/video-rooms/${room.id}`);
      if (!activeCall) return;
      if (current.familyJoined) {
        clearInterval(activeCall.timer);
        activeCall.room = current;
        speak("Tu familia ya se conectó.");
        openCallWindow(current, false);
        return;
      }
    } catch (error) {
      // Se reintenta en el siguiente ciclo.
    }
    if (Date.now() - startedAt > WAIT_TIMEOUT_MS) {
      speak("Tu familia no se conectó todavía. Puedes intentar más tarde.");
      showToast("Tu familia no se conectó todavía. Intenta más tarde.", true);
      endVideoCall({ silent: true });
    }
  }, WAIT_POLL_MS);
}

async function endVideoCall(options = {}) {
  const call = activeCall;
  activeCall = null;
  if (call?.timer) clearInterval(call.timer);
  clearInterval(resendTimer);
  resendTimer = null;
  try {
    call?.jitsi?.dispose();
  } catch (error) {
    // El marco ya estaba cerrado.
  }

  resetEmbeddedCall();
  document.querySelector("#callPanel").classList.add("is-hidden");
  document.querySelector("#mainContent").classList.remove("in-call");
  if (!options.silent) speak("Volviste al menú principal.");
  window.scrollTo({ top: 0, behavior: "smooth" });

  if (call?.room?.id) {
    try {
      await api(`/api/communication/video-rooms/${call.room.id}/close`, { method: "PATCH" });
    } catch (error) {
      // No se bloquea el regreso al menú si falla el cierre.
    }
  }
}

async function startVideoCall() {
  if (activeCall) return;
  try {
    const room = await api("/api/communication/video-rooms", {
      method: "POST",
      body: { residentId: state.resident.id, residentName: state.resident.fullName }
    });
    activeCall = { room, popup: null, timer: null };
    setResendCooldown(room.notifyRetryAfterSeconds ?? 0);

    // Jitsi (respaldo si Meet no está configurado) se incrusta en la misma
    // pantalla; Google Meet no se puede incrustar y se abre en otra ventana.
    if (room.provider === "jaas" && room.jaasAppId && room.jaasToken) {
      speak("Abriendo videollamada con tu familia.");
      showJaasCall(room);
    } else if (room.provider !== "google-meet") {
      speak("Abriendo videollamada con tu familia.");
      showEmbeddedCall(room);
    } else if (room.familyJoined === false) {
      waitForFamily(room);
    } else {
      speak("Abriendo videollamada con tu familia.");
      openCallWindow(room, true);
    }
  } catch (error) {
    showToast(navigator.onLine ? error.message : "La videollamada necesita conexión a internet.", true);
  }
}

document.addEventListener("DOMContentLoaded", () => {
  document.querySelector("#endCall").addEventListener("click", () => endVideoCall());
  document.querySelector("#resendNotice").addEventListener("click", resendFamilyNotice);
  document.querySelector("#callEnter").addEventListener("click", () => {
    if (activeCall) openCallWindow(activeCall.room, true);
  });
});
