const apiRoot = "/api/v1";
const sessionKey = "hagale.access-token";
const modeKey = "hagale.active-mode";
const customerNavKey = "hagale.customer-nav";
const customerRideStepKey = "hagale.customer-ride-step";
const customerRideDraftKey = "hagale.customer-ride-draft";
const dispatchRadiusKey = "hagale.dispatch-radius-km";
const driverSoundAlertsKey = "hagale.driver-sound-alerts";
const driverVoiceAlertsKey = "hagale.driver-voice-alerts";
const driverSystemAlertsKey = "hagale.driver-system-alerts";
const visualModeKey = "hagale.visual-mode.v2";
const accountSplashSessionKey = "hagale.account-splash-shown";
const accountSplashDurationMs = 1200;
const accountSplashLeaveMs = 950;
const app = document.querySelector("#app");
const notice = document.querySelector("#notice");

function getDefaultCustomerRideDraft() {
  return {
    pickupAddress: "",
    pickupNeighborhood: "",
    destinationAddress: "",
    destinationNeighborhood: "",
    pricingRuleKey: "",
    proposedPriceCop: "",
    paymentMethod: "Cash",
    fareMode: "DynamicFare"
  };
}

function loadCustomerRideDraft() {
  try {
    const saved = JSON.parse(sessionStorage.getItem(customerRideDraftKey) || "null");
    return { ...getDefaultCustomerRideDraft(), ...(saved || {}) };
  } catch {
    return getDefaultCustomerRideDraft();
  }
}

const state = {
  token: sessionStorage.getItem(sessionKey),
  activeMode: sessionStorage.getItem(modeKey) || "Customer",
  customerNav: sessionStorage.getItem(customerNavKey) || "ride",
  customerRideStep: sessionStorage.getItem(customerRideStepKey) || "locations",
  customerRideDraft: loadCustomerRideDraft(),
  profile: null,
  application: null,
  rideRequests: [],
  driverRideOffers: [],
  driverCurrentRideRequest: null,
  driverCompletedRideRequests: [],
  driverActivitySummary: null,
  rideRatings: {},
  rideRatingLoadingIds: new Set(),
  pricingRules: [],
  emergencyContacts: [],
  emergencyServiceChannels: [],
  platformAppearance: null,
  adminApplications: null,
  adminStatus: "All",
  dispatchRadiusKilometers: Number(sessionStorage.getItem(dispatchRadiusKey)) || 8,
  pendingPickupLocation: null,
  pendingDestinationLocation: null,
  customerRideQuote: null,
  customerRideQuoteVersion: 0,
  roadRouteCache: new Map(),
  roadRoutePending: new Map(),
  customerRideTracking: null,
  customerTrackingMap: null,
  customerTrackingPollingTimer: null,
  customerTrackingVersion: 0,
  lastCustomerJourneyVoiceKey: null,
  rideChatMessages: {},
  rideChatLoadingIds: new Set(),
  hiddenDriverOfferIds: new Set(),
  dispatchPollingTimer: null,
  driverMap: null,
  customerRideMap: null,
  rideMapPickerTarget: "pickup",
  driverNav: "requests",
  selectedDriverOfferId: null,
  driverLocationWatchId: null,
  driverLocationUsesHighAccuracy: true,
  lastDriverLocationSentAt: 0,
  isSendingDriverLocation: false,
  locationTrackingErrorShown: false,
  realtimeConnection: null,
  realtimeConnectionToken: null,
  realtimeRetryTimer: null,
  realtimeRefreshInProgress: false,
  driverSoundAlertsEnabled: localStorage.getItem(driverSoundAlertsKey) === "true",
  driverVoiceAlertsEnabled: localStorage.getItem(driverVoiceAlertsKey) === "true",
  driverSystemAlertsEnabled: localStorage.getItem(driverSystemAlertsKey) === "true",
  visualMode: localStorage.getItem(visualModeKey) || "night",
  accountSplashPending: false,
  driverAudioContext: null,
  driverAlertsUnlocked: false,
  lastDriverOfferAlertAt: 0,
  driverVoiceRecognition: null,
  driverVoiceRecognitionTimer: null,
  driverVoiceRecognitionOfferId: null,
  driverVoiceRecognitionToken: 0,
  pwaInstallPrompt: null,
  canInstallPwa: false,
  googleAuthStatus: null,
  revealObserver: null
};

function getDefaultPlatformAppearance() {
  return {
    id: 1,
    accentColor: "#FFD800",
    actionColor: "#9BE8B8",
    busyColor: "#D84545",
    customerModeLabel: "CLIENTE",
    driverModeLabel: "CONDUCTOR",
    freeStatusLabel: "LIBRE",
    busyStatusLabel: "OCUPADO",
    requestActionLabel: "PEDIR MOTO",
    driverOfferVoiceTemplate: "Nuevo servicio Hágale. Recoger en {origen}. Entregar en {destino}. Valor ofrecido {valor} pesos.",
    updatedAtUtc: new Date().toISOString()
  };
}

function getPlatformAppearance() {
  return { ...getDefaultPlatformAppearance(), ...(state.platformAppearance || {}) };
}
let googleIdentityScriptPromise = null;
let googleIdentityInitializedClientId = null;

const label = {
  Pending: "Pendiente",
  UnderReview: "En revisión",
  Approved: "Aprobada",
  Active: "Activa",
  Rejected: "Rechazada",
  Suspended: "Suspendida",
  Inactive: "Inactiva",
  Offline: "Desconectado",
  Available: "Disponible",
  Busy: "En servicio",
  Cancelled: "Cancelada",
  Accepted: "Aceptada",
  DriverEnRoute: "Conductor en camino",
  DriverArrived: "Conductor llegó a recogida",
  InProgress: "Viaje en curso",
  Completed: "Finalizado",
  CounterOfferPending: "Contraoferta pendiente",
  Motorcycle: "Moto",
  MotorcyclePremium: "Moto premium",
  MotorcycleFuturisticPremium: "Moto futurista premium",
  Delivery: "Envío",
  DeliveryPlus: "Reparto",
  PersonalIdentification: "Documento de identidad",
  DriverLicense: "Licencia de conducción",
  VehicleRegistration: "Tarjeta de propiedad",
  Insurance: "SOAT",
  Roadworthiness: "Tecnomecánica",
  SelfieVerification: "Selfie de validación",
  Cash: "Efectivo",
  Nequi: "Nequi",
  PassengerOffer: "Tu oferta",
  DynamicFare: "Tarifa dinámica",
  Other: "Otro",
  AwaitingReview: "Pendiente de revisión",
  GeneralEmergency: "Emergencia general",
  MedicalEmergency: "Emergencia médica",
  FireEmergency: "Bomberos"
};

const activeCustomerRideStatuses = new Set(["Accepted", "DriverEnRoute", "DriverArrived", "InProgress"]);
const openCustomerRideStatuses = new Set(["Pending", "CounterOfferPending", ...activeCustomerRideStatuses]);
const closedCustomerRideStatuses = new Set(["Completed", "Cancelled"]);
const baseRequiredDriverDocumentTypes = [
  "PersonalIdentification",
  "DriverLicense",
  "VehicleRegistration",
  "Insurance"
];

function getRidePaymentMethodLabel(rideRequest) {
  const paymentMethod = rideRequest?.paymentMethod || "Cash";
  return label[paymentMethod] || paymentMethod;
}

function getRideFareModeLabel(rideRequest) {
  const fareMode = rideRequest?.fareMode || "PassengerOffer";
  return label[fareMode] || fareMode;
}

function ridePreferenceTagSpans(rideRequest) {
  return `
    <span class="ride-preference-tag ride-preference-payment">${escapeHtml(getRidePaymentMethodLabel(rideRequest))}</span>
    <span class="ride-preference-tag ride-preference-fare">${escapeHtml(getRideFareModeLabel(rideRequest))}</span>`;
}

function renderRidePreferenceTags(rideRequest) {
  return `<div class="ride-preference-tags">${ridePreferenceTagSpans(rideRequest)}</div>`;
}

function applyVisualMode() {
  const isNight = state.visualMode === "night";
  document.body.classList.toggle("visual-night", isNight);
  document.body.classList.toggle("visual-day", !isNight);
}

function toggleVisualMode() {
  state.visualMode = state.visualMode === "night" ? "day" : "night";
  localStorage.setItem(visualModeKey, state.visualMode);
  renderDashboard();
  showNotice(state.visualMode === "night" ? "Modo noche activo." : "Modo día de alto contraste activo.");
}

function renderDriverVisualModeButton(extraClass = "", compact = false) {
  const isNight = state.visualMode === "night";
  const nextMode = isNight ? "día de alto contraste" : "noche";
  const icon = isNight ? "☀" : "☾";
  const text = isNight ? "Modo día" : "Modo noche";
  if (compact) {
    return `<button class="driver-mobile-icon driver-visual-toggle ${escapeHtml(extraClass)}" type="button" data-toggle-visual-mode aria-label="Cambiar a modo ${nextMode}"><span aria-hidden="true">${icon}</span><small>${isNight ? "Día" : "Noche"}</small></button>`;
  }

  return `<button class="button button-secondary small driver-visual-toggle ${escapeHtml(extraClass)}" type="button" data-toggle-visual-mode aria-label="Cambiar a modo ${nextMode}"><span aria-hidden="true">${icon}</span>${text}</button>`;
}

function escapeHtml(value = "") {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function requiresRoadworthiness(vehicle) {
  const year = Number(vehicle?.year);
  if (!Number.isFinite(year)) return false;
  return new Date().getFullYear() - year >= 2;
}

function getRequiredDriverDocumentTypes(driver) {
  const requiredTypes = [...baseRequiredDriverDocumentTypes];
  if ((driver?.vehicles || []).some(vehicle => vehicle.isActive && requiresRoadworthiness(vehicle))) {
    requiredTypes.push("Roadworthiness");
  }
  return requiredTypes;
}

function getLatestDocumentByType(driver, type) {
  return [...(driver?.documents || [])].reverse().find(document => document.type === type) || null;
}

function getMissingRequiredDriverDocumentTypes(driver) {
  return getRequiredDriverDocumentTypes(driver).filter(type => getLatestDocumentByType(driver, type)?.reviewStatus !== "Approved");
}

function renderDriverRequiredDocumentList(driver, { allowEmpty = true } = {}) {
  const requiredTypes = getRequiredDriverDocumentTypes(driver);
  const rows = requiredTypes.map(type => {
    const document = getLatestDocumentByType(driver, type);
    return `
      <li class="list-item required-document-item ${document ? "has-document" : "is-missing"}">
        <div>
          <h3>${escapeHtml(label[type] || type)}</h3>
          <p>${document
            ? document.expiresOn ? `Vence: ${escapeHtml(document.expiresOn)}` : "Cargado sin fecha de vencimiento"
            : "Pendiente de cargar"}</p>
        </div>
        ${statusBadge(document?.reviewStatus || "AwaitingReview")}
      </li>`;
  }).join("");

  if (rows || !allowEmpty) return rows;
  return '<li class="empty">Aún no hay documentos requeridos definidos.</li>';
}

function renderAdditionalDriverDocumentList(driver) {
  const requiredTypes = new Set(getRequiredDriverDocumentTypes(driver));
  const additionalDocuments = (driver?.documents || []).filter(document => !requiredTypes.has(document.type));
  if (!additionalDocuments.length) return "";

  return `
    <div>
      <h3>Documentos adicionales</h3>
      <ul class="document-list">${additionalDocuments.map(document => `<li class="list-item"><div><h3>${escapeHtml(label[document.type] || document.type)}</h3><p>${document.expiresOn ? `Vence: ${escapeHtml(document.expiresOn)}` : "Sin fecha de vencimiento"}</p></div>${statusBadge(document.reviewStatus)}</li>`).join("")}</ul>
    </div>`;
}

function getDriverDocumentInstruction(type) {
  switch (type) {
    case "PersonalIdentification":
      return "Cédula o documento del conductor. Toma foto clara por ambos lados si aplica.";
    case "DriverLicense":
      return "Licencia vigente. Debe verse nombre, número y fecha de vencimiento.";
    case "VehicleRegistration":
      return "Tarjeta de propiedad de la moto. La placa debe coincidir con el vehículo registrado.";
    case "Insurance":
      return "SOAT vigente. Sube PDF o una foto donde se vea la vigencia.";
    case "Roadworthiness":
      return "Tecnomecánica si aplica por antigüedad de la moto.";
    case "SelfieVerification":
      return "Foto actual del rostro para comparación manual del administrador.";
    default:
      return "Documento adicional para revisión administrativa.";
  }
}

function renderDriverDocumentUploadCards(driver) {
  const requiredTypes = getRequiredDriverDocumentTypes(driver);
  const cardTypes = [...new Set([...requiredTypes, "SelfieVerification"])];

  return `
    <section class="driver-document-uploader" aria-label="Carga de documentos del conductor">
      <div class="section-title">
        <div>
          <span class="eyebrow">Documentos y validación</span>
          <h3>Sube o toma foto de cada requisito</h3>
          <p class="muted small-text">En celular puedes tocar “Tomar foto”. En computador puedes usar “Subir archivo”. El administrador revisa y aprueba cada documento.</p>
        </div>
      </div>
      <div class="driver-document-upload-grid">
        ${cardTypes.map(type => renderDriverDocumentUploadCard(type, getLatestDocumentByType(driver, type), requiredTypes.includes(type))).join("")}
      </div>
    </section>`;
}

function renderDriverDocumentUploadCard(type, document, isRequired) {
  const slug = String(type).toLowerCase().replace(/[^a-z0-9]+/g, "-");
  const status = document?.reviewStatus || "AwaitingReview";
  const uploadedText = document
    ? document.expiresOn ? `Último archivo cargado · vence ${escapeHtml(document.expiresOn)}`
      : "Último archivo cargado · sin vencimiento"
    : isRequired ? "Pendiente obligatorio" : "Recomendado para validar identidad";
  const expiryLabel = type === "SelfieVerification" || type === "PersonalIdentification"
    ? "Vencimiento (si aplica)"
    : "Fecha de vencimiento";
  const uploadControls = renderDriverDocumentUploadControls(type, slug, expiryLabel, document ? "Reenviar documento" : "Enviar a revisión");

  if (document) {
    return `
      <article class="driver-document-upload-card has-document">
        <div class="document-upload-card-head">
          <div>
            <h4>${escapeHtml(label[type] || type)}</h4>
            <p>${escapeHtml(uploadedText)}</p>
          </div>
          ${statusBadge(status)}
        </div>
        <p class="document-upload-instruction">Documento enviado. Administración lo revisará; si necesitas corregirlo, abre “Actualizar archivo”.</p>
        <details class="document-update-details">
          <summary>Actualizar archivo</summary>
          <form class="document-upload-form" data-document-upload-form>
            ${uploadControls}
          </form>
        </details>
      </article>`;
  }

  return `
    <form class="driver-document-upload-card ${document ? "has-document" : "is-missing"}" data-document-upload-form>
      <div class="document-upload-card-head">
        <div>
          <h4>${escapeHtml(label[type] || type)}</h4>
          <p>${escapeHtml(uploadedText)}</p>
        </div>
        ${statusBadge(status)}
      </div>
      <p class="document-upload-instruction">${escapeHtml(getDriverDocumentInstruction(type))}</p>
      ${uploadControls}
    </form>`;
}

function renderDriverDocumentUploadControls(type, slug, expiryLabel, submitText) {
  return `
    <input type="hidden" name="type" value="${escapeHtml(type)}">
    <div class="field">
      <label for="document-expiry-${slug}">${escapeHtml(expiryLabel)}</label>
      <input id="document-expiry-${slug}" name="expiresOn" type="date">
    </div>
    <div class="document-capture-actions">
      <label class="document-file-action">
        <span aria-hidden="true">📷</span>
        <strong>Tomar foto</strong>
        <input name="cameraFile" type="file" accept="image/*" capture="environment">
      </label>
      <label class="document-file-action">
        <span aria-hidden="true">📁</span>
        <strong>Subir archivo</strong>
        <input name="uploadFile" type="file" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png">
      </label>
    </div>
    <p class="selected-document-file" data-selected-document-file>Sin archivo seleccionado.</p>
    <button class="button button-secondary document-submit-button" type="submit">${escapeHtml(submitText)}</button>`;
}

// Animaciones nativas y opcionales: no se depende de una librería externa para
// que la interfaz local siga funcionando sin conexión. Se respetan las
// preferencias de movimiento reducido del dispositivo.
function activateRevealAnimations() {
  state.revealObserver?.disconnect();
  const elements = [...app.querySelectorAll("[data-reveal]")];
  if (!elements.length) return;

  const prefersReducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches;
  if (prefersReducedMotion || !("IntersectionObserver" in window)) {
    elements.forEach(element => element.classList.add("is-revealed"));
    return;
  }

  state.revealObserver = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (!entry.isIntersecting) return;
      entry.target.classList.add("is-revealed");
      state.revealObserver?.unobserve(entry.target);
    });
  }, { threshold: 0.12 });
  elements.forEach(element => state.revealObserver.observe(element));
}

function statusBadge(value) {
  const className = String(value || "").toLowerCase().replaceAll(" ", "");
  return `<span class="badge badge-${className}">${escapeHtml(label[value] || value || "Sin estado")}</span>`;
}

function formatCop(value) {
  return new Intl.NumberFormat("es-CO", {
    style: "currency",
    currency: "COP",
    maximumFractionDigits: 0
  }).format(value);
}
function formatCopForVoice(value) {
  const amount = Math.max(0, Math.round(Number(value) || 0));
  return `${new Intl.NumberFormat("es-CO").format(amount)} pesos`;
}

function numberToSpanishUnderOneThousand(value) {
  const n = Number(value);
  const units = ["cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez", "once", "doce", "trece", "catorce", "quince", "dieciseis", "diecisiete", "dieciocho", "diecinueve", "veinte", "veintiuno", "veintidos", "veintitres", "veinticuatro", "veinticinco", "veintiseis", "veintisiete", "veintiocho", "veintinueve"];
  const tens = ["", "", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"];
  const hundreds = ["", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos"];
  if (!Number.isFinite(n)) return String(value);
  if (n < 30) return units[n];
  if (n < 100) return `${tens[Math.floor(n / 10)]}${n % 10 ? ` y ${units[n % 10]}` : ""}`;
  if (n === 100) return "cien";
  return `${hundreds[Math.floor(n / 100)]}${n % 100 ? ` ${numberToSpanishUnderOneThousand(n % 100)}` : ""}`;
}

function numberToSpanishForVoice(value) {
  const n = Math.round(Number(value));
  if (!Number.isFinite(n) || n < 0) return String(value);
  if (n < 1000) return numberToSpanishUnderOneThousand(n);
  if (n < 10000) {
    const thousands = Math.floor(n / 1000);
    const rest = n % 1000;
    return `${thousands === 1 ? "mil" : `${numberToSpanishUnderOneThousand(thousands)} mil`}${rest ? ` ${numberToSpanishUnderOneThousand(rest)}` : ""}`;
  }
  return new Intl.NumberFormat("es-CO").format(n);
}

function normalizeAddressForVoice(address) {
  return String(address || "")
    .replace(/\bcl\.?\b/gi, "calle")
    .replace(/\bcra\.?\b/gi, "carrera")
    .replace(/\bkr\.?\b/gi, "carrera")
    .replace(/\bav\.?\b/gi, "avenida")
    .replace(/#/g, " numero ")
    .replace(/-/g, " con ")
    .replace(/\b\d+\b/g, match => numberToSpanishForVoice(match))
    .replace(/\s+/g, " ")
    .trim();
}

function splitAddressNeighborhood(address) {
  const value = String(address || "").replace(/\s+/g, " ").trim();
  const match = value.match(/^(.+?)\s*·\s*Barrio:\s*(.+)$/i);
  return match
    ? { address: match[1].trim(), neighborhood: match[2].trim() }
    : { address: value, neighborhood: "" };
}

function formatDriverOfferAddress(address) {
  const parts = splitAddressNeighborhood(address);
  const neighborhood = parts.neighborhood
    ? "Barrio " + escapeHtml(parts.neighborhood) + " · "
    : "";
  return neighborhood + escapeHtml(parts.address);
}

function formatDistanceForVoice(value) {
  const distance = Number(value);
  if (!Number.isFinite(distance) || distance < 0) return "distancia pendiente";
  return String(new Intl.NumberFormat("es-CO", { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(distance)) + " kilómetros";
}

function getOfferEstimatedMinutes(offer) {
  const explicit = Number(offer?.estimatedDurationMinutes ?? offer?.tripDurationMinutes);
  if (Number.isFinite(explicit) && explicit > 0) return Math.round(explicit);
  return null;
}

function formatDriverOfferDuration(offer) {
  const minutes = getOfferEstimatedMinutes(offer);
  return minutes ? `${minutes} min` : "GPS pendiente";
}

function formatDateTime(value) {
  return value
    ? new Intl.DateTimeFormat("es-CO", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
    : "Aún no registrado";
}

function formatDistance(value) {
  return value === null || value === undefined
    ? "Sin ubicación"
    : `${new Intl.NumberFormat("es-CO", { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(Number(value))} km`;
}

function formatPickupProximity(value) {
  if (value === null || value === undefined || !Number.isFinite(Number(value))) return "GPS pendiente";
  const meters = Math.max(0, Math.round(Number(value) * 1000));
  return meters < 1000
    ? `~${new Intl.NumberFormat("es-CO").format(meters)} m`
    : `~${formatDistance(value)}`;
}

function formatCopPerKilometer(priceCop, distanceKilometers) {
  const price = Number(priceCop);
  const distance = Number(distanceKilometers);
  if (!Number.isFinite(price) || !Number.isFinite(distance) || distance <= 0) return "—";
  return `${formatCop(Math.round(price / distance))}/km`;
}

function getDriverPriceReference(offer) {
  const proposedPrice = Number(offer?.proposedPriceCop);
  const directReference = Number(offer?.directDistanceReferenceFareCop);
  const minimumFare = Number(offer?.minimumFareCopAtRequest);
  if (Number.isFinite(directReference) && directReference > 0 && Number.isFinite(proposedPrice)) {
    const ratio = proposedPrice / directReference;
    const fairMinimumPercent = Number(offer?.fairOfferMinimumPercent);
    const favorableMinimumPercent = Number(offer?.favorableOfferMinimumPercent);
    const fairThreshold = Number.isFinite(fairMinimumPercent) && fairMinimumPercent > 0 ? fairMinimumPercent / 100 : 0.9;
    const favorableThreshold = Number.isFinite(favorableMinimumPercent) && favorableMinimumPercent > 0 ? favorableMinimumPercent / 100 : 1.05;
    const signal = ratio >= favorableThreshold
      ? { label: "Oferta favorable", tone: "above" }
      : ratio >= fairThreshold
        ? { label: "Oferta justa", tone: "close" }
        : { label: "Oferta baja", tone: "below" };
    return {
      referenceFare: directReference,
      minimumFare: Number.isFinite(minimumFare) && minimumFare > 0 ? minimumFare : null,
      fairMinimumPercent,
      favorableMinimumPercent,
      ...signal
    };
  }

  return {
    referenceFare: null,
    minimumFare: Number.isFinite(minimumFare) && minimumFare > 0 ? minimumFare : null,
    label: "Sin referencia por distancia",
    tone: "pending"
  };
}

function renderDriverPriceReference(offer) {
  const reference = getDriverPriceReference(offer);
  const totalDistance = Number(offer?.totalDistanceKilometers);
  const directReferenceLine = reference.referenceFare
    ? `<div><span>Referencia A–B</span><strong>${formatCop(reference.referenceFare)}</strong></div>`
    : `<div><span>Referencia A–B</span><strong>GPS pendiente</strong></div>`;
  const minimumLine = reference.minimumFare
    ? `<div><span>Tarifa mínima</span><strong>${formatCop(reference.minimumFare)}</strong></div>`
    : "";

  return `
    <section class="driver-price-reference" aria-label="Referencia de la oferta">
      <div class="driver-price-reference-heading"><div><span class="eyebrow">Referencia de tarifa</span><h4>${reference.label}</h4></div><span class="driver-price-signal is-${reference.tone}">${reference.label}</span></div>
      <div class="driver-price-reference-values">
        ${directReferenceLine}
        <div><span>Oferta / km total</span><strong>${formatCopPerKilometer(offer.proposedPriceCop, totalDistance)}</strong></div>
        ${minimumLine}
      </div>
      <p>La referencia usa la regla vigente y solo la distancia directa A–B. No incluye calles, tráfico, tiempo, comisión ni pagos.</p>
    </section>`;
}

function getRequestInitials(offer) {
  const source = String(offer?.pickupAddress || "Solicitud").trim().split(/\s+/).filter(Boolean);
  return source.slice(0, 2).map(word => word[0]).join("").toUpperCase() || "S";
}

function getDriverAlertSupport() {
  return {
    sound: Boolean(window.AudioContext || window.webkitAudioContext),
    voice: Boolean(window.speechSynthesis && window.SpeechSynthesisUtterance),
    system: Boolean("Notification" in window && window.isSecureContext)
  };
}

function persistDriverAlertPreferences() {
  localStorage.setItem(driverSoundAlertsKey, state.driverSoundAlertsEnabled ? "true" : "false");
  localStorage.setItem(driverVoiceAlertsKey, state.driverVoiceAlertsEnabled ? "true" : "false");
  localStorage.setItem(driverSystemAlertsKey, state.driverSystemAlertsEnabled ? "true" : "false");
}

async function getDriverAudioContext() {
  const AudioContextType = window.AudioContext || window.webkitAudioContext;
  if (!AudioContextType) return null;
  if (!state.driverAudioContext) {
    state.driverAudioContext = new AudioContextType();
  }
  if (state.driverAudioContext.state === "suspended") {
    await state.driverAudioContext.resume();
  }
  return state.driverAudioContext;
}

function unlockDriverAlertsFromGesture() {
  if (!state.profile) return;
  state.driverAlertsUnlocked = true;
  if (state.driverSoundAlertsEnabled || state.driverVoiceAlertsEnabled) {
    void getDriverAudioContext().catch(() => {
      // El navegador puede mantener el audio bloqueado hasta otra interacción.
    });
  }
  window.speechSynthesis?.getVoices?.();
}

window.addEventListener("pointerdown", unlockDriverAlertsFromGesture, { passive: true });
window.addEventListener("keydown", unlockDriverAlertsFromGesture, { passive: true });

async function playDriverOfferTone(force = false, repeatCount = 1) {
  if (!force && !state.driverSoundAlertsEnabled) return;
  try {
    const context = await getDriverAudioContext();
    if (!context) return;
    const startedAt = context.currentTime + 0.02;
    const tones = [880, 1174, 1568, 1174];
    for (let repeat = 0; repeat < repeatCount; repeat += 1) {
      tones.forEach((frequency, index) => {
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        const toneStart = startedAt + repeat * 0.78 + index * 0.14;
        oscillator.type = "sine";
        oscillator.frequency.setValueAtTime(frequency, toneStart);
        gain.gain.setValueAtTime(0.0001, toneStart);
        gain.gain.exponentialRampToValueAtTime(0.3, toneStart + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, toneStart + 0.13);
        oscillator.connect(gain).connect(context.destination);
        oscillator.start(toneStart);
        oscillator.stop(toneStart + 0.15);
      });
    }
  } catch {
    // Algunos navegadores bloquean audio automático hasta que el conductor toca
    // "Activar avisos". La interfaz sigue funcionando con el aviso visual.
  }
}

function speakDriverAlert(message, force = false) {
  if (!force && !state.driverVoiceAlertsEnabled) return;
  if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return;

  try {
    window.speechSynthesis.cancel();
    // En móviles algunos navegadores dejan la síntesis pausada después de que
    // la pestaña vuelve al frente. Reanudarla antes de hablar evita que el
    // aviso se quede en silencio tras una interacción del usuario.
    window.speechSynthesis.resume?.();
    const utterance = new SpeechSynthesisUtterance(message);
    utterance.lang = "es-CO";
    utterance.rate = 0.96;
    utterance.pitch = 1;
    utterance.volume = 1;
    window.speechSynthesis.speak(utterance);
    return utterance;
  } catch {
    // La voz es una mejora progresiva; si el navegador la bloquea, queda el
    // sonido y el aviso visual en pantalla.
  }
}

function stopDriverVoiceAcceptance() {
  window.clearTimeout(state.driverVoiceRecognitionTimer);
  state.driverVoiceRecognitionTimer = null;
  state.driverVoiceRecognitionOfferId = null;
  const recognition = state.driverVoiceRecognition;
  state.driverVoiceRecognition = null;
  state.driverVoiceRecognitionToken += 1;
  if (recognition) {
    recognition.onresult = null;
    recognition.onerror = null;
    recognition.onend = null;
    try { recognition.stop(); } catch { /* ya estaba detenido */ }
  }
}

function normalizeVoiceCommand(value) {
  return String(value || "")
    .toLocaleLowerCase("es-CO")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^\p{L}\p{N}\s]/gu, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function isDriverAcceptanceCommand(value) {
  return /\bacepto\s+(el|este)\s+servicio\b/.test(normalizeVoiceCommand(value));
}

function startDriverVoiceAcceptance(offer) {
  const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!offer || !Recognition || !state.driverVoiceAlertsEnabled || !state.driverAlertsUnlocked) return;
  stopDriverVoiceAcceptance();
  const token = state.driverVoiceRecognitionToken;
  const recognition = new Recognition();
  state.driverVoiceRecognition = recognition;
  state.driverVoiceRecognitionOfferId = offer.id;
  recognition.lang = "es-CO";
  recognition.continuous = false;
  recognition.interimResults = false;
  recognition.maxAlternatives = 3;
  recognition.onresult = event => {
    const alternatives = [...(event.results?.[0] || [])].map(result => result.transcript);
    const accepted = alternatives.some(isDriverAcceptanceCommand);
    stopDriverVoiceAcceptance();
    if (accepted) {
      void acceptRideRequest(offer.id, "voice");
    } else {
      showNotice("No se reconoció la orden. Di: ACEPTO EL SERVICIO.", true);
    }
  };
  recognition.onerror = event => {
    if (event.error !== "aborted" && event.error !== "no-speech") {
      showNotice("No se pudo escuchar la orden de aceptación. Usa el botón si lo prefieres.", true);
    }
    stopDriverVoiceAcceptance();
  };
  recognition.onend = () => {
    if (state.driverVoiceRecognition === recognition && state.driverVoiceRecognitionToken === token) {
      stopDriverVoiceAcceptance();
    }
  };
  try {
    recognition.start();
    state.driverVoiceRecognitionTimer = window.setTimeout(() => {
      stopDriverVoiceAcceptance();
      showNotice("La ventana de voz terminó. El servicio sigue disponible para otros conductores.");
    }, 8_000);
    showNotice("Di: ACEPTO EL SERVICIO. Escuchando durante 8 segundos.", false, true);
  } catch {
    stopDriverVoiceAcceptance();
  }
}

function announceRideNotification(message, { alert = true, forceVoice = false, forceSound = false } = {}) {
  showNotice(`🔔 ${message}`, false, alert);
  if (navigator.vibrate) {
    navigator.vibrate([160, 80, 160, 80, 220]);
  }
  void playDriverOfferTone(forceSound, 2);
  return speakDriverAlert(message, forceVoice);
}

function summarizeAddressForVoice(address) {
  const clean = String(address || "")
    .split("·")[0]
    .replace(/\s+/g, " ")
    .trim();
  if (!clean) return "el destino indicado";
  const voiceReady = normalizeAddressForVoice(clean);
  return voiceReady.length > 90 ? `${voiceReady.slice(0, 87)}...` : voiceReady;
}

function fullAddressForVoice(address) {
  const clean = String(address || "").replace(/\s+/g, " ").trim();
  if (!clean) return "dirección pendiente";
  const voiceReady = normalizeAddressForVoice(clean);
  return voiceReady.length > 220 ? `${voiceReady.slice(0, 217)}...` : voiceReady;
}

function buildDriverRouteVoiceMessage(rideRequest) {
  if (!rideRequest) return "No encontré las direcciones de este servicio.";
  const pickup = fullAddressForVoice(rideRequest.pickupAddress);
  const destination = fullAddressForVoice(rideRequest.destinationAddress);
  return `Direcciones del servicio. Recogida A: ${pickup}. Destino B: ${destination}.`;
}

function repeatDriverRoute(rideRequestId) {
  const rideRequest = state.driverCurrentRideRequest?.id === rideRequestId
    ? state.driverCurrentRideRequest
    : state.driverRideOffers.find(offer => offer.id === rideRequestId);
  if (!rideRequest) {
    showNotice("La solicitud ya no está disponible.", true);
    return;
  }

  state.driverAlertsUnlocked = true;
  if (navigator.vibrate) navigator.vibrate([80, 55, 80]);
  speakDriverAlert(buildDriverRouteVoiceMessage(rideRequest), true);
  showNotice("Repitiendo las direcciones completas.");
}

function buildDriverOfferVoiceMessage(offer) {
  if (!offer) return "Tienes un nuevo servicio cerca.";
  const pickup = splitAddressNeighborhood(offer.pickupAddress);
  const destination = splitAddressNeighborhood(offer.destinationAddress);
  const pickupNeighborhood = pickup.neighborhood ? "barrio " + summarizeAddressForVoice(pickup.neighborhood) + ", " : "";
  const destinationNeighborhood = destination.neighborhood ? "barrio " + summarizeAddressForVoice(destination.neighborhood) + ", " : "";
  const totalDistance = offer.totalDistanceKilometers ?? offer.tripDistanceKilometers;
  const minutes = getOfferEstimatedMinutes(offer);
  const classification = getDriverOfferClassification(offer);
  const appearance = getPlatformAppearance();
  const distanceLine = formatDistanceForVoice(totalDistance);
  const timeLine = minutes ? numberToSpanishForVoice(minutes) + " minutos" : "tiempo pendiente";
  const origin = pickupNeighborhood + summarizeAddressForVoice(pickup.address);
  const destinationLine = destinationNeighborhood + summarizeAddressForVoice(destination.address);
  return appearance.driverOfferVoiceTemplate
    .replaceAll("{origen}", origin)
    .replaceAll("{destino}", destinationLine)
    .replaceAll("{valor}", formatCopForVoice(offer.proposedPriceCop))
    .replaceAll("{distancia}", distanceLine)
    .replaceAll("{tiempo}", timeLine) + " " + classification.label + ". Diga: ACEPTO EL SERVICIO.";
}

function notifyDriverNewOffers(newOffers) {
  const offers = Array.isArray(newOffers) ? newOffers : [];
  const newOfferCount = offers.length || Number(newOffers) || 0;
  const message = newOfferCount === 1
    ? buildDriverOfferVoiceMessage(offers[0])
    : `Tienes ${newOfferCount} nuevos servicios cerca. Abre solicitudes para ver precios y destinos.`;
  const now = Date.now();
  if (now - state.lastDriverOfferAlertAt < 3_500) return;
  state.lastDriverOfferAlertAt = now;

  const utterance = announceRideNotification(message);
  void showDriverSystemNotification(offers);
  if (offers.length === 1) {
    let started = false;
    const startRecognition = () => {
      if (started) return;
      started = true;
      startDriverVoiceAcceptance(offers[0]);
    };
    if (utterance) {
      utterance.onend = startRecognition;
      window.setTimeout(startRecognition, 12_000);
    } else {
      window.setTimeout(startRecognition, 900);
    }
  }
}

async function requestDriverSystemNotificationPermission() {
  const support = getDriverAlertSupport();
  if (!support.system) return false;
  if (Notification.permission === "granted") return true;
  if (Notification.permission === "denied") return false;
  return (await Notification.requestPermission()) === "granted";
}

async function showDriverSystemNotification(newOffers) {
  if (!state.driverSystemAlertsEnabled || !getDriverAlertSupport().system || Notification.permission !== "granted") return;

  const offers = Array.isArray(newOffers) ? newOffers : [];
  const newOfferCount = offers.length || Number(newOffers) || 0;
  const firstOffer = offers[0];
  const title = "Nuevo servicio HÁGALE";
  const body = newOfferCount === 1 && firstOffer
    ? `${formatCop(firstOffer.proposedPriceCop)} hacia ${summarizeAddressForVoice(firstOffer.destinationAddress)}. Recogida ${formatPickupProximity(firstOffer.pickupDistanceKilometers)}.`
    : `Tienes ${newOfferCount} solicitudes cerca. Abre el panel conductor para revisarlas.`;
  const options = {
    body,
    tag: "hagale-driver-offers",
    icon: "/assets/hagale-icon.svg",
    badge: "/assets/hagale-icon.svg",
    requireInteraction: true
  };

  try {
    const registration = await navigator.serviceWorker?.ready;
    if (registration?.showNotification) {
      await registration.showNotification(title, options);
      return;
    }
  } catch {
    // Si el service worker no está listo, usamos la notificación de la pestaña.
  }

  let notification;
  try {
    notification = new Notification(title, options);
  } catch {
    return;
  }
  notification.onclick = () => {
    window.focus();
    state.activeMode = "Driver";
    state.driverNav = "requests";
    sessionStorage.setItem(modeKey, state.activeMode);
    renderDashboard();
    notification.close();
  };
}

async function toggleDriverOfferAlerts() {
  const currentlyEnabled = state.driverSoundAlertsEnabled || state.driverVoiceAlertsEnabled || state.driverSystemAlertsEnabled;
  if (currentlyEnabled && !state.driverAlertsUnlocked) {
    state.driverAlertsUnlocked = true;
    await playDriverOfferTone(true);
    speakDriverAlert("Prueba de avisos HÁGALE activada. Cuando llegue un servicio sonará la alerta.", true);
    renderDashboard();
    showNotice("🔔 Prueba de sonido y voz activada.", false, true);
    return;
  }

  if (currentlyEnabled) {
    state.driverSoundAlertsEnabled = false;
    state.driverVoiceAlertsEnabled = false;
    state.driverSystemAlertsEnabled = false;
    state.driverAlertsUnlocked = false;
    persistDriverAlertPreferences();
    window.speechSynthesis?.cancel();
    renderDashboard();
    showNotice("Avisos de conductor desactivados.");
    return;
  }

  const support = getDriverAlertSupport();
  state.driverSoundAlertsEnabled = support.sound;
  state.driverVoiceAlertsEnabled = support.voice;
  state.driverSystemAlertsEnabled = await requestDriverSystemNotificationPermission();
  state.driverAlertsUnlocked = true;
  persistDriverAlertPreferences();
  await playDriverOfferTone(true);
  speakDriverAlert("Avisos activados. Te avisaré cuando llegue un nuevo servicio.", true);
  renderDashboard();

  if (state.driverSoundAlertsEnabled || state.driverVoiceAlertsEnabled || state.driverSystemAlertsEnabled) {
    showNotice(state.driverSystemAlertsEnabled ? "Avisos, voz y notificaciones activados." : "Avisos de voz y sonido activados.");
  } else {
    showNotice("Este navegador no permite avisos de voz, sonido o notificaciones.", true);
  }
}

function renderDriverAlertButton(extraClass = "") {
  const isEnabled = state.driverSoundAlertsEnabled || state.driverVoiceAlertsEnabled || state.driverSystemAlertsEnabled;
  const labelText = isEnabled
    ? (state.driverAlertsUnlocked ? "Avisos activos" : "Activar sonido")
    : "Activar voz y sonido";
  return `<button class="button ${isEnabled ? "button-secondary" : "button-primary"} small driver-alert-toggle ${escapeHtml(extraClass)}" type="button" data-toggle-driver-alerts aria-pressed="${isEnabled}" title="Activar o desactivar avisos de nuevos servicios">${labelText}</button>`;
}

function openSafetyAssist(context = "customer") {
  const contacts = state.emergencyContacts || [];
  const isCustomer = context === "customer";
  const confirmed = window.confirm("¿Quieres abrir el protocolo de seguridad? No se llamará a la policía ni se compartirá tu ubicación automáticamente.");
  if (!confirmed) return;
  if (!contacts.length) {
    if (isCustomer) {
      state.customerNav = "safety";
      sessionStorage.setItem(customerNavKey, state.customerNav);
    } else {
      state.driverNav = "settings";
    }
    renderDashboard();
    showNotice("Agrega al menos un contacto de confianza antes de preparar una alerta.", true);
    return;
  }
  showNotice("Protocolo de seguridad listo: confirma cualquier contacto o ubicación antes de enviarla. No se realiza ninguna llamada automática.");
}

function renderSafetyAssistButton(context) {
  return `<button class="button safety-assist-button small" type="button" data-open-safety-assist="${context}" title="Abrir protocolo de seguridad">⚠ Seguridad</button>`;
}
function renderDriverAlertControl() {
  const support = getDriverAlertSupport();
  const isEnabled = state.driverSoundAlertsEnabled || state.driverVoiceAlertsEnabled || state.driverSystemAlertsEnabled;
  const detail = isEnabled
    ? `${state.driverVoiceAlertsEnabled ? "Voz" : "Voz no disponible"} · ${state.driverSoundAlertsEnabled ? "sonido activo" : "sonido no disponible"} · ${state.driverSystemAlertsEnabled ? "notificación activa" : "notificación no disponible"}`
    : "Toca el botón para permitir sonido, voz y notificaciones en este navegador.";

  return `
    <article class="driver-alert-card ${isEnabled ? "is-active" : ""}">
      <div>
        <span class="eyebrow">Avisos de servicios</span>
        <strong>${isEnabled && state.driverAlertsUnlocked ? "Listo para avisarte" : "Activa voz y sonido"}</strong>
        <p>${detail}</p>
        ${!support.voice ? '<small>La voz depende del navegador del teléfono; si no está disponible queda activo el sonido.</small>' : ""}
        ${!support.system ? '<small>Las notificaciones del sistema requieren navegador compatible y normalmente HTTPS.</small>' : ""}
      </div>
      ${renderDriverAlertButton()}
    </article>`;
}

function isInstalledAppView() {
  return Boolean(window.matchMedia?.("(display-mode: standalone)")?.matches || window.navigator.standalone);
}

function renderInstallAppButton(extraClass = "") {
  if (isInstalledAppView()) return "";
  return `<button class="button button-secondary small install-app-button ${escapeHtml(extraClass)}" type="button" data-install-app>Instalar app</button>`;
}

function rerenderCurrentScreen() {
  if (state.profile) {
    renderDashboard();
  } else {
    renderWelcome();
  }
}

async function installHagaleApp() {
  if (state.pwaInstallPrompt) {
    const promptEvent = state.pwaInstallPrompt;
    state.pwaInstallPrompt = null;
    state.canInstallPwa = false;
    promptEvent.prompt();
    const choice = await promptEvent.userChoice.catch(() => null);
    showNotice(choice?.outcome === "accepted"
      ? "HÁGALE quedó lista para abrirse como app."
      : "Instalación cancelada. Puedes intentarlo luego.");
    rerenderCurrentScreen();
    return;
  }

  showNotice("En el celular usa el menú del navegador y elige “Agregar a pantalla de inicio”.");
}

function bindInstallAppEvents() {
  app.querySelectorAll("[data-install-app]").forEach(button => {
    button.addEventListener("click", installHagaleApp);
  });
}

function registerHagaleServiceWorker() {
  if (!("serviceWorker" in navigator)) return;
  window.addEventListener("load", () => {
    navigator.serviceWorker.register("/service-worker.js").catch(() => {
      // La app sigue funcionando aunque el navegador no acepte service worker
      // en la red local o sin HTTPS.
    });
  });
}

window.addEventListener("beforeinstallprompt", event => {
  event.preventDefault();
  state.pwaInstallPrompt = event;
  state.canInstallPwa = true;
  if (state.profile) renderDashboard();
});

window.addEventListener("appinstalled", () => {
  state.pwaInstallPrompt = null;
  state.canInstallPwa = false;
  showNotice("HÁGALE instalada en este dispositivo.");
});

registerHagaleServiceWorker();

function getDispatchOffersUrl() {
  return `/driver/ride-requests/available?maximumPickupDistanceKilometers=${encodeURIComponent(state.dispatchRadiusKilometers)}`;
}

async function refreshDriverDispatch({ announceNewOffers = false } = {}) {
  if (!state.profile?.roles.includes("Driver")) return;

  const previousIds = new Set((state.driverRideOffers || []).map(offer => offer.id));
  const previousCurrentRideId = state.driverCurrentRideRequest?.id || null;
  const previousCurrentRideStatus = state.driverCurrentRideRequest?.status || null;
  const [offers, currentRequest, application, activitySummary, completedRequests] = await Promise.all([
    request(getDispatchOffersUrl()),
    request("/driver/ride-requests/current"),
    request("/driver-application/me"),
    request("/driver/ride-requests/activity-summary"),
    request("/driver/ride-requests/completed")
  ]);
  state.driverRideOffers = offers;
  state.driverCurrentRideRequest = currentRequest;
  state.driverCompletedRideRequests = completedRequests;
  state.application = application;
  state.driverActivitySummary = activitySummary;
  if (state.selectedDriverOfferId && !offers.some(offer => offer.id === state.selectedDriverOfferId)) {
    state.selectedDriverOfferId = null;
  }

  const newOffers = announceNewOffers
    ? offers.filter(offer => !previousIds.has(offer.id))
    : [];
  const newOfferCount = newOffers.length;
  const offerSetChanged = offers.length !== previousIds.size || offers.some(offer => !previousIds.has(offer.id));
  const currentRideChanged = currentRequest?.id !== previousCurrentRideId
    || currentRequest?.status !== previousCurrentRideStatus;

  if (offerSetChanged || currentRideChanged) {
    renderDashboard();
  }
  if (newOfferCount > 0) {
    notifyDriverNewOffers(newOffers);
  }
  // Los cambios de estado provocados por el propio conductor ya se confirman
  // con el botón y el aviso visual de la acción. No los repitas como una nueva
  // oferta hablada: eso hacía que "Conductor en camino" sonara dos veces.

  return { offerSetChanged, currentRideChanged, newOfferCount };
}

function syncDispatchPolling(isDriverMode, driver) {
  window.clearInterval(state.dispatchPollingTimer);
  state.dispatchPollingTimer = null;
  if (!isDriverMode || driver?.availabilityStatus !== "Available") return;

  state.dispatchPollingTimer = window.setInterval(async () => {
    try {
      await refreshDriverDispatch({ announceNewOffers: true });
    } catch {
      // The next manual update will show any recoverable connectivity problem.
    }
  }, 20_000);
}

// Canal privado en tiempo real. Los avisos del hub no incluyen datos de viaje:
// cada cuenta vuelve a consultar los endpoints ya protegidos por el API. El
// polling existente se conserva como respaldo si la red o WebSocket fallan.
function stopRideRealtime() {
  window.clearTimeout(state.realtimeRetryTimer);
  state.realtimeRetryTimer = null;
  state.realtimeRefreshInProgress = false;

  const connection = state.realtimeConnection;
  state.realtimeConnection = null;
  state.realtimeConnectionToken = null;
  if (!connection) return;

  connection.off("rideChanged");
  connection.off("dispatchChanged");
  connection.off("driverLocationChanged");
  connection.off("driverApplicationChanged");
  connection.off("rideChatMessage");
  void connection.stop().catch(() => {
    // La conexión puede estar cerrada mientras la página cambia de sesión.
  });
}

function scheduleRideRealtimeRetry(connection) {
  if (state.realtimeConnection !== connection || !state.token) return;
  window.clearTimeout(state.realtimeRetryTimer);
  state.realtimeRetryTimer = window.setTimeout(() => {
    if (state.realtimeConnection !== connection || !state.token) return;
    state.realtimeConnection = null;
    state.realtimeConnectionToken = null;
    syncRideRealtime();
  }, 5_000);
}

async function runRealtimeRefresh(refresh) {
  if (state.realtimeRefreshInProgress || !state.profile) return;
  state.realtimeRefreshInProgress = true;
  try {
    await refresh();
  } catch {
    // El polling de respaldo recuperará cualquier cambio que llegue durante
    // una interrupción breve de red o cuando la página está cambiando de modo.
  } finally {
    state.realtimeRefreshInProgress = false;
  }
}

function syncRideRealtime() {
  if (!state.token || !window.signalR) return;
  if (state.realtimeConnection && state.realtimeConnectionToken === state.token) return;

  stopRideRealtime();
  const connection = new window.signalR.HubConnectionBuilder()
    .withUrl("/hubs/rides", { accessTokenFactory: () => state.token || "" })
    .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
    .configureLogging(window.signalR.LogLevel.Error)
    .build();

  state.realtimeConnection = connection;
  state.realtimeConnectionToken = state.token;

  connection.on("rideChanged", () => {
    if (state.realtimeConnection !== connection) return;
    void runRealtimeRefresh(async () => {
      if (state.activeMode === "Customer" && state.profile?.roles.includes("Customer")) {
        await refreshCustomerRideStatus({ notifyJourneyChange: true });
      } else if (state.activeMode === "Driver" && state.profile?.roles.includes("Driver")) {
        await refreshDriverDispatch({ announceNewOffers: true });
      }
    });
  });

  connection.on("dispatchChanged", () => {
    if (state.realtimeConnection !== connection) return;
    void runRealtimeRefresh(async () => {
      if (state.activeMode === "Driver" && state.application?.availabilityStatus === "Available") {
        await refreshDriverDispatch({ announceNewOffers: true });
      }
    });
  });

  connection.on("driverLocationChanged", rideRequestId => {
    if (state.realtimeConnection !== connection) return;
    void runRealtimeRefresh(async () => {
      const activeRide = getActiveCustomerRide();
      if (state.activeMode === "Customer" && activeRide?.id === rideRequestId) {
        await refreshCustomerRideTracking({ notifyJourneyChange: true });
      }
    });
  });

  connection.on("driverApplicationChanged", () => {
    if (state.realtimeConnection !== connection) return;
    void runRealtimeRefresh(async () => {
      // Puede ser una carga de documento, una revisión o una aprobación. El
      // API vuelve a aplicar los permisos y la interfaz actualiza solo lo que
      // cada persona autorizada puede consultar.
      await loadDashboard();
    });
  });

  connection.on("rideChatMessage", rideRequestId => {
    if (state.realtimeConnection !== connection) return;
    void runRealtimeRefresh(() => refreshRideChatIfVisible(rideRequestId));
  });

  connection.onclose(() => scheduleRideRealtimeRetry(connection));
  void connection.start().catch(() => scheduleRideRealtimeRetry(connection));
}

function toMapCoordinate(latitude, longitude) {
  const normalizedLatitude = Number(latitude);
  const normalizedLongitude = Number(longitude);
  if (!Number.isFinite(normalizedLatitude) || !Number.isFinite(normalizedLongitude)
    || Math.abs(normalizedLatitude) > 90 || Math.abs(normalizedLongitude) > 180) {
    return null;
  }

  return { latitude: normalizedLatitude, longitude: normalizedLongitude };
}

function rideMapCoordinate(rideRequest, place) {
  if (place === "pickup") {
    return toMapCoordinate(rideRequest?.pickupLatitude, rideRequest?.pickupLongitude);
  }

  return toMapCoordinate(rideRequest?.destinationLatitude, rideRequest?.destinationLongitude);
}

function buildExternalNavigationUrl(rideRequest, place) {
  const destination = rideMapCoordinate(rideRequest, place);
  if (!destination) return null;

  const parameters = new URLSearchParams({
    api: "1",
    destination: `${destination.latitude},${destination.longitude}`,
    travelmode: "driving"
  });
  const driverPosition = toMapCoordinate(
    state.application?.lastKnownLatitude,
    state.application?.lastKnownLongitude
  );
  if (driverPosition) {
    parameters.set("origin", `${driverPosition.latitude},${driverPosition.longitude}`);
  }

  return `https://www.google.com/maps/dir/?${parameters.toString()}`;
}

function renderDriverNavigationAction(rideRequest, place) {
  const targetLabel = place === "destination" ? "destino (B)" : "recogida (A)";
  const navigationUrl = buildExternalNavigationUrl(rideRequest, place);
  if (!navigationUrl) {
    return `<p class="driver-navigation-note">Comparte el punto ${place === "destination" ? "B" : "A"} para habilitar la navegación a ${targetLabel}.</p>`;
  }

  return `<a class="button button-secondary driver-navigation-link" href="${escapeHtml(navigationUrl)}" target="_blank" rel="noopener noreferrer">Abrir navegación a ${targetLabel}</a>`;
}

function getDriverMapPoints() {
  const points = [];
  const driverPosition = toMapCoordinate(
    state.application?.lastKnownLatitude,
    state.application?.lastKnownLongitude
  );

  if (driverPosition) {
    points.push({ ...driverPosition, kind: "driver", title: "Tu moto" });
  }

  const currentRequest = state.driverCurrentRideRequest;
  if (currentRequest) {
    const pickup = rideMapCoordinate(currentRequest, "pickup");
    const destination = rideMapCoordinate(currentRequest, "destination");
    if (pickup) {
      points.push({ ...pickup, kind: "pickup", title: `${currentRequest.pickupAddress} · Recogida` });
    }
    if (destination) {
      points.push({ ...destination, kind: "destination", title: `${currentRequest.destinationAddress} · Destino` });
    }
    return points;
  }

  const selectedOffer = state.driverRideOffers.find(offer => offer.id === state.selectedDriverOfferId);
  if (selectedOffer) {
    const pickup = rideMapCoordinate(selectedOffer, "pickup");
    const destination = rideMapCoordinate(selectedOffer, "destination");
    if (pickup) {
      points.push({ ...pickup, kind: "pickup", title: `${selectedOffer.pickupAddress} · Recogida` });
    }
    if (destination) {
      points.push({ ...destination, kind: "destination", title: `${selectedOffer.destinationAddress} · Destino` });
    }
    return points;
  }

  state.driverRideOffers
    .filter(offer => !state.hiddenDriverOfferIds.has(offer.id))
    .slice(0, 5)
    .forEach(offer => {
      const pickup = rideMapCoordinate(offer, "pickup");
      if (pickup) {
        points.push({ ...pickup, kind: "pickup", title: `${offer.pickupAddress} · Recogida` });
      }
    });

  return points;
}

function getDriverMapFallbackCenter() {
  const activeVehicle = state.application?.vehicles?.find(vehicle => vehicle.isActive)
    || state.application?.vehicles?.[0];
  const cityCode = String(activeVehicle?.operatingCityCode || "").trim().toUpperCase();
  const centers = {
    BUC: [7.1193, -73.1227],
    BUCARAMANGA: [7.1193, -73.1227]
  };
  return centers[cityCode] || null;
}

function getRideMapFallbackCenter(cityCode) {
  const centers = {
    BOG: [4.711, -74.0721],
    BOGOTA: [4.711, -74.0721],
    BUC: [7.1193, -73.1227],
    BUCARAMANGA: [7.1193, -73.1227],
    CAL: [3.4516, -76.532],
    CALI: [3.4516, -76.532],
    MDE: [6.2442, -75.5812],
    MEDELLIN: [6.2442, -75.5812]
  };
  return centers[String(cityCode || "").trim().toUpperCase()] || [7.1193, -73.1227];
}

function orderMapRoutePoints(points, { includeDriver = true } = {}) {
  const byKind = new Map((points || []).map(point => [point.kind, point]));
  return [
    includeDriver ? byKind.get("driver") : null,
    byKind.get("pickup"),
    byKind.get("destination")
  ].filter(Boolean);
}

function roadRouteKey(origin, destination) {
  return [origin, destination]
    .map(point => `${Number(point.latitude).toFixed(5)},${Number(point.longitude).toFixed(5)}`)
    .join(";");
}

async function getRoadRoute(origin, destination) {
  if (!origin || !destination) return null;
  const key = roadRouteKey(origin, destination);
  if (state.roadRouteCache.has(key)) return state.roadRouteCache.get(key);
  if (state.roadRoutePending.has(key)) return state.roadRoutePending.get(key);

  const query = new URLSearchParams({
    originLatitude: Number(origin.latitude).toFixed(6),
    originLongitude: Number(origin.longitude).toFixed(6),
    destinationLatitude: Number(destination.latitude).toFixed(6),
    destinationLongitude: Number(destination.longitude).toFixed(6)
  });
  const pending = request(`/routes/estimate?${query.toString()}`)
    .then(route => {
      state.roadRouteCache.set(key, route);
      return route;
    })
    .catch(error => {
      console.warn("No se pudo obtener la ruta real", error);
      return null;
    })
    .finally(() => state.roadRoutePending.delete(key));
  state.roadRoutePending.set(key, pending);
  return pending;
}

function roadRouteLatLngs(route) {
  return (route?.geometry || [])
    .map(point => [Number(point.latitude), Number(point.longitude)])
    .filter(point => point.every(Number.isFinite));
}

function drawRoadRouteSegments(leaflet, map, segments) {
  const validSegments = (segments || []).filter(segment => roadRouteLatLngs(segment.route).length > 1);
  if (!validSegments.length) return null;
  const routeLayerGroup = leaflet.layerGroup().addTo(map);
  validSegments.forEach(segment => {
    const coordinates = roadRouteLatLngs(segment.route);
    const color = segment.color || "#111218";
    const casingColor = segment.casingColor || "#ffd800";
    leaflet.polyline(coordinates, {
      color: "#ffffff",
      weight: 10,
      opacity: .94,
      lineCap: "round",
      lineJoin: "round"
    }).addTo(routeLayerGroup);
    leaflet.polyline(coordinates, {
      color,
      weight: 5,
      opacity: .98,
      lineCap: "round",
      lineJoin: "round"
    }).addTo(routeLayerGroup);
    leaflet.polyline(coordinates, {
      color: casingColor,
      weight: 1.5,
      opacity: .72,
      lineCap: "round",
      lineJoin: "round"
    }).addTo(routeLayerGroup);
  });
  return routeLayerGroup;
}

function replaceRouteBadge(container, text) {
  const badge = container?.querySelector(".driver-map-route-badge");
  if (badge && text) badge.textContent = text;
}

function drawReferenceRoute(leaflet, map, points, { includeDriver = true } = {}) {
  const orderedPoints = orderMapRoutePoints(points, { includeDriver });
  if (orderedPoints.length < 2) return null;

  const routeLayerGroup = leaflet.layerGroup().addTo(map);
  orderedPoints.slice(0, -1).forEach((point, index) => {
    const nextPoint = orderedPoints[index + 1];
    const isDriverToPickup = includeDriver
      && point.kind === "driver"
      && nextPoint.kind === "pickup";
    const color = isDriverToPickup ? "#ffd800" : "#111218";
    const casingColor = isDriverToPickup ? "#111218" : "#ffd800";
    const coordinates = [
      [point.latitude, point.longitude],
      [nextPoint.latitude, nextPoint.longitude]
    ];

    leaflet.polyline(coordinates, {
      color: "#ffffff",
      weight: 9,
      opacity: .9,
      lineCap: "round"
    }).addTo(routeLayerGroup);
    leaflet.polyline(coordinates, {
      color,
      weight: 4,
      opacity: .95,
      dashArray: "10 8",
      lineCap: "round"
    }).addTo(routeLayerGroup);
    leaflet.polyline(coordinates, {
      color: casingColor,
      weight: 1.5,
      opacity: .55,
      dashArray: "10 8",
      lineCap: "round"
    }).addTo(routeLayerGroup);
  });

  return routeLayerGroup;
}

function addMapRouteBadge(container, text) {
  if (!container || !text) return;
  const badge = document.createElement("div");
  badge.className = "driver-map-route-badge";
  badge.setAttribute("role", "status");
  badge.textContent = text;
  container.appendChild(badge);
}

function renderDriverMap(driver) {
  const isReadyForDispatch = driver?.status === "Approved"
    && ["Available", "Busy"].includes(driver?.availabilityStatus);
  const hasDriverPosition = Boolean(toMapCoordinate(driver?.lastKnownLatitude, driver?.lastKnownLongitude));
  const hasRidePoint = getDriverMapPoints().some(point => point.kind !== "driver");
  const hasMapPoint = hasDriverPosition || hasRidePoint;
  const status = hasDriverPosition
    ? state.driverLocationWatchId !== null ? "GPS en vivo" : "Ubicación registrada"
    : "GPS pendiente";
  const help = !isReadyForDispatch
    ? "Para compartir tu ubicación en el despacho, primero pulsa “Activar disponibilidad”."
    : hasDriverPosition
    ? state.driverCurrentRideRequest
      ? "Tu moto se actualiza al compartir GPS. La ruta dibujada es referencial; usa Abrir navegación para calles y giros reales."
      : "Tu moto aparece en el mapa. Las recogidas con ubicación compartida se mostrarán como puntos A."
    : "Activa el GPS para ver tu moto y ordenar las solicitudes por cercanía.";

  return `
    <article class="driver-map-card">
      <div class="section-title">
        <div><span class="eyebrow">Mapa de despacho</span><h2>Tu moto en tiempo real</h2><p class="muted">Mapa de OpenStreetMap para el despacho local.</p></div>
        <span class="map-status ${hasDriverPosition ? "is-live" : ""}"><span aria-hidden="true"></span>${status}</span>
      </div>
      <div class="driver-map-frame ${hasMapPoint ? "" : "is-empty"}">
        <div id="driver-map" class="driver-map-canvas" data-driver-map aria-label="Mapa de despacho del conductor">
          ${hasMapPoint ? "" : '<div class="driver-map-empty"><span aria-hidden="true">⌖</span><strong>Tu moto aparecerá aquí</strong><p>Comparte tu GPS cuando estés disponible.</p></div>'}
        </div>
      </div>
      <div class="driver-map-footer">
        <p>${help}</p>
        <button class="button button-secondary small" type="button" data-update-dispatch-location ${isReadyForDispatch ? "" : "disabled"}>${hasDriverPosition ? "Actualizar GPS" : "Activar GPS"}</button>
      </div>
      <div class="gps-diagnostic-row driver-gps-diagnostic" aria-live="polite">
        <button class="button button-quiet small" type="button" data-diagnose-gps>Comprobar GPS</button>
        <span data-gps-diagnostic-status>Comprueba el permiso solo si tu punto no aparece.</span>
      </div>
      <p class="map-privacy-note">El seguimiento comienza únicamente cuando tú pulsas “Activar GPS”. Al desconectarte, se detiene y la ubicación se elimina del despacho.</p>
    </article>`;
}

function destroyDriverMap() {
  if (state.driverMap?.map) {
    state.driverMap.map.remove();
  }
  state.driverMap = null;
}

function getPendingRideMapPoints() {
  const pickup = toMapCoordinate(
    state.pendingPickupLocation?.pickupLatitude,
    state.pendingPickupLocation?.pickupLongitude
  );
  const destination = toMapCoordinate(
    state.pendingDestinationLocation?.destinationLatitude,
    state.pendingDestinationLocation?.destinationLongitude
  );
  return [
    ...(pickup ? [{ ...pickup, kind: "pickup", title: "A · Punto de recogida" }] : []),
    ...(destination ? [{ ...destination, kind: "destination", title: "B · Punto de destino" }] : [])
  ];
}

function calculateDirectDistanceKilometers(start, end) {
  if (!start || !end) return null;
  const degreesToRadians = degrees => degrees * Math.PI / 180;
  const latitudeDifference = degreesToRadians(end.latitude - start.latitude);
  const longitudeDifference = degreesToRadians(end.longitude - start.longitude);
  const startLatitude = degreesToRadians(start.latitude);
  const endLatitude = degreesToRadians(end.latitude);
  const haversine = Math.sin(latitudeDifference / 2) ** 2 +
    Math.cos(startLatitude) * Math.cos(endLatitude) * Math.sin(longitudeDifference / 2) ** 2;
  return 6371.0088 * 2 * Math.atan2(Math.sqrt(haversine), Math.sqrt(1 - haversine));
}

function getDriverJourneyGuidance(rideRequest) {
  const driver = toMapCoordinate(state.application?.lastKnownLatitude, state.application?.lastKnownLongitude);
  const pickup = rideMapCoordinate(rideRequest, "pickup");
  const destination = rideMapCoordinate(rideRequest, "destination");
  const nearPickupKilometers = 0.18;
  const nearDestinationKilometers = 0.22;

  if (rideRequest.status === "Accepted") {
    return {
      tone: "ready",
      icon: "→",
      title: "Confirma salida hacia el punto A",
      message: "El GPS te acompaña, pero tú confirmas cuando realmente arrancas hacia el pasajero.",
      helper: pickup ? "Destino operativo: recogida A." : "Cuando A tenga GPS, HÁGALE medirá cercanía automáticamente."
    };
  }

  if (rideRequest.status === "DriverEnRoute") {
    const distance = calculateDirectDistanceKilometers(driver, pickup);
    if (distance !== null && distance <= nearPickupKilometers) {
      return {
        tone: "suggested",
        icon: "✓",
        title: "Estás cerca de la recogida",
        message: `GPS: ${formatPickupProximity(distance)} del punto A. Si ya llegaste, confirma “Ya llegué a recogida”.`,
        helper: "No se marca solo para evitar errores si pasas cerca sin recoger al cliente."
      };
    }

    return {
      tone: "watching",
      icon: "A",
      title: "En camino al punto A",
      message: distance === null
        ? "Comparte tu GPS y asegúrate de que el cliente haya marcado A para medir la cercanía."
        : `Aún estás a ${formatPickupProximity(distance)} del punto A.`,
      helper: "El botón se conserva manual por seguridad."
    };
  }

  if (rideRequest.status === "DriverArrived") {
    return {
      tone: "ready",
      icon: "●",
      title: "Cliente en recogida",
      message: "Inicia el viaje solo cuando el pasajero ya esté listo y se haya confirmado el servicio.",
      helper: "Así evitamos iniciar recorridos sin pasajero."
    };
  }

  if (rideRequest.status === "InProgress") {
    const distance = calculateDirectDistanceKilometers(driver, destination);
    if (distance !== null && distance <= nearDestinationKilometers) {
      return {
        tone: "suggested",
        icon: "B",
        title: "Estás cerca del destino",
        message: `GPS: ${formatPickupProximity(distance)} del punto B. Finaliza cuando el pasajero haya llegado.`,
        helper: "El cierre queda en tus manos para evitar finalizar antes de tiempo."
      };
    }

    return {
      tone: "watching",
      icon: "B",
      title: "Viaje en curso hacia B",
      message: distance === null
        ? "Cuando el destino tenga GPS, HÁGALE te dirá si ya estás cerca de B."
        : `Referencia directa al destino: ${formatPickupProximity(distance)}.`,
      helper: "Usa navegación externa si necesitas calles y giros exactos."
    };
  }

  return null;
}

function renderDriverJourneyGuidance(rideRequest) {
  const guidance = getDriverJourneyGuidance(rideRequest);
  if (!guidance) return "";

  return `
    <section class="driver-journey-guidance is-${escapeHtml(guidance.tone)}" aria-live="polite">
      <span class="driver-journey-guidance-icon" aria-hidden="true">${escapeHtml(guidance.icon)}</span>
      <div>
        <strong>${escapeHtml(guidance.title)}</strong>
        <p>${escapeHtml(guidance.message)}</p>
        <small>${escapeHtml(guidance.helper)}</small>
      </div>
    </section>`;
}

function getSelectedRidePricingRule() {
  const value = app.querySelector("#ride-pricing-rule")?.value;
  const [cityCode, serviceType] = String(value || "").split("|");
  return cityCode && serviceType ? { cityCode, serviceType } : null;
}

function renderCustomerRideQuote() {
  const pickup = toMapCoordinate(
    state.pendingPickupLocation?.pickupLatitude,
    state.pendingPickupLocation?.pickupLongitude
  );
  const destination = toMapCoordinate(
    state.pendingDestinationLocation?.destinationLatitude,
    state.pendingDestinationLocation?.destinationLongitude
  );
  const quote = state.customerRideQuote;
  if (quote) {
    return `<strong>Ruta real A–B: ${formatCop(quote.recommendedFareCop)}</strong><span>${formatDistance(quote.estimatedDistanceKilometers)} · aprox. ${quote.estimatedDurationMinutes} min por calles · mínimo ${formatCop(quote.minimumFareCop)}.</span>`;
  }
  if (pickup && destination) {
    return "<strong>Calculando ruta real…</strong><span>Estamos obteniendo kilómetros y tiempo estimado por las calles.</span>";
  }
  return "<strong>Marca A y B para calcular la ruta real.</strong><span>La tarifa mínima sigue siendo la regla obligatoria mientras no se compartan ambos puntos.</span>";
}

function updateCustomerRideQuoteUi() {
  const quoteElement = app.querySelector("#ride-price-reference");
  if (quoteElement) quoteElement.innerHTML = renderCustomerRideQuote();
}

function applyDynamicFareRecommendation() {
  const selectedFareMode = app.querySelector("#ride-request-form input[name='fareMode']:checked")?.value;
  const recommendedFare = Number(state.customerRideQuote?.recommendedFareCop);
  const priceInput = app.querySelector("#ride-proposed-price");
  const hint = app.querySelector("#minimum-fare-hint");
  if (selectedFareMode !== "DynamicFare" || !priceInput || !Number.isFinite(recommendedFare) || recommendedFare <= 0) return;

  priceInput.value = String(Math.round(recommendedFare));
  saveCustomerRideDraft({ proposedPriceCop: priceInput.value, fareMode: "DynamicFare" });
  if (hint) hint.textContent = `Tarifa dinámica sugerida por la ruta: ${formatCop(recommendedFare)}. Puedes cambiarla si prefieres enviar otra oferta.`;
}

async function refreshCustomerRideQuote() {
  const version = ++state.customerRideQuoteVersion;
  const pricingRule = getSelectedRidePricingRule();
  const pickup = toMapCoordinate(
    state.pendingPickupLocation?.pickupLatitude,
    state.pendingPickupLocation?.pickupLongitude
  );
  const destination = toMapCoordinate(
    state.pendingDestinationLocation?.destinationLatitude,
    state.pendingDestinationLocation?.destinationLongitude
  );
  if (!pricingRule || !pickup || !destination) {
    state.customerRideQuote = null;
    updateCustomerRideQuoteUi();
    return;
  }

  state.customerRideQuote = null;
  updateCustomerRideQuoteUi();
  try {
    const route = await getRoadRoute(pickup, destination);
    if (!route) {
      throw new Error("No se pudo calcular la ruta real.");
    }
    const query = new URLSearchParams({
      cityCode: pricingRule.cityCode,
      serviceType: pricingRule.serviceType,
      estimatedDistanceKilometers: Number(route.distanceKilometers).toFixed(3),
      estimatedDurationMinutes: String(route.estimatedDurationMinutes)
    });
    const quote = await request(`/pricing/quote?${query.toString()}`);
    if (version !== state.customerRideQuoteVersion) return;
    state.customerRideQuote = { ...quote, route };
    updateCustomerRideQuoteUi();
    applyDynamicFareRecommendation();
  } catch {
    if (version !== state.customerRideQuoteVersion) return;
    state.customerRideQuote = null;
    const quoteElement = app.querySelector("#ride-price-reference");
    if (quoteElement) {
      quoteElement.innerHTML = "<strong>No se pudo calcular la ruta real ahora.</strong><span>La tarifa mínima continúa siendo obligatoria; revisa la conexión e inténtalo de nuevo.</span>";
    }
  }
}

function destroyCustomerRideMap() {
  if (state.customerRideMap?.map) {
    state.customerRideMap.map.remove();
  }
  state.customerRideMap = null;
}

function getActiveCustomerRide() {
  return (state.rideRequests || []).find(rideRequest =>
    activeCustomerRideStatuses.has(rideRequest.status)) || null;
}

function getOpenCustomerRideRequest() {
  return (state.rideRequests || []).find(rideRequest =>
    openCustomerRideStatuses.has(rideRequest.status)) || null;
}

function getRideSortTime(rideRequest) {
  return new Date(
    rideRequest.completedAtUtc
    || rideRequest.cancelledAtUtc
    || rideRequest.startedAtUtc
    || rideRequest.acceptedAtUtc
    || rideRequest.requestedAtUtc
    || 0
  ).getTime();
}

function getSortedCustomerRideRequests() {
  return [...(state.rideRequests || [])].sort((left, right) => getRideSortTime(right) - getRideSortTime(left));
}

function getCustomerRideFinalLabel(rideRequest) {
  if (rideRequest.status === "Completed") return "Finalizado";
  if (rideRequest.status === "Cancelled") return "Cancelado";
  if (activeCustomerRideStatuses.has(rideRequest.status)) return "Activo";
  if (rideRequest.status === "CounterOfferPending") return "Contraoferta";
  if (rideRequest.status === "Pending") return "Buscando";
  return label[rideRequest.status] || rideRequest.status || "Solicitud";
}

function getCustomerTrackingMapPoints(rideRequest, tracking) {
  if (!rideRequest) return [];
  const pickup = toMapCoordinate(
    tracking?.pickupLatitude ?? rideRequest.pickupLatitude,
    tracking?.pickupLongitude ?? rideRequest.pickupLongitude
  );
  const destination = toMapCoordinate(
    tracking?.destinationLatitude ?? rideRequest.destinationLatitude,
    tracking?.destinationLongitude ?? rideRequest.destinationLongitude
  );
  const driver = toMapCoordinate(tracking?.driverLatitude, tracking?.driverLongitude);
  return [
    ...(driver ? [{ ...driver, kind: "driver", title: "Conductor · última ubicación compartida" }] : []),
    ...(pickup ? [{ ...pickup, kind: "pickup", title: "A · Recogida" }] : []),
    ...(destination ? [{ ...destination, kind: "destination", title: "B · Destino" }] : [])
  ];
}

function destroyCustomerTrackingMap() {
  if (state.customerTrackingMap?.map) {
    state.customerTrackingMap.map.remove();
  }
  state.customerTrackingMap = null;
}

function renderCustomerTrackingMapFallback(container, rideRequest, tracking) {
  const hasDriverLocation = Boolean(toMapCoordinate(tracking?.driverLatitude, tracking?.driverLongitude));
  container.innerHTML = `
    <div class="customer-tracking-map-unavailable">
      <strong>${hasDriverLocation ? "Ubicación recibida" : "Esperando ubicación del conductor"}</strong>
      <span>${hasDriverLocation ? "La última ubicación está disponible, pero el mapa no pudo cargarse." : "El mapa mostrará la moto cuando el conductor active y comparta su GPS durante este servicio."}</span>
    </div>`;
  state.customerTrackingMap = { fallback: true, rideRequestId: rideRequest?.id };
}

function mountCustomerTrackingMap() {
  const container = app.querySelector("[data-customer-ride-map]");
  const rideRequest = getActiveCustomerRide();
  if (!container || !rideRequest) return;

  destroyCustomerTrackingMap();
  container.innerHTML = "";
  const tracking = state.customerRideTracking;
  const points = getCustomerTrackingMapPoints(rideRequest, tracking);
  const leaflet = window.L;
  if (!leaflet) {
    renderCustomerTrackingMapFallback(container, rideRequest, tracking);
    return;
  }

  try {
    const map = leaflet.map(container, {
      zoomControl: true,
      scrollWheelZoom: false,
      attributionControl: true
    });
    leaflet.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(map);

    const bounds = [];
    points.forEach(point => {
      const latLng = [point.latitude, point.longitude];
      bounds.push(latLng);
      leaflet.marker(latLng, {
        icon: createDriverMapIcon(leaflet, point.kind),
        keyboard: true,
        title: point.title
      }).addTo(map).bindTooltip(point.title, { direction: "top", offset: [0, -15] });
    });

    const routePoints = points.filter(point => ["driver", "pickup", "destination"].includes(point.kind));
    let routeLayerGroup = drawReferenceRoute(leaflet, map, routePoints, { includeDriver: true });
    if (routeLayerGroup) {
      addMapRouteBadge(container, "Calculando ruta real por calles…");
    }
    if (bounds.length > 1) {
      map.fitBounds(bounds, { padding: [36, 36], maxZoom: 16 });
    } else if (bounds.length === 1) {
      map.setView(bounds[0], 15);
    } else {
      map.setView(getRideMapFallbackCenter(rideRequest.operatingCityCode), 13);
    }

    const mapState = { map, rideRequestId: rideRequest.id, routeLayerGroup };
    state.customerTrackingMap = mapState;
    const pickupPoint = points.find(point => point.kind === "pickup");
    const destinationPoint = points.find(point => point.kind === "destination");
    if (pickupPoint && destinationPoint) {
      void getRoadRoute(pickupPoint, destinationPoint).then(route => {
        if (!route || state.customerTrackingMap !== mapState) return;
        mapState.routeLayerGroup?.remove();
        mapState.routeLayerGroup = drawRoadRouteSegments(leaflet, map, [{
          route,
          color: "#111218",
          casingColor: "#ffd800"
        }]);
        replaceRouteBadge(container, `Ruta real A → B · ${formatDistance(route.distanceKilometers)} · ${route.estimatedDurationMinutes} min`);
      });
    }
    window.setTimeout(() => map.invalidateSize(), 0);
  } catch {
    renderCustomerTrackingMapFallback(container, rideRequest, tracking);
  }
}

function customerTrackingMessage(rideRequest, tracking) {
  const status = tracking?.status || rideRequest.status;
  const hasDriverLocation = Boolean(toMapCoordinate(tracking?.driverLatitude, tracking?.driverLongitude));
  const locationTime = tracking?.driverLocationUpdatedAtUtc
    ? `Última ubicación compartida: ${formatDateTime(tracking.driverLocationUpdatedAtUtc)}.`
    : "";
  if (status === "InProgress") {
    return hasDriverLocation
      ? `El viaje está en curso. ${locationTime}`
      : "El viaje está en curso. El mapa mostrará la moto cuando el conductor active GPS.";
  }
  if (status === "DriverEnRoute") {
    return hasDriverLocation
      ? `El conductor indicó que va en camino. ${locationTime}`
      : "El conductor indicó que va en camino. Esperando la primera ubicación compartida.";
  }
  if (status === "DriverArrived") {
    return hasDriverLocation
      ? `El conductor indicó que llegó a la recogida. ${locationTime}`
      : "El conductor indicó que llegó a la recogida. Confirmen cuando estén listos para iniciar.";
  }
  return hasDriverLocation
    ? `Tu conductor fue asignado. ${locationTime}`
    : "Tu conductor fue asignado. Esperando que active el GPS de este servicio.";
}

function getDriverInitials(driver) {
  const initials = [driver?.firstName, driver?.lastName]
    .map(value => String(value || "").trim())
    .filter(Boolean)
    .map(value => value[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();
  return initials || "H";
}

function getDriverOnlineLabel(driver) {
  if (driver?.availabilityStatus === "Busy") return "En servicio";
  if (driver?.availabilityStatus === "Available") return "En línea";
  return "Desconectado";
}

function renderDriverPhotoBadge(profile, driver, { compact = false } = {}) {
  const isBusy = driver?.availabilityStatus === "Busy";
  const isOnline = driver?.availabilityStatus === "Available";
  const stateLabel = getDriverOnlineLabel(driver);
  const vehicle = driver?.vehicles?.find(item => item.isActive);
  return `
    <div class="driver-photo-badge ${compact ? "is-compact" : ""} ${isBusy ? "is-busy" : isOnline ? "is-online" : "is-offline"}">
      <div class="driver-photo-frame" aria-hidden="true">
        <span class="driver-photo-helmet">●</span>
        <strong>${escapeHtml(getDriverInitials(profile))}</strong>
      </div>
      <div class="driver-photo-copy">
        <span>${escapeHtml(stateLabel)}</span>
        <small>${vehicle ? `${escapeHtml(vehicle.brand)} ${escapeHtml(vehicle.model)}` : "Foto de conductor"}</small>
      </div>
    </div>`;
}

function getCustomerRideStage(status) {
  if (status === "DriverEnRoute") {
    return { tone: "en-route", label: "Estado actual", headline: "VOY EN CAMINO", detail: "Tu conductor ya confirmó salida hacia el punto A." };
  }
  if (status === "DriverArrived") {
    return { tone: "arrived", label: "Estado actual", headline: "LLEGÓ A RECOGIDA", detail: "El conductor indicó que ya está en el punto A." };
  }
  if (status === "InProgress") {
    return { tone: "in-progress", label: "Estado actual", headline: "VIAJE INICIADO", detail: "El servicio ya está avanzando hacia el destino B." };
  }
  return { tone: "accepted", label: "Estado actual", headline: "CONDUCTOR ASIGNADO", detail: "El conductor aceptó el servicio y pronto confirmará salida." };
}

function renderCustomerStageAlert(status) {
  const stage = getCustomerRideStage(status);
  return `
    <section class="customer-stage-alert is-${escapeHtml(stage.tone)}" aria-live="polite">
      <span>${escapeHtml(stage.label)}</span>
      <strong>${escapeHtml(stage.headline)}</strong>
      <p>${escapeHtml(stage.detail)}</p>
    </section>`;
}

function getDriverJourneyStage(rideRequest) {
  if (!rideRequest) return null;
  const fare = formatCop(rideRequest.proposedPriceCop);
  if (rideRequest.status === "Accepted") {
    return {
      tone: "accepted",
      icon: "→",
      label: "Servicio aceptado",
      headline: "PREPÁRATE PARA SALIR",
      detail: `Oferta ${fare}. Cuando arranques hacia el punto A, toca el botón grande.`,
      action: "en-route",
      actionLabel: "Voy en camino",
      actionHelp: "Avisar al cliente"
    };
  }
  if (rideRequest.status === "DriverEnRoute") {
    return {
      tone: "en-route",
      icon: "A",
      label: "Estado activo",
      headline: "VOY EN CAMINO",
      detail: "El cliente ya ve este estado. Cuando estés en el punto A, confirma llegada.",
      action: "arrived",
      actionLabel: "Ya llegué a recogida",
      actionHelp: "Estoy en el punto A"
    };
  }
  if (rideRequest.status === "DriverArrived") {
    return {
      tone: "arrived",
      icon: "●",
      label: "Estado activo",
      headline: "LLEGUÉ A RECOGIDA",
      detail: "Espera al pasajero e inicia solo cuando ya esté listo para viajar.",
      action: "start",
      actionLabel: "Iniciar viaje",
      actionHelp: "Pasajero a bordo"
    };
  }
  if (rideRequest.status === "InProgress") {
    return {
      tone: "in-progress",
      icon: "B",
      label: "Estado activo",
      headline: "VIAJE INICIADO",
      detail: "Servicio en curso. Finaliza únicamente cuando el pasajero llegue al destino.",
      action: "complete",
      actionLabel: "Finalizar viaje",
      actionHelp: "Llegamos a B"
    };
  }
  if (rideRequest.status === "Completed") {
    return {
      tone: "completed",
      icon: "✓",
      label: "Servicio cerrado",
      headline: "VIAJE FINALIZADO",
      detail: "Quedaste disponible para recibir nuevas solicitudes.",
      action: null,
      actionLabel: "",
      actionHelp: ""
    };
  }
  return null;
}

function renderDriverJourneyStageAlert(rideRequest, stage) {
  if (!stage) return "";
  return `
    <section class="driver-stage-alert is-${escapeHtml(stage.tone)}" aria-live="polite">
      <div class="driver-stage-icon" aria-hidden="true">${escapeHtml(stage.icon)}</div>
      <div class="driver-stage-copy">
        <span>${escapeHtml(stage.label)}</span>
        <strong>${escapeHtml(stage.headline)}</strong>
        <p>${escapeHtml(stage.detail)}</p>
      </div>
      <div class="driver-stage-value">
        <span>Valor</span>
        <strong>${formatCop(rideRequest.proposedPriceCop)}</strong>
      </div>
    </section>`;
}

function renderDriverJourneyActionButton(rideRequest, stage) {
  if (!stage?.action) return "";
  return `
    <button class="button button-primary driver-stage-action is-${escapeHtml(stage.tone)}" type="button" data-journey-action="${escapeHtml(stage.action)}" data-journey-ride="${escapeHtml(rideRequest.id)}">
      <span>${escapeHtml(stage.actionLabel)}</span>
      <small>${escapeHtml(stage.actionHelp)}</small>
    </button>`;
}

function renderAssignedDriverSummary(driver, rideStatus) {
  if (!driver) {
    return `
      <div class="customer-driver-summary is-pending" data-customer-driver-summary>
        <div class="customer-driver-photo" aria-hidden="true"><div class="customer-driver-avatar">H</div><span>Asignando</span></div>
        <div class="customer-driver-summary-copy">
          <span>Conductor asignado</span>
          <strong>Preparando la ficha del servicio</strong>
          <p>Verás el nombre, la moto y la placa cuando estén disponibles.</p>
        </div>
      </div>`;
  }

  const fullName = [driver.firstName, driver.lastName]
    .map(value => String(value || "").trim())
    .filter(Boolean)
    .join(" ") || "Conductor HÁGALE";
  const vehicleParts = [
    driver.vehicleBrand,
    driver.vehicleModel,
    Number(driver.vehicleYear) > 0 ? String(driver.vehicleYear) : ""
  ].map(value => String(value || "").trim()).filter(Boolean);
  const vehicleType = label[driver.vehicleType] || String(driver.vehicleType || "Vehículo");
  const vehicleText = vehicleParts.length ? vehicleParts.join(" · ") : vehicleType;
  const colorText = String(driver.vehicleColor || "Color no informado").trim();
  const plateText = String(driver.vehiclePlate || "Pendiente").trim();
  const stage = getCustomerRideStage(rideStatus);

  return `
    <div class="customer-driver-summary is-${escapeHtml(stage.tone)}" data-customer-driver-summary>
      <div class="customer-driver-photo" aria-hidden="true">
        <div class="customer-driver-avatar">${escapeHtml(getDriverInitials(driver))}</div>
        <span>${escapeHtml(stage.headline)}</span>
      </div>
      <div class="customer-driver-summary-copy">
        <span>Tu conductor</span>
        <strong>${escapeHtml(fullName)}</strong>
        <p>${escapeHtml(vehicleText)} · ${escapeHtml(colorText)}</p>
      </div>
      <div class="customer-driver-plate">
        <span>Placa</span>
        <strong>${escapeHtml(plateText)}</strong>
        <small>${escapeHtml(vehicleType)}</small>
      </div>
    </div>`;
}

// La espera se muestra como información de estado. No es un cobro ni un pago:
// el valor se conserva para que las dos partes vean la misma regla que quedó
// registrada al llegar el conductor.
function renderWaitingInformation(rideRequest, tracking, audience) {
  const waitingStartedAtUtc = tracking?.waitingStartedAtUtc ?? rideRequest?.waitingStartedAtUtc;
  if (!waitingStartedAtUtc) return "";

  const waitingEndedAtUtc = tracking?.waitingEndedAtUtc ?? rideRequest?.waitingEndedAtUtc;
  const includedMinutes = Number(tracking?.includedWaitingMinutesAtStart ?? rideRequest?.includedWaitingMinutesAtStart ?? 0);
  const additionalRateCop = Number(tracking?.additionalWaitingFarePerMinuteCopAtStart ?? rideRequest?.additionalWaitingFarePerMinuteCopAtStart ?? 0);
  const elapsedMinutes = Math.max(0, Math.ceil((Date.now() - new Date(waitingStartedAtUtc).getTime()) / 60_000));
  const validIncludedMinutes = Number.isFinite(includedMinutes) ? includedMinutes : 0;
  const validAdditionalRateCop = Number.isFinite(additionalRateCop) ? additionalRateCop : 0;

  if (!waitingEndedAtUtc) {
    const elapsedLabel = Number.isFinite(elapsedMinutes) ? `${elapsedMinutes} min` : "en curso";
    return `
      <section class="journey-waiting journey-waiting-${audience}" aria-live="polite">
        <span class="journey-waiting-icon" aria-hidden="true">◷</span>
        <div><strong>Espera en el punto A</strong><p>Inició ${formatDateTime(waitingStartedAtUtc)} · tiempo transcurrido: ${elapsedLabel}.</p><small>La regla registrada incluye ${validIncludedMinutes} min. El valor adicional se calcula al iniciar el viaje; esta pantalla no ejecuta pagos.</small></div>
      </section>`;
  }

  const additionalMinutes = Number(tracking?.additionalWaitingMinutes ?? rideRequest?.additionalWaitingMinutes ?? 0);
  const additionalChargeCop = Number(tracking?.waitingAdditionalChargeCop ?? rideRequest?.waitingAdditionalChargeCop ?? 0);
  const validAdditionalMinutes = Number.isFinite(additionalMinutes) ? additionalMinutes : 0;
  const validAdditionalChargeCop = Number.isFinite(additionalChargeCop) ? additionalChargeCop : 0;
  const referenceLine = validAdditionalMinutes > 0 && validAdditionalRateCop > 0
    ? `${validAdditionalMinutes} min adicionales · referencia ${formatCop(validAdditionalChargeCop)}.`
    : "No se registró tiempo adicional.";
  return `
    <section class="journey-waiting journey-waiting-${audience} is-closed">
      <span class="journey-waiting-icon" aria-hidden="true">✓</span>
      <div><strong>Espera registrada</strong><p>${referenceLine}</p><small>Es un registro de la regla del servicio; no representa un pago procesado por la plataforma.</small></div>
    </section>`;
}

function renderCustomerTripGlance(rideRequest, tracking) {
  const status = tracking?.status || rideRequest.status;
  const statusText = label[status] || status;
  return `
    <section class="customer-trip-glance" aria-label="Resumen del servicio">
      <div><span>Valor acordado</span><strong>${formatCop(rideRequest.proposedPriceCop)}</strong><small>Oferta vigente del servicio</small></div>
      <div><span>Pago</span><strong>${escapeHtml(getRidePaymentMethodLabel(rideRequest))}</strong><small>Método elegido</small></div>
      <div><span>Tarifa</span><strong>${escapeHtml(getRideFareModeLabel(rideRequest))}</strong><small>Modo registrado</small></div>
      <div><span>Estado</span><strong>${escapeHtml(statusText)}</strong><small>Actualizado por el flujo del viaje</small></div>
      <div><span>Servicio</span><strong>${escapeHtml(label[rideRequest.serviceType] || rideRequest.serviceType)}</strong><small>${escapeHtml(rideRequest.operatingCityCode)}</small></div>
    </section>`;
}

async function refreshCustomerRideTracking({ notifyJourneyChange = false } = {}) {
  const version = ++state.customerTrackingVersion;
  const activeRide = getActiveCustomerRide();
  if (!activeRide) {
    state.customerRideTracking = null;
    destroyCustomerTrackingMap();
    return null;
  }

  const tracking = await request(`/ride-requests/${activeRide.id}/tracking`);
  if (version !== state.customerTrackingVersion) return null;
  const hadDriverSummary = Boolean(state.customerRideTracking?.driver);
  state.customerRideTracking = tracking;
  if (tracking?.status) {
    const updatedRide = {
      ...activeRide,
      status: tracking.status,
      acceptedAtUtc: tracking.acceptedAtUtc ?? activeRide.acceptedAtUtc,
      driverEnRouteAtUtc: tracking.driverEnRouteAtUtc ?? activeRide.driverEnRouteAtUtc,
      driverArrivedAtUtc: tracking.driverArrivedAtUtc ?? activeRide.driverArrivedAtUtc,
      waitingStartedAtUtc: tracking.waitingStartedAtUtc ?? activeRide.waitingStartedAtUtc,
      waitingEndedAtUtc: tracking.waitingEndedAtUtc ?? activeRide.waitingEndedAtUtc,
      includedWaitingMinutesAtStart: tracking.includedWaitingMinutesAtStart ?? activeRide.includedWaitingMinutesAtStart,
      additionalWaitingFarePerMinuteCopAtStart: tracking.additionalWaitingFarePerMinuteCopAtStart ?? activeRide.additionalWaitingFarePerMinuteCopAtStart,
      additionalWaitingMinutes: tracking.additionalWaitingMinutes ?? activeRide.additionalWaitingMinutes,
      waitingAdditionalChargeCop: tracking.waitingAdditionalChargeCop ?? activeRide.waitingAdditionalChargeCop,
      startedAtUtc: tracking.startedAtUtc ?? activeRide.startedAtUtc
    };
    state.rideRequests = state.rideRequests.map(rideRequest =>
      rideRequest.id === activeRide.id ? updatedRide : rideRequest);
  }
  if (tracking?.status && tracking.status !== activeRide.status) {
    renderDashboard();
    if (notifyJourneyChange) {
      announceCustomerJourneyUpdate(updatedRide, tracking.status);
    }
    return tracking;
  }

  if (Boolean(tracking?.driver) !== hadDriverSummary && app.querySelector("[data-customer-driver-summary]")) {
    renderDashboard();
    return tracking;
  }

  mountCustomerTrackingMap();
  const statusText = app.querySelector("[data-customer-tracking-status]");
  if (statusText) statusText.textContent = customerTrackingMessage(activeRide, tracking);
  return tracking;
}

async function refreshCustomerRideStatus({ notifyJourneyChange = false } = {}) {
  const previousOpenRide = getOpenCustomerRideRequest();
  const previousRideId = previousOpenRide?.id || null;
  const previousStatus = previousOpenRide?.status || null;
  const requests = await request("/ride-requests/me");
  state.rideRequests = requests;

  const currentOpenRide = getOpenCustomerRideRequest();
  const journeyChanged = currentOpenRide?.id !== previousRideId || currentOpenRide?.status !== previousStatus;

  if (getActiveCustomerRide()) {
    await refreshCustomerRideTracking();
  } else {
    state.customerRideTracking = null;
    destroyCustomerTrackingMap();
  }

  if (currentOpenRide && activeCustomerRideStatuses.has(currentOpenRide.status) && state.customerNav === "ride") {
    state.customerNav = "tracking";
    sessionStorage.setItem(customerNavKey, state.customerNav);
  }
  if (!currentOpenRide && previousOpenRide && ["tracking", "ride"].includes(state.customerNav)) {
    const refreshedPreviousRide = state.rideRequests.find(rideRequest => rideRequest.id === previousRideId);
    state.customerNav = refreshedPreviousRide && closedCustomerRideStatuses.has(refreshedPreviousRide.status) ? "history" : "ride";
    sessionStorage.setItem(customerNavKey, state.customerNav);
  }

  if (journeyChanged) {
    renderDashboard();
    if (notifyJourneyChange) {
      const completedRide = currentOpenRide
        ? null
        : state.rideRequests.find(rideRequest => rideRequest.id === previousRideId && rideRequest.status === "Completed");
      if (currentOpenRide) {
        announceCustomerJourneyUpdate(currentOpenRide, currentOpenRide.status);
      } else if (completedRide) {
        announceCustomerJourneyUpdate(completedRide, "Completed");
      }
    }
  }

  return currentOpenRide;
}

function buildCustomerRideVoiceMessage(rideRequest, status) {
  const rideStatus = status || rideRequest?.status;
  const driver = state.customerRideTracking?.driver;
  const driverName = [driver?.firstName, driver?.lastName]
    .map(value => String(value || "").trim())
    .filter(Boolean)
    .join(" ") || "tu conductor";
  if (rideStatus === "Accepted") return `Conductor asignado: ${driverName}.`;
  if (rideStatus === "DriverArrived") return "Ya llegué a recogida.";
  if (rideStatus === "InProgress") return "Viaje iniciado.";
  if (rideStatus === "Completed") return "Su viaje ha sido finalizado.";
  return "";
}

function announceCustomerJourneyUpdate(rideRequest, status) {
  const message = buildCustomerRideVoiceMessage(rideRequest, status);
  if (!message) return;
  const notificationKey = `${rideRequest?.id || "ride"}:${status}`;
  if (state.lastCustomerJourneyVoiceKey === notificationKey) return;
  state.lastCustomerJourneyVoiceKey = notificationKey;
  announceRideNotification(message, { forceVoice: true, forceSound: true });
}

function testCustomerVoiceAlerts() {
  state.driverAlertsUnlocked = true;
  const activeRide = getActiveCustomerRide();
  const message = activeRide
    ? buildCustomerRideVoiceMessage(activeRide, activeRide.status)
    : "Avisos de voz activados.";
  speakDriverAlert(message || "Avisos de voz activados.", true);
  showNotice("Aviso de voz activado. Mantén el volumen del teléfono encendido.");
}
function syncCustomerTrackingPolling(isCustomerMode) {
  window.clearInterval(state.customerTrackingPollingTimer);
  state.customerTrackingPollingTimer = null;
  if (!isCustomerMode || !getOpenCustomerRideRequest()) return;

  state.customerTrackingPollingTimer = window.setInterval(() => {
    void refreshCustomerRideStatus({ notifyJourneyChange: true }).catch(() => {
      // A future update can retry; the current map remains visible.
    });
  }, 15_000);
}

function renderCustomerRideMapFallback(container) {
  const points = getPendingRideMapPoints();
  const pointSummary = points.length
    ? `${points.map(point => point.kind === "pickup" ? "A" : "B").join(" y ")} guardado${points.length > 1 ? "s" : ""}.`
    : "Aún no hay puntos guardados.";
  container.innerHTML = `
    <div class="ride-map-unavailable">
      <strong>Mapa interactivo no disponible</strong>
      <span>${pointSummary} Conecta internet para tocar el mapa o usa tu ubicación actual.</span>
    </div>`;
  state.customerRideMap = { fallback: true };
}

function mountCustomerRideMap() {
  const picker = app.querySelector("[data-ride-location-picker]");
  const container = app.querySelector("[data-ride-location-map]");
  if (!picker || picker.hidden || !container) return;

  destroyCustomerRideMap();
  container.innerHTML = "";
  const points = getPendingRideMapPoints();
  const leaflet = window.L;
  if (!leaflet) {
    renderCustomerRideMapFallback(container);
    return;
  }

  try {
    const cityCode = app.querySelector("#ride-pricing-rule")?.value?.split("|")[0];
    const map = leaflet.map(container, {
      zoomControl: true,
      scrollWheelZoom: false,
      attributionControl: true
    });
    leaflet.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(map);

    const bounds = [];
    points.forEach(point => {
      const latLng = [point.latitude, point.longitude];
      bounds.push(latLng);
      leaflet.marker(latLng, {
        icon: createDriverMapIcon(leaflet, point.kind),
        keyboard: true,
        title: point.title
      }).addTo(map).bindTooltip(point.title, { direction: "top", offset: [0, -15] });
    });

    let routeLayerGroup = drawReferenceRoute(leaflet, map, points, { includeDriver: false });
    if (routeLayerGroup) {
      addMapRouteBadge(container, "A → B · calculando ruta real…");
      map.fitBounds(bounds, { padding: [36, 36], maxZoom: 16 });
    } else if (bounds.length === 1) {
      map.setView(bounds[0], 15);
    } else {
      map.setView(getRideMapFallbackCenter(cityCode), 13);
    }

    map.on("click", event => {
      setRideMapLocation(state.rideMapPickerTarget, {
        latitude: Number(event.latlng.lat.toFixed(6)),
        longitude: Number(event.latlng.lng.toFixed(6))
      });
    });
    const mapState = { map, routeLayerGroup };
    state.customerRideMap = mapState;
    if (points.length >= 2) {
      void getRoadRoute(points[0], points[1]).then(route => {
        if (!route || state.customerRideMap !== mapState) return;
        mapState.routeLayerGroup?.remove();
        mapState.routeLayerGroup = drawRoadRouteSegments(leaflet, map, [{
          route,
          color: "#111218",
          casingColor: "#ffd800"
        }]);
        replaceRouteBadge(container, `A → B · ruta real · ${formatDistance(route.distanceKilometers)} · ${route.estimatedDurationMinutes} min`);
      });
    }
    window.setTimeout(() => map.invalidateSize(), 0);
  } catch {
    renderCustomerRideMapFallback(container);
  }
}

function updateRideMapPickerUi() {
  const picker = app.querySelector("[data-ride-location-picker]");
  if (!picker) return;
  const target = state.rideMapPickerTarget === "destination" ? "destination" : "pickup";
  const label = target === "pickup" ? "origen (A)" : "destino (B)";
  picker.querySelector("[data-ride-map-instruction]").textContent = `Toca el mapa para ubicar el ${label}.`;
  picker.querySelectorAll("[data-select-ride-map-target]").forEach(button => {
    const isSelected = button.dataset.selectRideMapTarget === target;
    button.classList.toggle("is-selected", isSelected);
    button.setAttribute("aria-pressed", String(isSelected));
  });
}

function openRideMapPicker(target) {
  state.rideMapPickerTarget = target === "destination" ? "destination" : "pickup";
  const picker = app.querySelector("[data-ride-location-picker]");
  if (!picker) return;
  picker.hidden = false;
  updateRideMapPickerUi();
  mountCustomerRideMap();
  picker.scrollIntoView({ behavior: "smooth", block: "center" });
}

function closeRideMapPicker() {
  const picker = app.querySelector("[data-ride-location-picker]");
  if (picker) picker.hidden = true;
  destroyCustomerRideMap();
}

function selectRideMapPickerTarget(target) {
  state.rideMapPickerTarget = target === "destination" ? "destination" : "pickup";
  updateRideMapPickerUi();
}

function setRideMapLocation(target, location) {
  if (target === "destination") {
    state.pendingDestinationLocation = {
      destinationLatitude: location.latitude,
      destinationLongitude: location.longitude
    };
    const status = app.querySelector("#destination-location-status");
    if (status) status.textContent = "Punto B listo para esta solicitud.";
  } else {
    state.pendingPickupLocation = {
      pickupLatitude: location.latitude,
      pickupLongitude: location.longitude
    };
    const status = app.querySelector("#pickup-location-status");
    if (status) status.textContent = "Punto A listo para esta solicitud.";
    state.rideMapPickerTarget = "destination";
  }
  updateRideMapPickerUi();
  mountCustomerRideMap();
  void refreshCustomerRideQuote();
  showNotice(target === "destination" ? "Destino ubicado en el mapa." : "Origen ubicado. Ahora, si quieres, marca el destino.");
}

function createDriverMapIcon(leaflet, kind) {
  const glyphByKind = { driver: "🏍", pickup: "A", destination: "B" };
  return leaflet.divIcon({
    className: `hagale-map-pin hagale-map-pin-${kind}`,
    html: `<span class="hagale-map-pin-shape"><i aria-hidden="true">${glyphByKind[kind] || "•"}</i></span>`,
    iconSize: [42, 42],
    iconAnchor: [21, 21],
    tooltipAnchor: [0, -23]
  });
}

function renderFallbackDriverMap(container, points, fallbackCenter) {
  const center = fallbackCenter || [0, 0];
  const allPoints = points.length ? points : [{ latitude: center[0], longitude: center[1], kind: "zone", title: "Zona de despacho" }];
  const latitudes = allPoints.map(point => point.latitude);
  const longitudes = allPoints.map(point => point.longitude);
  const minLatitude = Math.min(...latitudes, center[0]) - .004;
  const maxLatitude = Math.max(...latitudes, center[0]) + .004;
  const minLongitude = Math.min(...longitudes, center[1]) - .004;
  const maxLongitude = Math.max(...longitudes, center[1]) + .004;
  const project = point => {
    const x = 24 + ((point.longitude - minLongitude) / Math.max(maxLongitude - minLongitude, .0001)) * 552;
    const y = 276 - ((point.latitude - minLatitude) / Math.max(maxLatitude - minLatitude, .0001)) * 252;
    return { x: Math.max(18, Math.min(582, x)), y: Math.max(18, Math.min(282, y)) };
  };
  const markerColor = { driver: "#111218", pickup: "#ffd800", destination: "#111218", zone: "#d8d8d0" };
  const markerTextColor = { driver: "#fff", pickup: "#111218", destination: "#fff", zone: "#555" };
  const markers = allPoints.map(point => {
    const projected = project(point);
    const color = markerColor[point.kind] || markerColor.zone;
    const textColor = markerTextColor[point.kind] || markerTextColor.zone;
    const glyph = point.kind === "driver" ? "🏍" : point.kind === "pickup" ? "A" : point.kind === "destination" ? "B" : "·";
    return `<g transform="translate(${projected.x} ${projected.y})"><circle r="15" fill="${color}" stroke="#fff" stroke-width="3"/><text y="5" fill="${textColor}" font-size="12" font-family="sans-serif" font-weight="800" text-anchor="middle">${glyph}</text></g>`;
  }).join("");
  container.innerHTML = `
    <svg class="driver-fallback-map" viewBox="0 0 600 300" role="img" aria-label="Mapa de despacho sin conexión de mapas">
      <rect width="600" height="300" fill="#e8e8e1"/>
      <path d="M-20 248 L620 66 M-30 120 L630 210 M75 -20 L165 320 M330 -20 L385 320 M520 -20 L445 320" stroke="#fff" stroke-width="18" opacity=".9"/>
      <path d="M-20 248 L620 66 M-30 120 L630 210 M75 -20 L165 320 M330 -20 L385 320 M520 -20 L445 320" stroke="#c5c7bf" stroke-width="2"/>
      <path d="M0 28 H600 M0 278 H600" stroke="#d2d3cb" stroke-width="1" stroke-dasharray="7 9"/>
      ${markers}
    </svg>
    <div class="driver-map-offline-badge"><strong>Mapa local</strong><span>Conecta internet para ver calles detalladas</span></div>`;
  state.driverMap = { fallback: true };
}

function mountDriverMap() {
  const container = app.querySelector("[data-driver-map]");
  const points = getDriverMapPoints();
  const fallbackCenter = getDriverMapFallbackCenter();
  if (!container || (!points.length && !fallbackCenter)) return;

  const leaflet = window.L;
  if (!leaflet) {
    renderFallbackDriverMap(container, points, fallbackCenter);
    return;
  }

  try {
    container.querySelector(".driver-map-empty")?.remove();
    const map = leaflet.map(container, {
      zoomControl: true,
      scrollWheelZoom: false,
      attributionControl: true
    });
    leaflet.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(map);

    const mappedRequest = state.driverCurrentRideRequest
      || state.driverRideOffers.find(offer => offer.id === state.selectedDriverOfferId);
    const routePoints = mappedRequest
      ? points.filter(point => ["driver", "pickup", "destination"].includes(point.kind))
      : [];
    let routeLayerGroup = drawReferenceRoute(leaflet, map, routePoints, { includeDriver: true });
    if (routeLayerGroup) {
      addMapRouteBadge(container, "Calculando ruta real por calles…");
    }

    let driverMarker = null;
    const bounds = [];
    points.forEach(point => {
      const latLng = [point.latitude, point.longitude];
      bounds.push(latLng);
      const marker = leaflet.marker(latLng, {
        icon: createDriverMapIcon(leaflet, point.kind),
        keyboard: true,
        title: point.title
      }).addTo(map);
      marker.bindTooltip(escapeHtml(point.title), { direction: "top", offset: [0, -15] });
      if (point.kind === "driver") driverMarker = marker;
    });

    if (bounds.length === 1) {
      map.setView(bounds[0], 16);
    } else if (bounds.length > 1) {
      map.fitBounds(bounds, { padding: [36, 36], maxZoom: 16 });
    } else {
      map.setView(fallbackCenter, 13);
      const overlay = document.createElement("div");
      overlay.className = "driver-map-overlay";
      overlay.innerHTML = '<strong>Zona de despacho</strong><span>Centro aproximado · activa el GPS para mostrar tu moto</span>';
      container.appendChild(overlay);
    }

    const mapState = { map, driverMarker, routeLayerGroup, routePoints, roadRouteActive: false };
    state.driverMap = mapState;
    if (mappedRequest) {
      const driverPoint = routePoints.find(point => point.kind === "driver");
      const pickupPoint = routePoints.find(point => point.kind === "pickup");
      const destinationPoint = routePoints.find(point => point.kind === "destination");
      const segments = [];
      if (driverPoint && pickupPoint) {
        segments.push(getRoadRoute(driverPoint, pickupPoint).then(route => ({
          route,
          color: "#ffd800",
          casingColor: "#111218"
        })));
      }
      if (pickupPoint && destinationPoint) {
        segments.push(getRoadRoute(pickupPoint, destinationPoint).then(route => ({
          route,
          color: "#111218",
          casingColor: "#ffd800"
        })));
      }
      if (segments.length) {
        void Promise.all(segments).then(routeSegments => {
          if (state.driverMap !== mapState) return;
          const usableSegments = routeSegments.filter(segment => segment.route);
          if (!usableSegments.length) return;
          mapState.routeLayerGroup?.remove();
          mapState.routeLayerGroup = drawRoadRouteSegments(leaflet, map, usableSegments);
          mapState.roadRouteActive = true;
          const totalMinutes = usableSegments.reduce((sum, segment) => sum + Number(segment.route.estimatedDurationMinutes || 0), 0);
          replaceRouteBadge(container, `Ruta real · ${totalMinutes} min estimados · usa navegación para giros`);
        });
      }
    }
    window.setTimeout(() => map.invalidateSize(), 0);
  } catch {
    renderFallbackDriverMap(container, points, fallbackCenter);
  }
}

function updateDriverMapPosition(location) {
  const mapState = state.driverMap;
  if (!mapState?.map || !mapState.driverMarker) {
    if (app.querySelector("[data-driver-map]")) renderDashboard();
    return;
  }

  const latLng = [location.latitude, location.longitude];
  mapState.driverMarker.setLatLng(latLng);
  if (Array.isArray(mapState.routePoints)) {
    const updatedRoutePoints = mapState.routePoints.map(point => point.kind === "driver"
      ? { ...point, latitude: location.latitude, longitude: location.longitude }
      : point);
    mapState.routePoints = updatedRoutePoints;
    if (mapState.routeLayerGroup && !mapState.roadRouteActive) {
      mapState.routeLayerGroup.remove();
      mapState.routeLayerGroup = drawReferenceRoute(window.L, mapState.map, updatedRoutePoints, { includeDriver: true });
    } else if (mapState.roadRouteActive) {
      void refreshDriverRoadRoute(mapState);
    }
  }
  mapState.map.panTo(latLng, { animate: true, duration: .6 });
}

async function refreshDriverRoadRoute(mapState) {
  if (!mapState?.map || !Array.isArray(mapState.routePoints)) return;
  const now = Date.now();
  if (now - Number(mapState.lastRoadRouteRefreshAt || 0) < 15_000) return;
  const driverPoint = mapState.routePoints.find(point => point.kind === "driver");
  const pickupPoint = mapState.routePoints.find(point => point.kind === "pickup");
  const destinationPoint = mapState.routePoints.find(point => point.kind === "destination");
  if (!driverPoint || !pickupPoint) return;

  mapState.lastRoadRouteRefreshAt = now;
  const segments = await Promise.all([
    getRoadRoute(driverPoint, pickupPoint).then(route => ({ route, color: "#ffd800", casingColor: "#111218" })),
    destinationPoint
      ? getRoadRoute(pickupPoint, destinationPoint).then(route => ({ route, color: "#111218", casingColor: "#ffd800" }))
      : Promise.resolve(null)
  ]);
  if (state.driverMap !== mapState) return;
  const usableSegments = segments.filter(segment => segment?.route);
  if (!usableSegments.length) return;
  mapState.routeLayerGroup?.remove();
  mapState.routeLayerGroup = drawRoadRouteSegments(window.L, mapState.map, usableSegments);
}

function stopDriverLocationTracking() {
  if (state.driverLocationWatchId !== null) {
    navigator.geolocation?.clearWatch(state.driverLocationWatchId);
  }
  state.driverLocationWatchId = null;
  state.lastDriverLocationSentAt = 0;
  state.isSendingDriverLocation = false;
  state.locationTrackingErrorShown = false;
  state.driverLocationUsesHighAccuracy = true;
}

function applyLocalDriverLocation(location) {
  state.application = {
    ...state.application,
    lastKnownLatitude: location.latitude,
    lastKnownLongitude: location.longitude,
    locationUpdatedAtUtc: new Date().toISOString()
  };
  updateDriverMapPosition(location);
}

async function saveDriverLocation(location) {
  state.application = await request("/driver-application/location", {
    method: "PATCH",
    data: location
  });
}

function startDriverLocationTracking({ highAccuracy = true } = {}) {
  if (!navigator.geolocation) {
    showNotice("Este navegador no ofrece GPS. Abre HÁGALE en Chrome, Firefox o Safari y permite la ubicación.", true);
    return;
  }
  if (state.driverLocationWatchId !== null) return;

  state.locationTrackingErrorShown = false;
  state.driverLocationUsesHighAccuracy = highAccuracy;
  state.driverLocationWatchId = navigator.geolocation.watchPosition(
    async position => {
      if (!["Available", "Busy"].includes(state.application?.availabilityStatus)) {
        stopDriverLocationTracking();
        return;
      }

      const location = {
        latitude: Number(position.coords.latitude.toFixed(6)),
        longitude: Number(position.coords.longitude.toFixed(6))
      };
      state.locationTrackingErrorShown = false;
      applyLocalDriverLocation(location);

      const now = Date.now();
      if (state.isSendingDriverLocation || now - state.lastDriverLocationSentAt < 15_000) return;

      state.lastDriverLocationSentAt = now;
      state.isSendingDriverLocation = true;
      try {
        await saveDriverLocation(location);
      } catch {
        // Keep the local marker moving and retry on the next allowed GPS update.
      } finally {
        state.isSendingDriverLocation = false;
      }
    },
    error => {
      if (error.code === 3 && state.driverLocationUsesHighAccuracy) {
        navigator.geolocation.clearWatch(state.driverLocationWatchId);
        state.driverLocationWatchId = null;
        showNotice("El GPS preciso tardó demasiado. Buscando una ubicación aproximada…");
        startDriverLocationTracking({ highAccuracy: false });
        return;
      }
      if (state.locationTrackingErrorShown) return;
      state.locationTrackingErrorShown = true;
      if (error.code === 1) stopDriverLocationTracking();
      showNotice(getDeviceLocationErrorMessage(error), true);
    },
    { enableHighAccuracy: highAccuracy, maximumAge: highAccuracy ? 5_000 : 30_000, timeout: highAccuracy ? 15_000 : 20_000 }
  );
}

function renderJourneyTimeline(rideRequest) {
  if (rideRequest.status === "CounterOfferPending") {
    return `<p class="journey-counter-offer">Contraoferta de ${formatCop(rideRequest.counterOfferPriceCop)}${rideRequest.counterOfferAtUtc ? ` · ${formatDateTime(rideRequest.counterOfferAtUtc)}` : ""}. Espera la decisión del pasajero.</p>`;
  }

  if (rideRequest.status === "Cancelled") {
    return `<p class="journey-cancelled">Solicitud cancelada${rideRequest.cancelledAtUtc ? ` el ${formatDateTime(rideRequest.cancelledAtUtc)}` : ""}.</p>`;
  }

  const steps = [
    { status: "Pending", title: "Solicitud registrada", at: rideRequest.requestedAtUtc },
    { status: "Accepted", title: "Conductor asignado", at: rideRequest.acceptedAtUtc },
    { status: "DriverEnRoute", title: "Conductor en camino", at: rideRequest.driverEnRouteAtUtc },
    { status: "DriverArrived", title: "Conductor llegó a recogida", at: rideRequest.driverArrivedAtUtc },
    { status: "InProgress", title: "Viaje iniciado", at: rideRequest.startedAtUtc },
    { status: "Completed", title: "Viaje finalizado", at: rideRequest.completedAtUtc }
  ];
  const currentStepIndex = steps.findIndex(step => step.status === rideRequest.status);

  return `
    <ol class="journey-timeline" aria-label="Progreso del servicio">
      ${steps.map((step, index) => {
        const stateClass = index < currentStepIndex || step.at ? "complete" : index === currentStepIndex ? "current" : "upcoming";
        return `<li class="${stateClass}"><span class="journey-marker" aria-hidden="true"></span><div><strong>${step.title}</strong><small>${step.at ? formatDateTime(step.at) : "Pendiente"}</small></div></li>`;
      }).join("")}
    </ol>`;
}

function showNotice(message, isError = false, isAlert = false) {
  notice.textContent = message;
  notice.className = `notice show${isError ? " error" : ""}${isAlert ? " alert" : ""}`;
  window.clearTimeout(showNotice.timeout);
  showNotice.timeout = window.setTimeout(() => {
    notice.className = "notice";
  }, 4600);
}

async function request(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (state.token) {
    headers.set("Authorization", `Bearer ${state.token}`);
  }

  let body;
  if (options.data !== undefined) {
    headers.set("Content-Type", "application/json");
    body = JSON.stringify(options.data);
  } else if (options.form) {
    body = options.form;
  }

  const response = await fetch(`${apiRoot}${path}`, {
    method: options.method || "GET",
    headers,
    body
  });

  const contentType = response.headers.get("content-type") || "";
  const payload = contentType.includes("json") ? await response.json() : null;
  if (!response.ok) {
    if (response.status === 401 && state.token) {
      signOut(false);
    }
    const modelErrors = payload?.errors
      ? Object.values(payload.errors).flat().join(" ")
      : null;
    const detail = modelErrors || payload?.detail || payload?.title;
    const error = new Error(detail || `No fue posible completar la operación (${options.method || "GET"} ${path}: ${response.status}).`);
    error.statusCode = response.status;
    throw error;
  }

  return payload;
}

async function openAdminDocument(driverProfileId, documentId) {
  if (!state.token) {
    showNotice("Inicia sesión como administrador para abrir documentos.", true);
    return;
  }

  try {
    const response = await fetch(`${apiRoot}/admin/driver-applications/${driverProfileId}/documents/${documentId}/file`, {
      headers: { Authorization: `Bearer ${state.token}` }
    });
    if (!response.ok) {
      if (response.status === 401 && state.token) {
        signOut(false);
      }

      const contentType = response.headers.get("content-type") || "";
      const payload = contentType.includes("json") ? await response.json() : null;
      throw new Error(payload?.detail || payload?.title || "No fue posible abrir el documento.");
    }

    const blob = await response.blob();
    const documentUrl = URL.createObjectURL(blob);
    const openedWindow = window.open(documentUrl, "_blank", "noopener,noreferrer");
    if (!openedWindow) {
      showNotice("El navegador bloqueó la vista del documento. Permite ventanas emergentes para HÁGALE.", true);
    }
    window.setTimeout(() => URL.revokeObjectURL(documentUrl), 120_000);
  } catch (error) {
    showNotice(error.message || "No fue posible abrir el documento.", true);
  }
}

function getAccessTokenRoles() {
  const payloadPart = state.token?.split(".")[1];
  if (!payloadPart) return [];

  try {
    const base64 = payloadPart.replaceAll("-", "+").replaceAll("_", "/");
    const paddedBase64 = base64.padEnd(Math.ceil(base64.length / 4) * 4, "=");
    const bytes = Uint8Array.from(atob(paddedBase64), character => character.charCodeAt(0));
    const payload = JSON.parse(new TextDecoder().decode(bytes));
    const roleClaimNames = [
      "role",
      "roles",
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    ];
    return [...new Set(roleClaimNames.flatMap(name => {
      const value = payload[name];
      return Array.isArray(value) ? value : value ? [value] : [];
    }).map(role => String(role)))];
  } catch {
    return [];
  }
}

function accessTokenMatchesProfileRoles(roles) {
  const tokenRoles = getAccessTokenRoles();
  const profileRoles = [...new Set((roles || []).map(role => String(role)))];
  return tokenRoles.length === profileRoles.length
    && profileRoles.every(role => tokenRoles.includes(role));
}

async function renewCurrentSession() {
  const result = await request("/auth/renew-session", { method: "POST", data: {} });
  if (!result?.accessToken) {
    throw new Error("No fue posible actualizar el acceso de la sesión.");
  }

  state.token = result.accessToken;
  sessionStorage.setItem(sessionKey, state.token);
  return result;
}

function renderWelcome() {
  stopRideRealtime();
  destroyCustomerRideMap();
  destroyCustomerTrackingMap();
  window.clearInterval(state.customerTrackingPollingTimer);
  state.customerTrackingPollingTimer = null;
  applyVisualMode();
  document.body.classList.remove("driver-mode");
  document.body.classList.remove("customer-mode");
  document.body.classList.add("welcome-view");
  app.innerHTML = `
    <section class="welcome-shell">
      <section class="welcome-stage" aria-labelledby="welcome-title">
        <div class="welcome-copy" data-reveal>
          <p class="welcome-brand" aria-label="HÁGALE"><img class="hagale-logo-image welcome-logo-image" src="/assets/hagale-logo-yellow.png" alt="HÁGALE"></p>
          <h1 id="welcome-title">Tu moto,<br>tu precio</h1>
          <p class="welcome-slogan">Conecta rápido y seguro.</p>
        </div>
        <figure class="welcome-rider" data-reveal>
          <img src="/assets/hagale-moto-original.png" alt="Motociclista HÁGALE con casco amarillo sobre una moto negra y amarilla">
        </figure>
        <div class="welcome-actions" data-reveal>
          <button class="button button-primary" type="button" data-open-auth="login">Ingresar</button>
          <button class="button welcome-register-button" type="button" data-open-auth="register">Regístrate</button>
          <button class="button button-secondary welcome-guest-button" type="button" data-demo-guest>Entrar como visitante</button>
          ${renderInstallAppButton("welcome-install-button")}
        </div>
      </section>

      <section class="auth-panel" id="auth" hidden aria-label="Acceso HÁGALE">
        <article class="card welcome-auth-card" data-reveal>
          <button class="button button-quiet auth-back-button" type="button" data-close-auth>Volver</button>
          <p class="welcome-brand auth-brand" aria-label="HÁGALE"><img class="hagale-logo-image welcome-logo-image" src="/assets/hagale-logo-yellow.png" alt="HÁGALE"></p>
          <div class="auth-tablist" role="tablist" aria-label="Acceso">
            <button class="tab" role="tab" aria-selected="true" type="button" data-auth-mode="register">Crear cuenta</button>
            <button class="tab" role="tab" aria-selected="false" type="button" data-auth-mode="login">Ingresar</button>
          </div>
          <div id="auth-form"></div>
        </article>
      </section>
    </section>`;

  setAuthForm("login");
  app.querySelectorAll("[data-open-auth]").forEach(button => {
    button.addEventListener("click", () => {
      setAuthForm(button.dataset.openAuth);
      const panel = document.querySelector("#auth");
      if (panel) {
        panel.hidden = false;
        document.body.classList.add("auth-panel-open");
        panel.querySelectorAll("[data-reveal]").forEach(element => element.classList.add("is-revealed"));
        panel.querySelector("input")?.focus();
      }
    });
  });
  app.querySelector("[data-close-auth]")?.addEventListener("click", () => {
    const panel = document.querySelector("#auth");
    if (panel) panel.hidden = true;
    document.body.classList.remove("auth-panel-open");
  });
  app.querySelectorAll("[data-auth-mode]").forEach(button => {
    button.addEventListener("click", () => setAuthForm(button.dataset.authMode));
  });
  bindInstallAppEvents();
  bindGuestAccessButtons();
  activateRevealAnimations();
}

function renderGoogleAuthSection(mode) {
  const actionText = mode === "register" ? "Crear cuenta con Google" : "Continuar con Google";
  return `
    <section class="external-auth-panel" data-google-auth-section data-google-auth-mode="${escapeHtml(mode)}">
      <div class="auth-divider"><span>o</span></div>
      <div class="google-auth-container" data-google-auth-container>
        <button class="button google-auth-button" type="button" data-google-auth-fallback>
          <span class="google-auth-mark" aria-hidden="true">${renderGoogleMark()}</span>
          <span>${actionText}</span>
        </button>
      </div>
      <p class="google-auth-help">Si Google muestra “acceso bloqueado”, agrega tu Gmail como usuario de prueba en Google Cloud → Google Auth Platform → Público.</p>
    </section>`;
}

function renderGoogleMark() {
  return `
    <svg class="google-auth-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.58c2.09-1.93 3.27-4.78 3.27-8.09z"></path>
      <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.58-2.77c-.98.66-2.23 1.06-3.7 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"></path>
      <path fill="#FBBC05" d="M5.84 14.1c-.22-.66-.35-1.36-.35-2.1s.13-1.44.35-2.1V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l3.66-2.84z"></path>
      <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06L5.84 9.9C6.71 7.3 9.14 5.38 12 5.38z"></path>
    </svg>`;
}

async function getGoogleAuthStatus(force = false) {
  if (state.googleAuthStatus && !force) return state.googleAuthStatus;
  try {
    state.googleAuthStatus = await request("/auth/google/status");
  } catch {
    state.googleAuthStatus = { provider: "Google", isConfigured: false, clientId: null };
  }

  return state.googleAuthStatus;
}

function loadGoogleIdentityScript() {
  if (window.google?.accounts?.id) return Promise.resolve();
  if (googleIdentityScriptPromise) return googleIdentityScriptPromise;

  googleIdentityScriptPromise = new Promise((resolve, reject) => {
    const existingScript = document.querySelector('script[src="https://accounts.google.com/gsi/client"]');
    if (existingScript) {
      existingScript.addEventListener("load", resolve, { once: true });
      existingScript.addEventListener("error", reject, { once: true });
      return;
    }

    const script = document.createElement("script");
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.defer = true;
    script.onload = resolve;
    script.onerror = reject;
    document.head.appendChild(script);
  });

  return googleIdentityScriptPromise;
}

async function hydrateGoogleAuthSection(mode) {
  const section = document.querySelector(`[data-google-auth-section][data-google-auth-mode="${mode}"]`);
  if (!section) return;

  const container = section.querySelector("[data-google-auth-container]");
  if (!container) return;

  const status = await getGoogleAuthStatus();
  if (!section.isConnected || section.dataset.googleAuthMode !== mode) return;

  if (!status?.isConfigured || !status.clientId) {
    section.classList.add("is-sample");
    const sampleButton = container.querySelector("[data-google-auth-fallback]");
    sampleButton?.setAttribute("aria-disabled", "true");
    if (sampleButton) sampleButton.title = "Ingreso con Google disponible próximamente";
    return;
  }

  try {
    await loadGoogleIdentityScript();
    if (!window.google?.accounts?.id) {
      throw new Error("Google Identity Services no está disponible.");
    }

    if (googleIdentityInitializedClientId !== status.clientId) {
      window.google.accounts.id.initialize({
        client_id: status.clientId,
        callback: handleGoogleCredentialResponse,
        ux_mode: "popup",
        auto_select: false
      });
      googleIdentityInitializedClientId = status.clientId;
    }

    container.innerHTML = '<div class="google-official-button" data-google-button-slot></div>';
    const buttonSlot = container.querySelector("[data-google-button-slot]");
    window.google.accounts.id.renderButton(buttonSlot, {
      type: "standard",
      theme: "filled_black",
      size: "large",
      shape: "pill",
      text: mode === "register" ? "signup_with" : "signin_with",
      logo_alignment: "left",
      width: 320
    });
    section.classList.remove("is-sample");
  } catch {
    section.classList.add("is-sample");
    const sampleButton = container.querySelector("[data-google-auth-fallback]");
    sampleButton?.setAttribute("aria-disabled", "true");
    if (sampleButton) sampleButton.title = "Ingreso con Google disponible próximamente";
  }
}

function bindGoogleAuthSection(mode) {
  const fallback = document.querySelector(`[data-google-auth-section][data-google-auth-mode="${mode}"] [data-google-auth-fallback]`);
  if (fallback) {
    fallback.addEventListener("click", async () => {
      const status = await getGoogleAuthStatus(true);
      if (!status?.isConfigured) {
        showNotice("Para activar Google falta pegar el Client ID real en Render: Authentication__Google__ClientId.");
        return;
      }

      await hydrateGoogleAuthSection(mode);
    });
  }

  void hydrateGoogleAuthSection(mode);
}

async function handleGoogleCredentialResponse(response) {
  if (!response?.credential) {
    showNotice("Google no devolvió una credencial válida.", true);
    return;
  }

  await authenticate("/auth/google", { credential: response.credential });
}

function renderPasswordIcon(isVisible = false) {
  return `
    <svg class="password-eye-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z"></path>
      <circle cx="12" cy="12" r="2.7"></circle>
      ${isVisible ? '<path class="password-eye-slash" d="M4 4l16 16"></path>' : ""}
    </svg>`;
}

function bindAuthUtilities() {
  document.querySelectorAll("[data-toggle-password]").forEach(button => {
    button.addEventListener("click", () => {
      const target = button.dataset.togglePassword;
      const input = target ? document.querySelector(target) : null;
      if (!input) return;

      const willShow = input.type === "password";
      input.type = willShow ? "text" : "password";
      button.setAttribute("aria-label", willShow ? "Ocultar contraseña" : "Mostrar contraseña");
      button.setAttribute("aria-pressed", String(willShow));
      button.title = willShow ? "Ocultar contraseña" : "Mostrar contraseña";
      button.innerHTML = renderPasswordIcon(willShow);
    });
  });

  document.querySelectorAll("[data-forgot-password]").forEach(button => {
    button.addEventListener("click", () => {
      const emailTarget = button.dataset.emailTarget;
      const emailInput = emailTarget ? document.querySelector(emailTarget) : null;
      setAuthForm("forgot", { email: emailInput?.value || "" });
    });
  });
}

function bindGuestAccessButtons() {
  document.querySelectorAll("[data-demo-guest]").forEach(button => {
    if (button.dataset.guestBound === "true") return;
    button.dataset.guestBound = "true";
    button.addEventListener("click", signInAsDemoGuest);
  });
}

function setAuthForm(mode, options = {}) {
  app.querySelectorAll("[data-auth-mode]").forEach(button => {
    button.setAttribute("aria-selected", String(button.dataset.authMode === mode));
  });

  const container = document.querySelector("#auth-form");
  if (mode === "forgot") {
    container.innerHTML = `
      <h2>Recuperar acceso</h2>
      <p class="muted small-text">Modo demo: actualiza la contraseña de una cuenta existente. En producción esto enviará un enlace seguro por correo.</p>
      <form id="password-recovery-form">
        <div class="field"><label for="recovery-email">Correo</label><input id="recovery-email" name="email" type="email" autocomplete="email" value="${escapeHtml(options.email || "")}" required></div>
        <div class="field">
          <label for="recovery-password">Nueva contraseña</label>
          <div class="password-control">
            <input id="recovery-password" name="password" type="password" autocomplete="new-password" minlength="12" required>
            <button class="password-toggle" type="button" data-toggle-password="#recovery-password" aria-label="Mostrar contraseña" aria-pressed="false" title="Mostrar contraseña">${renderPasswordIcon()}</button>
          </div>
        </div>
        <div class="field">
          <label for="recovery-password-confirm">Confirmar contraseña</label>
          <div class="password-control">
            <input id="recovery-password-confirm" name="passwordConfirm" type="password" autocomplete="new-password" minlength="12" required>
            <button class="password-toggle" type="button" data-toggle-password="#recovery-password-confirm" aria-label="Mostrar contraseña" aria-pressed="false" title="Mostrar contraseña">${renderPasswordIcon()}</button>
          </div>
        </div>
        <div class="button-row">
          <button class="button button-primary" type="submit">Actualizar contraseña</button>
          <button class="button button-secondary" type="button" data-auth-mode="login">Volver a ingresar</button>
        </div>
    </form>`;
    document.querySelector("#password-recovery-form").addEventListener("submit", handlePasswordRecovery);
    container.querySelector("[data-auth-mode='login']")?.addEventListener("click", () => setAuthForm("login"));
    bindAuthUtilities();
    const recoveryEmail = document.querySelector("#recovery-email");
    if (recoveryEmail && !recoveryEmail.value.trim()) recoveryEmail.focus();
    return;
  }

  if (mode === "login") {
    container.innerHTML = `
      <h2>Bienvenido de nuevo</h2>
      <p class="muted small-text">Ingresa con tu cuenta o prueba la plataforma como visitante.</p>
      ${renderGoogleAuthSection("login")}
      <button class="button button-secondary demo-guest-inline" type="button" data-demo-guest>Entrar como visitante</button>
      <form id="login-form">
        <div class="field"><label for="login-email">Correo</label><input id="login-email" name="email" type="email" autocomplete="email" required></div>
        <div class="field">
          <label for="login-password">Contraseña</label>
          <div class="password-control">
            <input id="login-password" name="password" type="password" autocomplete="current-password" required>
            <button class="password-toggle" type="button" data-toggle-password="#login-password" aria-label="Mostrar contraseña" aria-pressed="false" title="Mostrar contraseña">${renderPasswordIcon()}</button>
          </div>
          <button class="forgot-password-link" type="button" data-forgot-password data-email-target="#login-email">¿Olvidaste tu contraseña?</button>
        </div>
        <button class="button button-primary" type="submit">Ingresar</button>
      </form>`;
    document.querySelector("#login-form").addEventListener("submit", handleLogin);
    bindAuthUtilities();
    bindGuestAccessButtons();
    bindGoogleAuthSection("login");
    return;
  }

  container.innerHTML = `
    <h2>Crea tu cuenta</h2>
    <p class="muted small-text">La contraseña debe tener mínimo 12 caracteres, mayúscula, minúscula, número y símbolo.</p>
    ${renderGoogleAuthSection("register")}
    <form id="register-form">
      <div class="form-grid">
        <div class="field"><label for="first-name">Nombre</label><input id="first-name" name="firstName" autocomplete="given-name" minlength="2" required></div>
        <div class="field"><label for="last-name">Apellido</label><input id="last-name" name="lastName" autocomplete="family-name" minlength="2" required></div>
      </div>
      <div class="field"><label for="email">Correo</label><input id="email" name="email" type="email" autocomplete="email" required></div>
      <div class="field"><label for="phone">Celular</label><input id="phone" name="phoneNumber" inputmode="tel" placeholder="+573001234567" pattern="\\+[1-9]\\d{7,14}" required></div>
      <div class="field">
        <label for="password">Contraseña</label>
        <div class="password-control">
          <input id="password" name="password" type="password" autocomplete="new-password" minlength="12" required>
          <button class="password-toggle" type="button" data-toggle-password="#password" aria-label="Mostrar contraseña" aria-pressed="false" title="Mostrar contraseña">${renderPasswordIcon()}</button>
        </div>
      </div>
      <button class="button button-primary" type="submit">Crear mi cuenta</button>
    </form>`;
  document.querySelector("#register-form").addEventListener("submit", handleRegister);
  bindAuthUtilities();
  bindGuestAccessButtons();
  bindGoogleAuthSection("register");
}

async function handleLogin(event) {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  await authenticate("/auth/login", { email: form.get("email"), password: form.get("password") });
}

async function signInAsDemoGuest(event) {
  event?.preventDefault();
  const button = event?.currentTarget;
  if (button) button.disabled = true;
  try {
    await authenticate("/auth/demo-guest", {});
  } finally {
    if (button?.isConnected) button.disabled = false;
  }
}

function getPasswordValidationErrors(password) {
  const errors = [];
  if (password.length < 12) errors.push("mínimo 12 caracteres");
  if (!/[A-ZÁÉÍÓÚÜÑ]/.test(password)) errors.push("una mayúscula");
  if (!/[a-záéíóúüñ]/.test(password)) errors.push("una minúscula");
  if (!/\d/.test(password)) errors.push("un número");
  if (!/[^\p{L}\p{N}\s]/u.test(password)) errors.push("un símbolo");
  return errors;
}

async function handleRegister(event) {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const password = String(form.get("password") || "");
  const passwordErrors = getPasswordValidationErrors(password);

  if (passwordErrors.length > 0) {
    showNotice(`La contraseña necesita: ${passwordErrors.join(", ")}.`, true);
    document.querySelector("#password")?.focus();
    return;
  }

  await authenticate("/auth/register", Object.fromEntries(form));
}

async function handlePasswordRecovery(event) {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const password = String(form.get("password") || "");
  const passwordConfirm = String(form.get("passwordConfirm") || "");
  const passwordErrors = getPasswordValidationErrors(password);

  if (passwordErrors.length > 0) {
    showNotice(`La nueva contraseña necesita: ${passwordErrors.join(", ")}.`, true);
    document.querySelector("#recovery-password")?.focus();
    return;
  }

  if (password !== passwordConfirm) {
    showNotice("Las contraseñas no coinciden.", true);
    document.querySelector("#recovery-password-confirm")?.focus();
    return;
  }

  await authenticate("/auth/demo-reset-password", {
    email: form.get("email"),
    password
  });
}

async function authenticate(path, data) {
  try {
    const result = await request(path, { method: "POST", data });
    state.token = result.accessToken;
    sessionStorage.setItem(sessionKey, state.token);
    showNotice(path.endsWith("register")
      ? "Cuenta creada. Bienvenido a HÁGALE."
      : path.endsWith("google")
        ? "Sesión iniciada con Google."
        : path.endsWith("demo-reset-password")
          ? "Contraseña actualizada. Sesión iniciada."
          : path.endsWith("demo-guest")
            ? "Entraste como visitante para conocer la plataforma."
            : "Sesión iniciada.");
    await loadDashboard();
  } catch (error) {
    showNotice(error.message, true);
  }
}

async function loadDashboard({ allowRoleSessionRenewal = true } = {}) {
  try {
    state.profile = await request("/profile/me");
    // El perfil consulta los roles vigentes. Si administración acaba de
    // aprobar una cuenta, el JWT previo aún no los contiene; se actualiza una
    // sola vez antes de llamar a los endpoints protegidos por rol.
    if (allowRoleSessionRenewal && !accessTokenMatchesProfileRoles(state.profile.roles)) {
      await renewCurrentSession();
      return loadDashboard({ allowRoleSessionRenewal: false });
    }
    if (!state.profile.roles.includes("Driver")) {
      state.activeMode = "Customer";
      sessionStorage.removeItem(modeKey);
    }

    const applicationPromise = request("/driver-application/me")
      .then(application => ({ application, error: null }))
      .catch(error => ({ application: null, error }));
    const rideRequestsPromise = state.profile.roles.includes("Customer")
      ? request("/ride-requests/me")
      : Promise.resolve([]);
    const [{ application, error: applicationError }, rideRequests] = await Promise.all([
      applicationPromise,
      rideRequestsPromise
    ]);
    if (applicationError && !/Solicitud no encontrada|no tiene una solicitud/i.test(applicationError.message)) {
      throw applicationError;
    }
    state.application = application;
    state.rideRequests = rideRequests;
    if (state.profile.roles.includes("Customer")) {
      await refreshCustomerRideTracking();
    } else {
      state.customerRideTracking = null;
    }
    if (state.profile.roles.includes("Driver")) {
      const [driverRideOffers, driverCurrentRideRequest, driverActivitySummary, driverCompletedRideRequests] = await Promise.all([
        request(getDispatchOffersUrl()),
        request("/driver/ride-requests/current"),
        request("/driver/ride-requests/activity-summary"),
        request("/driver/ride-requests/completed")
      ]);
      state.driverRideOffers = driverRideOffers;
      state.driverCurrentRideRequest = driverCurrentRideRequest;
      state.driverActivitySummary = driverActivitySummary;
      state.driverCompletedRideRequests = driverCompletedRideRequests;
    } else {
      state.driverRideOffers = [];
      state.driverCurrentRideRequest = null;
      state.driverCompletedRideRequests = [];
      state.driverActivitySummary = null;
    }
    const emergencyContactsPromise = request("/safety/emergency-contacts/me");
    const pricingRulesPromise = state.profile.roles.includes("Administrator")
      ? request("/admin/pricing-rules")
      : state.profile.roles.includes("Customer")
        ? request("/pricing/rules")
        : Promise.resolve([]);
    const platformAppearancePromise = request("/platform-appearance").catch(() => getDefaultPlatformAppearance());
    const emergencyServiceChannelsPromise = state.profile.roles.includes("Administrator")
      ? request("/admin/safety/emergency-channels")
      : Promise.resolve([]);
    const adminApplicationsPromise = state.profile.roles.includes("Administrator")
      ? loadAdminApplications().then(() => state.adminApplications)
      : Promise.resolve(null);
    [state.emergencyContacts, state.pricingRules, state.emergencyServiceChannels, state.adminApplications, state.platformAppearance] = await Promise.all([
      emergencyContactsPromise,
      pricingRulesPromise,
      emergencyServiceChannelsPromise,
      adminApplicationsPromise,
      platformAppearancePromise
    ]);
    if (!sessionStorage.getItem(accountSplashSessionKey)) {
      state.accountSplashPending = true;
      sessionStorage.setItem(accountSplashSessionKey, "true");
    }
    renderDashboard();
  } catch (error) {
    showNotice(error.message, true);
    renderWelcome();
  }
}

async function loadAdminApplications(status = state.adminStatus) {
  state.adminStatus = status;
  const statusQuery = status === "All" ? "" : `status=${encodeURIComponent(status)}&`;
  state.adminApplications = await request(`/admin/driver-applications?${statusQuery}page=1&pageSize=20`);
}

function getCustomerPanelItems({ profile, driver, isAdministrator, hasDriverRole, hasActiveCustomerRide }) {
  const historyCount = (state.rideRequests || []).length;
  const items = [
    { id: "history", label: "Historial", meta: historyCount ? `${historyCount} registros` : "Viajes" }
  ];

  if (!hasDriverRole) {
    items.push({
      id: "driver",
      label: driver ? "Mi solicitud" : "Ser conductor",
      meta: "Conductor"
    });
  }

  items.push(
    { id: "profile", label: "Perfil", meta: "Cuenta" },
    { id: "safety", label: "Seguridad", meta: "Contactos" }
  );

  return items;
}

function getDriverOfferClassification(offer) {
  const reference = getDriverPriceReference(offer);
  if (reference.tone === "above") return { label: "Oferta favorable", tone: "favorable" };
  if (reference.tone === "close") return { label: "Oferta justa", tone: "fair" };
  if (reference.tone === "below" || reference.tone === "low") return { label: "Oferta baja", tone: "low" };
  return { label: "Oferta pendiente", tone: "pending" };
}
function resolveCustomerPanel(items, hasActiveCustomerRide) {
  const availablePanels = items.filter(item => !item.mode).map(item => item.id);
  if (availablePanels.includes(state.customerNav)) return state.customerNav;

  const fallback = hasActiveCustomerRide ? "tracking" : "ride";
  if (fallback === "ride" || fallback === "tracking") {
    state.customerNav = fallback;
    sessionStorage.setItem(customerNavKey, state.customerNav);
    return fallback;
  }
  state.customerNav = availablePanels.includes(fallback) ? fallback : availablePanels[0];
  sessionStorage.setItem(customerNavKey, state.customerNav);
  return state.customerNav;
}
function renderCustomerAppHeader(profile, hasDriverRole, isAdministrator) {
  return `
    <header class="customer-app-header" data-reveal>
      <a class="customer-mobile-brand" href="/" aria-label="HÁGALE, inicio"><img class="hagale-logo-image customer-logo-image" src="/assets/hagale-logo-yellow.png" alt="HÁGALE"></a>
      <div class="customer-app-actions">
        ${renderInstallAppButton("customer-install-button")}

        ${isAdministrator ? '<button class="button button-secondary small customer-admin-entry" type="button" data-open-admin>Administrador</button>' : ""}
        <button class="button button-secondary small" type="button" data-customer-nav="profile">Mi cuenta</button>
        <button class="button button-quiet small" type="button" data-sign-out>Salir</button>
      </div>
    </header>`;
}

function renderCustomerPanelNav(items, activePanel) {
  return `
    <nav class="customer-panel-nav" aria-label="Paneles de HÁGALE">
      ${items.map(item => item.mode
        ? `<button class="customer-panel-tab customer-panel-switch" type="button" data-set-mode="${item.mode}"><span>${escapeHtml(item.label)}</span><small>Entrar al despacho →</small></button>`
        : `<button class="customer-panel-tab ${activePanel === item.id ? "is-active" : ""}" type="button" data-customer-nav="${item.id}" aria-current="${activePanel === item.id ? "page" : "false"}"><span>${escapeHtml(item.label)}</span><small>${escapeHtml(item.meta)}</small></button>`
      ).join("")}
    </nav>`;
}

function renderModeSwitchControl(isDriverMode, compact = false) {
  return `
    <div class="mode-switch-app ${isDriverMode ? "is-driver" : "is-customer"} ${compact ? "is-compact" : ""}" aria-label="Seleccionar panel">
      <button class="mode-switch-tab is-customer-tab" type="button" data-set-mode="Customer" aria-pressed="${!isDriverMode}">
        <span class="mode-switch-tab-icon" aria-hidden="true">●</span><span>CLIENTE</span>
      </button>
      <button class="mode-switch-tab is-driver-tab" type="button" data-set-mode="Driver" aria-pressed="${isDriverMode}">
        <span class="mode-switch-tab-icon" aria-hidden="true">●</span><span>CONDUCTOR</span>
      </button>
    </div>`;
}
function renderCustomerPanelContent(panel, profile, driver, accountSummary, isAdministrator) {
  if (panel === "tracking") {
    return renderCustomerRideTrackingPanel() || renderRideRequestPanel();
  }

  if (panel === "history") {
    return renderCustomerRideHistoryPanel();
  }

  if (panel === "driver") {
    return `${renderPanelBackButton("Pedir moto")}${renderDriverPanel(driver, false)}`;
  }

  if (panel === "admin" && isAdministrator) {
    return `${renderPanelBackButton("Pedir moto")}${renderAdminPanel()}`;
  }

  if (panel === "profile") {
    return `${renderPanelBackButton("Pedir moto")}${accountSummary}${renderProfilePanel(profile)}`;
  }

  if (panel === "safety") {
    return `${renderPanelBackButton("Pedir moto")}${renderSafetyPanel("cliente")}`;
  }

  return renderRideRequestPanel();
}

function renderPanelBackButton(label = "Volver") {
  return `<div class="panel-back-row"><button class="button button-quiet panel-back-button" type="button" data-panel-back>← ${escapeHtml(label)}</button></div>`;
}

function renderDriverWorkspacePanel(driver, hasActiveJourney) {
  const activePanel = state.driverNav || "requests";
  const withBack = content => activePanel === "requests"
    ? content
    : `${renderPanelBackButton("Solicitudes")}${content}`;
  if (activePanel === "account") {
    return withBack(`${renderDriverPanel(driver, true)}${renderSafetyPanel("conductor")}`);
  }

  if (activePanel === "dispatch") {
    return withBack(`${renderDriverMap(driver)}${renderDriverDispatchPanel(driver)}`);
  }

  if (activePanel === "performance") {
    return withBack(renderDriverPerformancePanel());
  }

  if (activePanel === "wallet") {
    return withBack(renderDriverWalletPanel());
  }

  if (activePanel === "settings") {
    return withBack(`${renderDriverPanel(driver, true)}${renderSafetyPanel("conductor")}`);
  }

  return `${renderDriverMap(driver)}${renderDriverRideRequestsPanel()}`;
}

function getDriverLiveModeLabel(driver) {
  const appearance = getPlatformAppearance();
  if (driver?.availabilityStatus === "Busy") return "EN SERVICIO";
  if (driver?.availabilityStatus === "Available") return appearance.freeStatusLabel;
  return appearance.busyStatusLabel;
}

function renderDriverAvailabilitySlider(driver, compact = false) {
  const appearance = getPlatformAppearance();
  const isApproved = driver?.status === "Approved";
  const isAvailable = driver?.availabilityStatus === "Available";
  const isBusy = driver?.availabilityStatus === "Busy";
  const visibleOfferCount = (state.driverRideOffers || []).filter(offer => !state.hiddenDriverOfferIds.has(offer.id)).length;
  const detail = isBusy
    ? "Viaje activo"
    : isAvailable
      ? String(visibleOfferCount) + (visibleOfferCount === 1 ? " solicitud cercana" : " solicitudes cercanas")
      : isApproved ? "No recibes solicitudes" : "Cuenta en revisión";
  const locked = !isApproved || isBusy;
  return '<div class="driver-status-control ' + (compact ? "is-compact" : "") + " " + (isBusy ? "is-journey" : isAvailable ? "is-free" : "is-occupied") + '" data-availability-slider data-availability-locked="' + (locked ? "true" : "false") + '">' +
    '<div class="driver-status-slider" role="group" aria-label="Disponibilidad del conductor">' +
      '<button class="driver-status-option driver-status-occupied ' + (!isAvailable ? "is-active" : "") + '" type="button" data-availability="Offline" ' + (locked ? "disabled" : "") + ' aria-pressed="' + (!isAvailable) + '"><span class="driver-status-dot" aria-hidden="true"></span><strong>' + escapeHtml(appearance.busyStatusLabel) + '</strong><small>No recibir</small></button>' +
      '<button class="driver-status-option driver-status-free ' + (isAvailable ? "is-active" : "") + '" type="button" data-availability="Available" ' + (locked ? "disabled" : "") + ' aria-pressed="' + (isAvailable) + '"><span class="driver-status-dot" aria-hidden="true"></span><strong>' + escapeHtml(appearance.freeStatusLabel) + '</strong><small>Recibir viajes</small></button>' +
    '</div><small class="driver-status-detail">' + escapeHtml(isBusy ? "EN SERVICIO · " : "") + escapeHtml(detail) + '</small></div>';
}
function renderDriverQuickActions(activePanel, hasActiveJourney) {
  const visibleOfferCount = (state.driverRideOffers || []).filter(offer => !state.hiddenDriverOfferIds.has(offer.id)).length;
  const chatMeta = hasActiveJourney ? "Carrera activa" : "Al aceptar";
  const items = [
    { id: "requests", title: "Solicitudes", meta: `${visibleOfferCount} cerca`, icon: "≡" },
    { id: "performance", title: "Desempeño", meta: "Nivel y km", icon: "↗" },
    { id: "wallet", title: "Cartera", meta: "Saldo", icon: "$" },
    { id: "requests", title: "Chat privado", meta: chatMeta, icon: "✉", extraClass: hasActiveJourney ? "is-ready" : "" }
  ];

  return `
    <nav class="driver-quick-actions" aria-label="Accesos rápidos del conductor">
      ${items.map(item => `
        <button class="driver-quick-action ${activePanel === item.id ? "is-active" : ""} ${item.extraClass || ""}" type="button" data-driver-nav="${item.id}">
          <span aria-hidden="true">${escapeHtml(item.icon)}</span>
          <strong>${escapeHtml(item.title)}</strong>
          <small>${escapeHtml(item.meta)}</small>
        </button>`).join("")}
    </nav>`;
}

function renderDashboard() {
  destroyDriverMap();
  destroyCustomerRideMap();
  destroyCustomerTrackingMap();
  applyVisualMode();
  const profile = state.profile;
  const driver = state.application;
  const hasDriverRole = profile.roles.includes("Driver");
  const isDriverMode = hasDriverRole && state.activeMode === "Driver";
  const hasActiveJourney = ["Accepted", "DriverEnRoute", "DriverArrived", "InProgress"].includes(state.driverCurrentRideRequest?.status);
  const openCustomerRide = getOpenCustomerRideRequest();
  const customerServiceStatus = openCustomerRide ? label[openCustomerRide.status] : "Listo";
  const hasCustomerPricing = (state.pricingRules || []).some(rule => rule.isActive);
  document.body.classList.toggle("driver-mode", isDriverMode);
  document.body.classList.toggle("customer-mode", !isDriverMode);
  document.body.classList.remove("welcome-view");
  document.body.classList.remove("auth-panel-open");
  const isAdministrator = profile.roles.includes("Administrator");
  const approvedWithoutRole = driver?.status === "Approved" && !hasDriverRole;
  const visibleDriverOfferCount = (state.driverRideOffers || []).filter(offer => !state.hiddenDriverOfferIds.has(offer.id)).length;
  const hasActiveCustomerRide = Boolean(getActiveCustomerRide());
  const driverLiveModeLabel = getDriverLiveModeLabel(driver);
  const modeHero = isDriverMode
    ? `<article class="mode-hero mode-hero-driver"><div><img class="hagale-logo-image mode-hero-logo" src="/assets/hagale-logo-black.png" alt="HÁGALE"><span class="eyebrow">MODO CONDUCTOR</span><h1>Recibe servicios y decide rápido.</h1><p>Panel simple: estado, ofertas cercanas y viaje paso a paso.</p><div class="driver-hero-actions"><button class="button button-secondary small driver-hero-return" type="button" data-set-mode="Customer">Ir a modo cliente</button>${renderDriverVisualModeButton("driver-hero-visual")}${renderDriverAlertButton("driver-hero-alert")}${renderInstallAppButton("driver-hero-install")}</div></div><div class="mode-kpis"><div><strong class="driver-mode-status-big">${driverLiveModeLabel}</strong><span>Modo conductor</span></div><div><strong>${visibleDriverOfferCount}</strong><span>Ofertas nuevas</span></div></div></article>`
    : `<article class="mode-hero mode-hero-customer" data-reveal><div><img class="hagale-logo-image mode-hero-logo" src="/assets/hagale-logo-yellow.png" alt="HÁGALE"><span class="eyebrow">Cliente</span><h1>Tu moto, tu precio.</h1><p>Define origen, destino y tu oferta. Si compartes A y B, HÁGALE calcula la ruta real por calles antes de pedir la moto.</p></div><div class="mode-kpis"><div><strong>${escapeHtml(customerServiceStatus)}</strong><span>Estado actual</span></div><div><strong>${hasCustomerPricing ? "Moto" : "—"}</strong><span>${hasCustomerPricing ? "Servicio disponible" : "Tarifa pendiente"}</span></div></div></article>`;
  const modeSwitch = hasDriverRole ? renderModeSwitchControl(isDriverMode) : "";

  const accountSummary = `
    <article class="card card-dark account-summary">
      <span class="eyebrow">Tu cuenta</span>
      <p class="account-name">${escapeHtml(profile.firstName)} ${escapeHtml(profile.lastName)}</p>
      <p class="small-text">${escapeHtml(profile.email)}</p>
      <div class="role-list">${profile.roles.map(statusBadge).join("")}</div>
      ${approvedWithoutRole ? '<div class="role-refresh"><strong>Solicitud aprobada</strong><span class="small-text">Renueva tu sesión para activar el Modo conductor.</span><button class="button button-primary small" type="button" data-refresh-driver-role>Activar acceso conductor</button></div>' : ""}
      ${isAdministrator ? '<button class="button button-primary admin-quick-access" type="button" data-open-admin>Ir al Centro de administración</button>' : ""}
      <button class="button button-secondary" type="button" data-sign-out>Salir</button>
    </article>`;
  const customerPanelItems = isDriverMode
    ? []
    : getCustomerPanelItems({ profile, driver, isAdministrator, hasDriverRole, hasActiveCustomerRide });
  const activeCustomerPanel = isDriverMode
    ? null
    : resolveCustomerPanel(customerPanelItems, hasActiveCustomerRide);

  app.innerHTML = isDriverMode
    ? `
      <section class="shell dashboard-shell driver-shell ${hasActiveJourney ? "has-active-journey" : ""} driver-nav-${escapeHtml(state.driverNav || "requests")}">
        ${renderDriverMobileHeader(profile, driver)}
        <section class="driver-availability-focus" aria-label="Disponibilidad del conductor">${renderDriverAvailabilitySlider(driver)}</section>
        <div class="driver-app-layout">
          <aside class="driver-command-rail">
            ${renderDriverCommandRail(profile, driver, "")}
          </aside>
          <main class="driver-workspace stack">
            ${renderDriverWorkspacePanel(driver, hasActiveJourney)}
          </main>
        </div>
        ${renderDriverBottomNav()}
      </section>`
    : `
      <section class="shell dashboard-shell customer-shell customer-panel-shell">
        ${renderCustomerAppHeader(profile, hasDriverRole, isAdministrator)}
        ${modeSwitch}
        ${modeHero}
        ${renderCustomerPanelNav(customerPanelItems, activeCustomerPanel)}
        <main class="customer-panel-stage stack" data-active-customer-panel="${escapeHtml(activeCustomerPanel)}">
          ${renderCustomerPanelContent(activeCustomerPanel, profile, driver, accountSummary, isAdministrator)}
        </main>
      </section>`;

  if (state.accountSplashPending) {
    state.accountSplashPending = false;
    const splash = document.createElement("div");
    splash.className = "account-entry-splash";
    splash.setAttribute("role", "status");
    splash.setAttribute("aria-label", "Tu moto, tu precio");
    splash.innerHTML = `<div class="account-entry-splash-inner"><img src="/assets/hagale-logo-black.png" alt="HÁGALE" class="account-entry-splash-logo"><p class="account-entry-splash-title"><span>TU MOTO</span><span>TU PRECIO</span></p><span class="account-entry-splash-arrow" aria-hidden="true">➜</span></div>`;
    app.prepend(splash);
    document.body.classList.add("account-splash-active");
    window.setTimeout(() => splash.classList.add("is-leaving"), accountSplashLeaveMs);
    window.setTimeout(() => {
      splash.remove();
      document.body.classList.remove("account-splash-active");
    }, accountSplashDurationMs);
  }
  app.querySelectorAll("[data-sign-out]").forEach(button => {
    button.addEventListener("click", () => signOut(true));
  });
  bindInstallAppEvents();
  const refreshDriverRole = app.querySelector("[data-refresh-driver-role]");
  if (refreshDriverRole) refreshDriverRole.addEventListener("click", refreshDriverAccess);
  app.querySelectorAll("[data-set-mode]").forEach(button => button.addEventListener("click", () => setActiveMode(button.dataset.setMode)));
  app.querySelectorAll("[data-toggle-visual-mode]").forEach(button => button.addEventListener("click", toggleVisualMode));
  app.querySelectorAll("[data-customer-nav]").forEach(button => button.addEventListener("click", () => {
    state.customerNav = button.dataset.customerNav;
    sessionStorage.setItem(customerNavKey, state.customerNav);
    renderDashboard();
    app.scrollIntoView({ behavior: "smooth", block: "start" });
  }));
  app.querySelectorAll("[data-open-admin]").forEach(button => button.addEventListener("click", () => {
    if (!isDriverMode) {
      state.customerNav = "admin";
      sessionStorage.setItem(customerNavKey, state.customerNav);
      renderDashboard();
      return;
    }
    app.querySelector("#admin-center")?.scrollIntoView({ behavior: "smooth", block: "start" });
  }));
  app.querySelectorAll("[data-panel-back]").forEach(button => button.addEventListener("click", goBackPanel));
  const refreshProfile = app.querySelector("[data-refresh]");
  if (refreshProfile) refreshProfile.addEventListener("click", loadDashboard);
  const profileForm = app.querySelector("#profile-form");
  if (profileForm) profileForm.addEventListener("submit", updateProfile);
  app.querySelector("[data-delete-account]")?.addEventListener("click", deleteMyAccount);
  bindSafetyEvents();
  bindDriverEvents();
  bindAdminEvents();
  bindRideChatEvents();
  bindRideRatingEvents();
  syncDispatchPolling(isDriverMode, driver);
  syncCustomerTrackingPolling(!isDriverMode && profile.roles.includes("Customer"));
  syncRideRealtime();
  mountDriverMap();
  mountCustomerTrackingMap();
  mountCustomerRideMap();
  syncVisibleRideChats();
  syncVisibleRideRatings();
  activateRevealAnimations();
}

function renderProfilePanel(profile) {
  return `
    <article class="card profile-panel">
      <div class="section-title"><div><span class="eyebrow">Perfil</span><h2>Tu información</h2></div><button class="button button-secondary small" type="button" data-refresh>Actualizar</button></div>
      <form id="profile-form">
        <div class="form-grid">
          <div class="field"><label for="profile-first-name">Nombre</label><input id="profile-first-name" name="firstName" value="${escapeHtml(profile.firstName)}" minlength="2" required></div>
          <div class="field"><label for="profile-last-name">Apellido</label><input id="profile-last-name" name="lastName" value="${escapeHtml(profile.lastName)}" minlength="2" required></div>
        </div>
        <div class="field"><label for="profile-phone">Celular</label><input id="profile-phone" name="phoneNumber" value="${escapeHtml(profile.phoneNumber)}" inputmode="tel" pattern="\\+[1-9]\\d{7,14}" required></div>
        <div class="button-row"><button class="button" type="submit">Guardar cambios</button><button class="button button-danger" type="button" data-delete-account>Eliminar cuenta</button><span class="muted small-text">Registro: ${new Date(profile.registeredAtUtc).toLocaleDateString("es-CO")}</span></div>
      </form>
    </article>`;
}

function renderDriverMobileHeader(profile, driver) {
  return '<header class="driver-mobile-header" aria-label="Controles del conductor">' +
    '<button class="driver-mobile-icon" type="button" data-driver-nav="account" aria-label="Abrir cuenta"><svg aria-hidden="true" viewBox="0 0 24 24"><path d="M4 6h16M4 12h16M4 18h16"/></svg></button>' +
    '<div class="driver-mobile-brand">' + renderDriverPhotoBadge(profile, driver, { compact: true }) + '<span class="driver-mobile-mode-title">' + escapeHtml(getPlatformAppearance().driverModeLabel) + '</span></div>' +
    "" +
    renderDriverVisualModeButton("", true) +
    renderDriverAlertButton("driver-mobile-alert") +
    '<button class="driver-mobile-icon" type="button" data-driver-nav="settings" aria-label="Abrir configuración"><svg aria-hidden="true" viewBox="0 0 24 24"><path d="m12 3 1.2 1.8 2.2.5 1.8-1 1.5 1.5-1 1.8.5 2.2L21 11v2l-1.8 1.2-.5 2.2 1 1.8-1.5 1.5-1.8-1-2.2.5L12 21l-1.2-1.8-2.2-.5-1.8 1-1.5-1.5 1-1.8-.5-2.2L5 13v-2l1.8-1.2.5-2.2-1-1.8L8.6 4.3l1.8 1 2.2-.5L12 3Z"/><circle cx="12" cy="12" r="2.5"/></svg></button>' +
    '</header>';
}
function renderDriverBottomNav() {
  const activeNav = state.driverNav || "requests";
  const navButton = (destination, icon, text) => `<button type="button" class="${activeNav === destination ? "is-selected" : ""}" data-driver-nav="${destination}" aria-current="${activeNav === destination ? "page" : "false"}">${icon}<small>${text}</small></button>`;
  return `
    <nav class="driver-bottom-nav" aria-label="Navegación del conductor">
      ${navButton("requests", '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M5 6h14M5 12h14M5 18h9"/></svg>', "Solicitudes")}
      ${navButton("dispatch", '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="m13 2-8 12h6l-1 8 8-12h-6l1-8Z"/></svg>', "Demanda")}
      ${navButton("performance", '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M4 19V5M4 19h16"/><path d="m7 15 3-4 3 2 5-7"/></svg>', "Desempeño")}
      ${navButton("wallet", '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M4 6.5h14a2 2 0 0 1 2 2v9H4z"/><path d="M4 6.5v-1h12a2 2 0 0 1 2 2"/><path d="M15 13h5"/></svg>', "Cartera")}
      <button type="button" class="driver-mode-return" data-set-mode="Customer" aria-label="Volver al panel de cliente"><svg aria-hidden="true" viewBox="0 0 24 24"><path d="M19 12H5"/><path d="m12 5-7 7 7 7"/></svg><small>Cliente</small></button>
    </nav>`;
}

function renderDriverCommandRail(profile, driver, modeSwitch) {
  const isApproved = driver?.status === "Approved";
  const isAvailable = driver?.availabilityStatus === "Available";
  const isBusy = driver?.availabilityStatus === "Busy";
  const activeVehicle = driver?.vehicles?.find(vehicle => vehicle.isActive);
  const serviceState = getDriverLiveModeLabel(driver);
  const serviceDescription = isBusy
    ? "Tienes un servicio en curso. Finalízalo para recibir nuevas solicitudes."
    : isAvailable
      ? "Estás visible para solicitudes compatibles en tu ciudad."
      : "Activa tu disponibilidad cuando estés listo para recibir solicitudes.";

  return `
    <article id="driver-account" class="driver-identity">
      <span class="eyebrow">Socio conductor</span>
      ${renderDriverPhotoBadge(profile, driver)}
      <h2>${escapeHtml(profile.firstName)}</h2>
      <p>${activeVehicle ? `${escapeHtml(activeVehicle.brand)} ${escapeHtml(activeVehicle.model)} · ${escapeHtml(activeVehicle.operatingCityCode)}` : "Vehículo pendiente"}</p>
      <div class="driver-live-state ${isAvailable ? "is-online" : isBusy ? "is-busy" : ""}"><span aria-hidden="true"></span>${serviceState}</div>
    </article>
    <article class="driver-command-card">
      <h3>${isAvailable ? "Estás recibiendo solicitudes" : isBusy ? "Servicio en curso" : "¿Listo para trabajar?"}</h3>
      <p>${serviceDescription}</p>
      <div class="driver-command-actions driver-command-status" aria-hidden="true"></div>
      ${!isApproved ? `<p class="driver-rail-warning">Tu cuenta aún está ${escapeHtml(label[driver?.status] || "en proceso")}. Administración debe aprobarla antes de conectar el despacho.</p>` : ""}
    </article>
    ${renderDriverAlertControl()}
    <article class="driver-visibility-card">
      <h3>Visibilidad en calle</h3>
      <p>${state.visualMode === "night" ? "Modo noche activo para reducir brillo y mantener contraste." : "Modo día activo: textos grandes, negros y fuertes para sol directo."}</p>
      ${renderDriverVisualModeButton("driver-rail-visual")}
    </article>
    <article class="driver-rail-info">
      <span>Ciudad de despacho</span>
      <strong>${escapeHtml(activeVehicle?.operatingCityCode || "Sin ciudad")}</strong>
      <span>Estado de moto</span>
      <strong>${activeVehicle?.isActive ? "Activa" : "Pendiente"}</strong>
    </article>
    <div class="driver-rail-actions">
      ${modeSwitch ? renderModeSwitchControl(true, true) : ""}
      <button class="button button-secondary" type="button" data-sign-out>Salir</button>
    </div>
    <details class="driver-account-details">
      <summary>Mi cuenta y perfil</summary>
      <div class="driver-account-content">${renderProfilePanel(profile)}</div>
    </details>`;
}

function renderDriverDispatchPanel(driver) {
  const isAvailable = driver?.availabilityStatus === "Available";
  const locationText = driver?.locationUpdatedAtUtc
    ? `Ubicación de despacho actualizada ${formatDateTime(driver.locationUpdatedAtUtc)}.`
    : "Aún no has compartido tu ubicación de despacho.";
  const radiusOptions = [3, 5, 8, 15]
    .map(radius => `<button class="dispatch-radius ${state.dispatchRadiusKilometers === radius ? "selected" : ""}" type="button" data-set-dispatch-radius="${radius}" ${isAvailable ? "" : "disabled"}>${radius} km</button>`)
    .join("");

  return `
    <article id="driver-dispatch" class="driver-dispatch-card" data-reveal>
      <div class="section-title">
        <div><span class="eyebrow">Despacho inteligente</span><h2>Solicitudes cercanas</h2><p class="muted">Las solicitudes se ordenan por distancia directa hasta la recogida cuando el pasajero y tú comparten ubicación.</p></div>
        <div class="driver-dispatch-count"><strong>${state.driverRideOffers.length}</strong><span>disponibles</span></div>
      </div>
      <div class="dispatch-location-row">
        <div><strong>Tu ubicación</strong><p>${locationText}</p></div>
        <button class="button button-secondary small" type="button" data-update-dispatch-location ${isAvailable ? "" : "disabled"}>Actualizar ubicación</button>
      </div>
      <div class="dispatch-filter-row">
        <div><strong>Radio de búsqueda</strong><div class="dispatch-radius-list">${radiusOptions}</div></div>
        <button class="button button-secondary small" type="button" data-refresh-dispatch ${isAvailable ? "" : "disabled"}>Actualizar solicitudes</button>
      </div>
      <section class="dispatch-tech-card">
        <div><strong>Tiempo real</strong><span>SignalR + respaldo cada 20 s</span></div>
        <div><strong>Más cerca</strong><span>Orden por GPS directo a recogida</span></div>
        <div><strong>Batería</strong><span>Segundo plano requiere app instalada/PWA</span></div>
      </section>
      <p class="dispatch-privacy-note">Tu ubicación solo se usa para ordenar ofertas mientras estás disponible.</p>
    </article>`;
}

function setActiveMode(mode) {
  if (mode === "Driver" && !state.profile?.roles.includes("Driver")) return;
  state.activeMode = mode === "Driver" ? "Driver" : "Customer";
  sessionStorage.setItem(modeKey, state.activeMode);
  if (state.activeMode === "Driver" && !state.driverNav) {
    state.driverNav = "requests";
  }
  if (state.activeMode === "Customer" && !state.customerNav) {
    state.customerNav = getActiveCustomerRide() ? "tracking" : "ride";
    sessionStorage.setItem(customerNavKey, state.customerNav);
  }
  renderDashboard();
  if (state.activeMode === "Driver") {
    refreshDriverDispatch()
      .then(() => renderDashboard())
      .catch(error => showNotice(error.message, true));
  }
  showNotice(state.activeMode === "Driver" ? "Modo conductor activado." : "Modo cliente activado.");
}

async function refreshDriverAccess() {
  try {
    await renewCurrentSession();
    state.activeMode = "Driver";
    sessionStorage.setItem(modeKey, state.activeMode);
    await loadDashboard({ allowRoleSessionRenewal: false });
    showNotice("Acceso actualizado. Ya puedes usar el Modo conductor.");
  } catch (error) {
    showNotice(error.message, true);
  }
}

function canUseRideChat(rideRequest) {
  return Boolean(rideRequest?.id && activeCustomerRideStatuses.has(rideRequest.status));
}

function renderRideChatMessages(messages = []) {
  if (!messages.length) {
    return '<li class="ride-chat-empty">Todavía no hay mensajes. Puedes avisar por aquí que ya vas en camino.</li>';
  }

  return messages.map(message => {
    const isMine = state.profile?.userId && message.senderUserId === state.profile.userId;
    const sender = isMine ? "Tú" : (message.senderName || message.senderRole || "Participante");
    return `
      <li class="ride-chat-message ${isMine ? "is-mine" : "is-other"}">
        <div class="ride-chat-message-meta"><strong>${escapeHtml(sender)}</strong><time>${escapeHtml(formatDateTime(message.sentAtUtc))}</time></div>
        <p>${escapeHtml(message.message)}</p>
      </li>`;
  }).join("");
}

function renderPrivateCommunicationCard(rideRequest = null) {
  const chatEnabled = canUseRideChat(rideRequest);
  const rideId = rideRequest?.id || "";
  const messages = rideId ? (state.rideChatMessages[rideId] || []) : [];

  if (!chatEnabled) {
    return `
      <section class="private-communication-card is-disabled">
        <div>
          <span class="eyebrow">Comunicación privada</span>
          <strong>Chat interno protegido</strong>
          <p>Se habilita cuando el servicio sea aceptado. Solo podrán verlo el pasajero y el conductor asignado.</p>
        </div>
        <div class="private-communication-actions" aria-label="Funciones de comunicación">
          <button type="button" disabled>Chat</button>
          <button type="button" disabled title="Las llamadas privadas se conectarán en la siguiente integración">Llamar</button>
          <button type="button" disabled title="Telegram queda como respaldo de alertas">Telegram</button>
        </div>
      </section>`;
  }

  return `
    <section class="private-communication-card is-active" data-ride-chat="${escapeHtml(rideId)}">
      <div class="private-communication-heading">
        <div>
          <span class="eyebrow">Comunicación privada</span>
          <strong>Chat de esta carrera</strong>
          <p>Mensaje directo entre los participantes. No se publican teléfonos ni se mezcla con otras carreras.</p>
        </div>
        <button class="button button-quiet small" type="button" data-refresh-ride-chat="${escapeHtml(rideId)}" aria-label="Actualizar chat">↻</button>
      </div>
      <ul class="ride-chat-messages" data-ride-chat-list="${escapeHtml(rideId)}" aria-live="polite">${renderRideChatMessages(messages)}</ul>
      <form class="ride-chat-form" data-ride-chat-form="${escapeHtml(rideId)}">
        <label class="sr-only" for="ride-chat-input-${escapeHtml(rideId)}">Mensaje privado</label>
        <input id="ride-chat-input-${escapeHtml(rideId)}" name="message" maxlength="500" autocomplete="off" placeholder="Escribe un mensaje..." required>
        <button class="button button-primary small" type="submit">Enviar</button>
      </form>
      <div class="private-communication-actions" aria-label="Funciones de comunicación">
        <button class="is-selected" type="button" disabled>Chat interno</button>
        <button type="button" data-private-call="${escapeHtml(rideId)}" title="Llamada privada de esta carrera">Llamar</button>
        <button type="button" disabled title="Telegram queda como respaldo de alertas">Telegram</button>
      </div>
    </section>`;
}

function updateRideChatDom(rideRequestId) {
  const rideId = String(rideRequestId || "");
  if (!rideId) return;
  const list = app.querySelector(`[data-ride-chat-list="${rideId}"]`);
  if (!list) return;
  list.innerHTML = renderRideChatMessages(state.rideChatMessages[rideId] || []);
  list.scrollTop = list.scrollHeight;
}

async function loadRideChat(rideRequestId) {
  const rideId = String(rideRequestId || "");
  if (!rideId || !state.token || state.rideChatLoadingIds.has(rideId)) return;
  state.rideChatLoadingIds.add(rideId);
  try {
    state.rideChatMessages[rideId] = await request(`/ride-requests/${rideId}/messages`);
    updateRideChatDom(rideId);
  } catch {
    // El chat puede dejar de estar disponible cuando el servicio cambia de
    // estado; la tarjeta visible conserva el último contenido recibido.
  } finally {
    state.rideChatLoadingIds.delete(rideId);
  }
}

async function refreshRideChatIfVisible(rideRequestId) {
  const rideId = String(rideRequestId || "");
  if (!rideId || !app.querySelector(`[data-ride-chat-list="${rideId}"]`)) return;
  await loadRideChat(rideId);
}

function syncVisibleRideChats() {
  app.querySelectorAll("[data-ride-chat-list]").forEach(list => {
    void loadRideChat(list.dataset.rideChatList);
  });
}

async function sendRideChatMessage(event, form) {
  event.preventDefault();
  const rideId = form.dataset.rideChatForm;
  const input = form.elements.message;
  const message = input?.value?.trim();
  if (!rideId || !message) return;

  const submit = form.querySelector("button[type='submit']");
  if (submit) submit.disabled = true;
  try {
    const created = await request(`/ride-requests/${rideId}/messages`, {
      method: "POST",
      data: { message }
    });
    const messages = state.rideChatMessages[rideId] || [];
    state.rideChatMessages[rideId] = [...messages, created].slice(-200);
    updateRideChatDom(rideId);
    input.value = "";
  } catch (error) {
    showNotice(error.message || "No fue posible enviar el mensaje.", true);
  } finally {
    if (submit?.isConnected) submit.disabled = false;
  }
}


function handlePrivateCall(rideRequestId) {
  const rideId = String(rideRequestId || "");
  if (!rideId) return;
  showNotice("Llamada privada preparada para esta carrera. Falta conectar el proveedor de voz para no exponer celulares.", true);
}
function bindRideChatEvents() {
  app.querySelectorAll("[data-ride-chat-form]").forEach(form => {
    form.addEventListener("submit", event => sendRideChatMessage(event, form));
  });
  app.querySelectorAll("[data-private-call]").forEach(button => {
    button.addEventListener("click", () => handlePrivateCall(button.dataset.privateCall));
  });
  app.querySelectorAll("[data-refresh-ride-chat]").forEach(button => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      try {
        await loadRideChat(button.dataset.refreshRideChat);
      } finally {
        if (button.isConnected) button.disabled = false;
      }
    });
  });
}

function renderDriverRideRequestsPanel() {
  const currentRequest = state.driverCurrentRideRequest;
  if (currentRequest) {
    const journeyStage = getDriverJourneyStage(currentRequest);
    const journeyGuidance = renderDriverJourneyGuidance(currentRequest);
    const journeyAction = renderDriverJourneyActionButton(currentRequest, journeyStage);
    const journeyDescription = currentRequest.status === "Accepted"
      ? "Confirma cuando estés desplazándote hacia el punto de recogida."
      : currentRequest.status === "DriverEnRoute"
        ? "Cuando llegues al punto A, confírmalo antes de iniciar el viaje."
        : currentRequest.status === "DriverArrived"
          ? "Ya llegaste a la recogida. Inicia el viaje solo cuando el pasajero esté listo."
          : currentRequest.status === "InProgress"
            ? "El servicio está en curso. Finalízalo al llegar al destino."
            : currentRequest.status === "CounterOfferPending"
              ? `Propusiste ${formatCop(currentRequest.counterOfferPriceCop)}. Quedas reservado mientras el pasajero decide.`
              : "Este servicio ya finalizó.";
    const navigationTarget = currentRequest.status === "InProgress" ? "destination" : "pickup";
    const showNavigation = ["Accepted", "DriverEnRoute", "DriverArrived", "InProgress"].includes(currentRequest.status);
    return `
      <article id="driver-requests" class="driver-active-card driver-active-journey-card ${currentRequest.status === "Accepted" ? "is-accepted" : ""}" data-reveal>
        <div class="driver-active-heading"><div><span class="eyebrow">Servicio asignado</span><h2>${currentRequest.status === "Completed" ? "Servicio finalizado" : currentRequest.status === "Accepted" ? "¡Servicio aceptado!" : "Tu recorrido actual"}</h2><p>${journeyDescription}</p></div>${statusBadge(currentRequest.status)}</div>
        ${currentRequest.status === "Accepted" ? '<div class="driver-accepted-banner"><strong>✓ ACEPTADO</strong><span>El pasajero ya recibió tu confirmación.</span></div>' : ""}
        ${renderDriverJourneyStageAlert(currentRequest, journeyStage)}
        <div class="driver-route-card">
          <div class="route-stop route-stop-pickup"><span>RECOGIDA · A</span><strong>${escapeHtml(currentRequest.pickupAddress)}</strong></div>
          <div class="route-connector" aria-hidden="true"></div>
          <div class="route-stop route-stop-destination"><span>DESTINO · B</span><strong>${escapeHtml(currentRequest.destinationAddress)}</strong></div>
        </div>
        <div class="driver-route-audio-actions"><button class="button driver-repeat-route" type="button" data-repeat-driver-route="${escapeHtml(currentRequest.id)}">🔊 Repetir dirección</button>${renderSafetyAssistButton("driver")}</div>
        ${journeyGuidance}
        ${renderJourneyTimeline(currentRequest)}
        ${renderWaitingInformation(currentRequest, null, "driver")}
        ${renderPrivateCommunicationCard(currentRequest)}
        ${showNavigation ? renderDriverNavigationAction(currentRequest, navigationTarget) : ""}
        ${journeyAction ? `<div class="button-row driver-active-actions">${journeyAction}</div>` : ""}
      </article>`;
  }

  const offers = (state.driverRideOffers || []).filter(offer => !state.hiddenDriverOfferIds.has(offer.id));
  const selectedOffer = offers.find(offer => offer.id === state.selectedDriverOfferId);
  const availabilityNotice = state.application?.availabilityStatus === "Available"
    ? "Las solicitudes nuevas se ordenan por cercanía. Toca una para ver el mapa y decidir."
    : "Activa tu disponibilidad desde el panel lateral para comenzar a recibir solicitudes.";
  const offerList = offers.length
    ? offers.map(offer => `
      <li class="driver-request-row ${offer.id === state.selectedDriverOfferId ? "is-selected" : ""}" data-view-driver-offer="${offer.id}" tabindex="0" role="button" aria-label="Ver solicitud de ${escapeHtml(offer.pickupAddress)}">
        <div class="driver-request-priceblock">
          <span>Nueva solicitud</span>
          <strong class="driver-request-price">${formatCop(offer.proposedPriceCop)}</strong>
          <small>${escapeHtml(getRidePaymentMethodLabel(offer))}</small>
        </div>
        <div class="driver-request-main">
          <div class="driver-request-top"><span class="driver-request-distance">${formatPickupProximity(offer.pickupDistanceKilometers)}</span><span class="driver-request-pulse">Toca para ver detalles</span></div>
          <h3>${escapeHtml(offer.pickupAddress)}</h3>
          <p>${escapeHtml(offer.destinationAddress)}</p>
          <div class="driver-request-tags"><span>${escapeHtml(label[offer.serviceType] || offer.serviceType)}</span><span>${escapeHtml(offer.operatingCityCode)}</span>${ridePreferenceTagSpans(offer)}${offer.pickupDistanceKilometers === null || offer.pickupDistanceKilometers === undefined ? '<span>Sin GPS del pasajero</span>' : ""}</div>
        </div>
        <button class="driver-request-more" type="button" data-dismiss-offer="${offer.id}" aria-label="Ocultar solicitud">⋮</button>
      </li>`).join("")
    : `<li class="dispatch-empty"><span class="dispatch-empty-icon" aria-hidden="true">⌁</span><strong>${state.application?.availabilityStatus === "Available" ? "Buscando servicios cerca de ti" : "El despacho está desconectado"}</strong><p>${state.application?.availabilityStatus === "Available" ? "Cuando un pasajero solicite una moto compatible en tu zona, aparecerá aquí." : "Activa tu disponibilidad para empezar a recibir solicitudes."}</p></li>`;

  return `
    <article id="driver-requests" class="driver-offers-card driver-request-inbox" data-reveal>
      <div class="section-title driver-request-inbox-heading"><div><span class="eyebrow">Bandeja de despacho</span><h2>Solicitudes de viaje</h2><p class="muted">${availabilityNotice}</p></div><span class="offer-counter" aria-label="${offers.length} ${offers.length === 1 ? "solicitud" : "solicitudes"}"><strong>${offers.length}</strong><small>${offers.length === 1 ? "solicitud" : "solicitudes"}</small></span></div>
      ${state.application?.availabilityStatus === "Available" ? '<p class="dispatch-live-note"><span aria-hidden="true"></span>Despacho activo · se actualiza automáticamente cada 20 segundos</p>' : ""}
      <ul class="driver-request-list">${offerList}</ul>
      ${selectedOffer ? renderDriverOfferSheet(selectedOffer) : ""}
    </article>`;
}

function renderDriverOfferSheet(offer) {
  const classification = getDriverOfferClassification(offer);
  const appearance = getPlatformAppearance();
  const requestedFare = Number(offer?.proposedPriceCop) || 0;
  const distanceReferenceFare = Number(offer?.directDistanceReferenceFareCop) || 0;
  const counterOfferBase = Math.max(requestedFare, distanceReferenceFare);
  const counterOfferSuggestions = [500, 1000, 1500]
    .map(increment => counterOfferBase + increment)
    .filter(price => Number.isFinite(price) && price > counterOfferBase);
  const counterOfferButtons = counterOfferSuggestions.map(price => `
    <button class="button driver-quick-counter-offer" type="button" data-quick-counter-offer="${escapeHtml(offer.id)}" data-counter-offer-price="${price}">
      <span>Ofrecer</span><strong>${formatCop(price)}</strong>
    </button>`).join("");
  return `
    <section class="driver-request-sheet" aria-label="Detalle de la solicitud seleccionada">
      <div class="driver-sheet-heading"><div><span class="eyebrow">Solicitud seleccionada</span><h3><span>Gana</span><strong>${formatCop(offer.proposedPriceCop)}</strong></h3><p>${formatPickupProximity(offer.pickupDistanceKilometers)} · ${escapeHtml(getRidePaymentMethodLabel(offer))}</p></div><button class="button button-quiet" type="button" data-close-driver-offer>Cerrar</button></div>
      <div class="driver-offer-route-summary" aria-label="Direcciones completas">
        <div><span class="driver-offer-route-label is-pickup">A · Recogida</span><strong>${formatDriverOfferAddress(offer.pickupAddress)}</strong></div>
        <div><span class="driver-offer-route-label is-destination">B · Entrega</span><strong>${formatDriverOfferAddress(offer.destinationAddress)}</strong></div>
      </div>
      <div class="driver-offer-classification is-${classification.tone}" aria-label="Clasificación de la oferta">${classification.label}</div>
      <div class="driver-offer-action-stack">
        <button class="button driver-accept-large" type="button" data-accept-ride="${offer.id}"><span>Aceptar por</span><strong>${formatCop(offer.proposedPriceCop)}</strong></button>
        <div class="driver-counter-sheet" aria-label="Contraofertas rápidas">
          <label>Contraofertas rápidas · suben de $500 en $500</label>
          <div class="driver-counter-options">${counterOfferButtons}</div>
        </div>
      </div>
    </section>`;
}
function getDriverRecognition(summary = {}) {
  const rides = Number(summary.completedRideCount) || 0;
  const value = Number(summary.completedServiceValueCop) || 0;
  const distance = Number(summary.completedDirectDistanceKilometers) || 0;
  const score = rides * 10 + Math.floor(value / 20_000) + Math.floor(distance / 5);
  const levels = [
    { name: "Arranque", className: "bronze", min: 0, next: 60, benefit: "Base de confianza: historial visible y prioridad normal." },
    { name: "Ruta Pro", className: "silver", min: 60, next: 180, benefit: "Más visibilidad en solicitudes y distintivo de confianza." },
    { name: "Leyenda HÁGALE", className: "gold", min: 180, next: null, benefit: "Máximo reconocimiento, prioridad alta y beneficios comerciales." }
  ];
  const current = score >= 180 ? levels[2] : score >= 60 ? levels[1] : levels[0];
  const nextLevel = current.next === null ? null : levels.find(level => level.min === current.next);
  const progress = current.next === null
    ? 100
    : Math.max(0, Math.min(100, Math.round(((score - current.min) / (current.next - current.min)) * 100)));
  return { ...current, score, progress, nextLevel };
}

function getDriverCommissionLaunch(driver) {
  const startValue = driver?.approvedAtUtc || driver?.appliedAtUtc;
  if (!startValue) {
    return { daysUsed: 0, daysLeft: 30, status: "Primer mes sin comisión listo para activar al aprobarse." };
  }
  const elapsedDays = Math.max(0, Math.floor((Date.now() - new Date(startValue).getTime()) / 86_400_000));
  const daysLeft = Math.max(0, 30 - elapsedDays);
  return {
    daysUsed: Math.min(30, elapsedDays),
    daysLeft,
    status: daysLeft > 0
      ? `${daysLeft} día${daysLeft === 1 ? "" : "s"} de lanzamiento sin comisión.`
      : "Periodo de lanzamiento finalizado; comisión pendiente por configurar."
  };
}

function renderDriverRecognitionPanel(summary, driver) {
  const recognition = getDriverRecognition(summary);
  const commission = getDriverCommissionLaunch(driver);
  const nextText = recognition.nextLevel
    ? `Te faltan ${Math.max(0, recognition.next - recognition.score)} puntos para llegar a ${recognition.nextLevel.name}.`
    : "Estás en el nivel máximo de reconocimiento.";
  return `
    <section class="driver-growth-card level-${escapeHtml(recognition.className)}">
      <div class="driver-level-medal">
        <span>${escapeHtml(recognition.name[0])}</span>
      </div>
      <div class="driver-growth-main">
        <span class="eyebrow">Reconocimiento HÁGALE</span>
        <h3>Nivel ${escapeHtml(recognition.name)}</h3>
        <p>${escapeHtml(recognition.benefit)}</p>
        <div class="driver-level-progress" aria-label="Progreso del nivel"><i style="width: ${recognition.progress}%"></i></div>
        <small>${escapeHtml(nextText)} Puntaje demo: ${recognition.score}.</small>
      </div>
      <div class="driver-commission-card">
        <strong>0% comisión</strong>
        <span>Primer mes</span>
        <p>${escapeHtml(commission.status)}</p>
      </div>
    </section>`;
}

function rideRatingStars(score) {
  const value = Math.max(0, Math.min(5, Number(score) || 0));
  return `${"★".repeat(value)}${"☆".repeat(5 - value)}`;
}

function renderRideRatingWidget(rideRequest, audience = "customer") {
  if (rideRequest?.status !== "Completed" || !rideRequest.id) return "";
  const rideId = rideRequest.id;
  const ratings = state.rideRatings[rideId] || [];
  const myRating = ratings.find(rating => rating.isMine);
  const otherRating = ratings.find(rating => !rating.isMine);
  const targetLabel = audience === "driver" ? "cliente" : "conductor";


  if (myRating) {
    return `
      <section class="ride-rating-widget is-complete" data-ride-rating-widget="${escapeHtml(rideId)}" data-rating-audience="${escapeHtml(audience)}">
        <div><span class="eyebrow">Reputación HÁGALE</span><strong>Tu calificación: <span class="rating-stars">${rideRatingStars(myRating.score)}</span></strong><small>Tu calificación quedó guardada de forma privada.</small></div>
        ${otherRating ? `<span class="rating-received"><strong>${rideRatingStars(otherRating.score)}</strong><small>Calificación recibida · anónima</small></span>` : '<span class="rating-pending">La otra persona aún no califica. Cuando lo haga, la verás mañana de forma anónima.</span>'}
      </section>`;
  }

  return `
    <section class="ride-rating-widget" data-ride-rating-widget="${escapeHtml(rideId)}" data-rating-audience="${escapeHtml(audience)}">
      <div class="ride-rating-heading"><div><span class="eyebrow">Servicio finalizado</span><strong>¿Cómo fue tu experiencia con el ${targetLabel}?</strong><small>Tu calificación ayuda a construir confianza en HÁGALE.</small></div>${otherRating ? `<span class="rating-received"><strong>${rideRatingStars(otherRating.score)}</strong><small>Calificación anónima disponible</small></span>` : ""}</div>
      <form class="ride-rating-form" data-ride-rating-form="${escapeHtml(rideId)}" data-rating-audience="${escapeHtml(audience)}">
        <div class="ride-rating-stars" role="radiogroup" aria-label="Calificación de 1 a 5 estrellas">
          ${[5, 4, 3, 2, 1].map(score => `<label><input type="radio" name="rating-${escapeHtml(rideId)}" value="${score}" ${score === 5 ? "checked" : ""}><span>${rideRatingStars(score)}</span></label>`).join("")}
        </div>
        <textarea name="comment" maxlength="240" rows="2" placeholder="Comentario opcional"></textarea>
        <button class="button button-primary small" type="submit">Guardar calificación</button>
      </form>
    </section>`;
}

async function loadRideRatings(rideRequestId) {
  const rideId = String(rideRequestId || "");
  if (!rideId || !state.token || state.rideRatingLoadingIds.has(rideId)) return;
  state.rideRatingLoadingIds.add(rideId);
  try {
    const ratings = await request(`/ride-requests/${rideId}/ratings`);
    state.rideRatings[rideId] = ratings;

    const ride = [...(state.rideRequests || []), ...(state.driverCompletedRideRequests || [])]
      .find(item => item.id === rideId);
    if (ride) {
      app.querySelectorAll(`[data-ride-rating-widget="${rideId}"]`).forEach(ratingNode => {
        ratingNode.outerHTML = renderRideRatingWidget(ride, ratingNode.dataset.ratingAudience || "customer");
      });
      bindRideRatingEvents();
    }


  } catch {
    // El siguiente refresco vuelve a consultar el estado autorizado.
  } finally {
    state.rideRatingLoadingIds.delete(rideId);
  }
}

function syncVisibleRideRatings() {
  const rideIds = new Set([...app.querySelectorAll("[data-ride-rating-widget]")]
    .map(widget => widget.dataset.rideRatingWidget)
    .filter(Boolean));
  rideIds.forEach(rideId => void loadRideRatings(rideId));
}

async function submitRideRating(event, form) {
  event.preventDefault();
  const rideId = form.dataset.rideRatingForm;
  const formData = new FormData(form);
  const score = Number(formData.get(`rating-${rideId}`));
  const comment = String(formData.get("comment") || "").trim();
  const button = form.querySelector("button[type='submit']");
  if (!rideId || !Number.isInteger(score) || score < 1 || score > 5) {
    showNotice("Selecciona entre 1 y 5 estrellas.", true);
    return;
  }

  if (button) button.disabled = true;
  try {
    const created = await request(`/ride-requests/${rideId}/ratings`, {
      method: "POST",
      data: { score, comment: comment || null }
    });
    state.rideRatings[rideId] = [...(state.rideRatings[rideId] || []), created];
    renderDashboard();
    showNotice("Calificación guardada. Gracias por ayudar a mejorar HÁGALE.");
  } catch (error) {
    showNotice(error.message || "No fue posible guardar la calificación.", true);
  } finally {
    if (button?.isConnected) button.disabled = false;
  }
}

function bindRideRatingEvents() {
  app.querySelectorAll("[data-ride-rating-form]").forEach(form => {
    if (form.dataset.ratingBound === "true") return;
    form.dataset.ratingBound = "true";
    form.addEventListener("submit", event => submitRideRating(event, form));
  });
}

function renderDriverRatingsPreview() {
  const completed = (state.driverCompletedRideRequests || []).slice(0, 3);
  const completedList = completed.length
    ? `<ul class="driver-recent-ratings">${completed.map(ride => `
        <li><div><strong>${escapeHtml(ride.destinationAddress)}</strong><small>${ride.completedAtUtc ? formatDateTime(ride.completedAtUtc) : "Servicio finalizado"} · ${formatCop(ride.proposedPriceCop)}</small></div>${renderRideRatingWidget(ride, "driver")}</li>`).join("")}</ul>`
    : '<p class="small-text muted">Cuando finalices una carrera aparecerá aquí la opción para calificar al cliente.</p>';
  return `
    <section class="driver-rating-card">
      <div><span class="eyebrow">Calificaciones</span><h3>Reputación compartida</h3><p>Cliente y conductor se califican después de cada servicio finalizado.</p></div>
      <div class="rating-preview-grid">
        <div><strong>★ —</strong><span>Como conductor</span></div>
        <div><strong>★ —</strong><span>Clientes atendidos</span></div>
      </div>
      ${completedList}
    </section>`;
}

function renderDriverPerformancePanel() {
  const summary = state.driverActivitySummary || {};
  const completedRideCount = Number(summary.completedRideCount) || 0;
  const completedValue = Number(summary.completedServiceValueCop) || 0;
  const waitingValue = Number(summary.completedWaitingChargeCop) || 0;
  const directDistance = summary.completedDirectDistanceKilometers;
  const estimatedTodayNet = completedValue + waitingValue;
  return `
    <article id="driver-performance" class="card driver-performance-card">
      <div class="section-title"><div><span class="eyebrow">Mi día · desempeño</span><h2>Tu actividad real</h2><p class="muted">Resumen operativo de servicios finalizados, valores y kilómetros directos registrados.</p></div><span class="performance-mark" aria-hidden="true">↗</span></div>
      ${renderDriverRecognitionPanel(summary, state.application)}
      <section class="driver-day-card">
        <div><span class="eyebrow">Cierre rápido</span><h3>Hoy en HÁGALE</h3><p>Este panel será el reporte diario del conductor: carreras, kilómetros, pagos y calificación.</p></div>
        <strong>${formatCop(estimatedTodayNet)}</strong>
        <small>Valor finalizado + espera registrada. Demo acumulada hasta conectar corte diario real.</small>
      </section>
      <div class="performance-metrics">
        <div><strong>${completedRideCount}</strong><span>Servicios finalizados</span></div>
        <div><strong>${formatCop(completedValue)}</strong><span>Valor finalizado</span></div>
        <div><strong>${directDistance == null ? "—" : formatDistance(directDistance)}</strong><span>Distancia directa registrada</span></div>
        <div><strong>${formatCop(waitingValue)}</strong><span>Espera adicional registrada</span></div>
      </div>
      ${renderDriverRatingsPreview()}
      <p class="small-text muted">${summary.lastCompletedAtUtc ? `Último servicio finalizado: ${formatDateTime(summary.lastCompletedAtUtc)}. Espera adicional acumulada: ${formatCop(waitingValue)}.` : "Cuando completes tu primer servicio, aparecerá aquí."}</p>
    </article>`;
}

function renderDriverWalletPanel() {
  const summary = state.driverActivitySummary || {};
  const completedValue = Number(summary.completedServiceValueCop) || 0;
  const cashValue = Number(summary.completedCashValueCop) || 0;
  const nequiValue = Number(summary.completedNequiValueCop) || 0;
  const waitingValue = Number(summary.completedWaitingChargeCop) || 0;
  const cashCount = Number(summary.completedCashRideCount) || 0;
  const nequiCount = Number(summary.completedNequiRideCount) || 0;
  const platformBalance = 0;
  const platformCommissionPending = 0;
  return `
    <article id="driver-wallet" class="card driver-wallet-card">
      <div class="section-title"><div><span class="eyebrow">Cartera</span><h2>Saldo y recargas</h2><p class="muted">Panel preparado para cuando empiece el cobro de plataforma.</p></div><span class="wallet-mark" aria-hidden="true">$</span></div>
      <section class="wallet-balance-card">
        <div><span>Saldo HÁGALE</span><strong>${formatCop(platformBalance)}</strong><small>Demo: sin cobros activos todavía.</small></div>
        <div><span>Comisión pendiente</span><strong>${formatCop(platformCommissionPending)}</strong><small>Primer mes de lanzamiento: 0% comisión.</small></div>
      </section>
      <div class="performance-metrics wallet-metrics">
        <div><strong>${formatCop(completedValue)}</strong><span>Total finalizado</span></div>
        <div><strong>${formatCop(cashValue)}</strong><span>${cashCount} en efectivo</span></div>
        <div><strong>${formatCop(nequiValue)}</strong><span>${nequiCount} por Nequi</span></div>
        <div><strong>${formatCop(waitingValue)}</strong><span>Espera adicional</span></div>
      </div>
      <section class="wallet-topup-card">
        <div>
          <strong>Recargar cuenta</strong>
          <p>Medios previstos para producción; por ahora son informativos.</p>
        </div>
        <div class="payment-method-preview wallet-payment-methods">
          <span>Nequi</span><span>Daviplata</span><span>Bancolombia</span><span>PSE</span><span>Efecty</span>
        </div>
      </section>
      <div class="wallet-empty"><strong>Saldo de plataforma: no activado</strong><p>Por ahora el conductor cobra directo. Al conectar pasarela se separará saldo disponible, pendiente, recargas y retiros.</p></div>
      <p class="small-text muted">No se guarda información bancaria en esta versión.</p>
    </article>`;
}

function renderSafetyPanel() {
  const contacts = state.emergencyContacts || [];
  const contactList = contacts.length
    ? contacts.map(contact => `
      <li class="list-item">
        <div>
          <h3>${escapeHtml(contact.name)}</h3>
          <p>${escapeHtml(contact.relationship || "Contacto de confianza")} · ${escapeHtml(contact.phoneNumber)}</p>
        </div>
        <button class="button button-danger small" type="button" data-archive-emergency-contact="${contact.id}">Desactivar</button>
      </li>`).join("")
    : '<li class="empty">Aún no tienes contactos de confianza registrados.</li>';

  return `
    <article class="card">
      <span class="eyebrow">Seguridad</span>
      <h2>Contactos de confianza</h2>
      <p class="muted">Guarda personas de confianza para las próximas funciones de seguridad de HÁGALE.</p>
      <p class="callout">Por ahora no se comparten ubicaciones, no se realizan llamadas y no se envían alertas automáticas. Esas acciones solo se habilitarán con tu confirmación explícita durante un servicio.</p>
      <ul class="application-list">${contactList}</ul>
      <div class="divider"></div>
      <form id="emergency-contact-form" class="form-grid">
        <div class="field"><label for="emergency-contact-name">Nombre completo</label><input id="emergency-contact-name" name="name" minlength="2" maxlength="100" autocomplete="name" required></div>
        <div class="field"><label for="emergency-contact-phone">Celular internacional</label><input id="emergency-contact-phone" name="phoneNumber" inputmode="tel" placeholder="+573001234567" pattern="\\+[1-9]\\d{7,14}" required></div>
        <div class="field wide"><label for="emergency-contact-relationship">Relación (opcional)</label><input id="emergency-contact-relationship" name="relationship" maxlength="100" placeholder="Ej.: Hermana, amigo o acudiente"></div>
        <div class="button-row"><button class="button button-secondary" type="submit">Guardar contacto</button></div>
      </form>
    </article>`;
}

function renderCustomerRideTrackingPanel() {
  const rideRequest = getActiveCustomerRide();
  if (!rideRequest) return "";
  const tracking = state.customerRideTracking;
  const status = tracking?.status || rideRequest.status;
  const hasDriverLocation = Boolean(toMapCoordinate(tracking?.driverLatitude, tracking?.driverLongitude));
  const statusLabel = status === "InProgress"
    ? "Viaje en curso"
    : status === "DriverEnRoute"
      ? "Conductor en camino"
      : status === "DriverArrived"
        ? "Conductor llegó a recogida"
        : "Conductor asignado";

  return `
    <article id="customer-ride-tracking" class="card customer-tracking-card" data-reveal>
      <div class="customer-tracking-brand"><img class="hagale-logo-image tracking-logo-image" src="/assets/hagale-logo-yellow.png" alt="HÁGALE"><small>Seguimiento del servicio</small></div>
      <div class="section-title"><div><span class="eyebrow">Servicio activo</span><h2>${statusLabel}</h2><p class="muted" data-customer-tracking-status>${customerTrackingMessage(rideRequest, tracking)}</p></div>${renderPanelBackButton("Historial")}</div>
      ${renderCustomerStageAlert(status)}
      ${renderAssignedDriverSummary(tracking?.driver, status)}
      ${renderPrivateCommunicationCard(rideRequest)}
      <div class="customer-tracking-route"><div><span class="route-letter route-letter-a">A</span><p><small>Recogida</small><strong>${escapeHtml(rideRequest.pickupAddress)}</strong></p></div><div><span class="route-letter route-letter-b">B</span><p><small>Destino</small><strong>${escapeHtml(rideRequest.destinationAddress)}</strong></p></div></div>
      <div class="customer-tracking-map-frame"><div class="customer-tracking-map driver-map-canvas" data-customer-ride-map aria-label="Mapa del servicio activo">${hasDriverLocation ? "" : '<div class="customer-tracking-map-wait"><strong>Esperando GPS del conductor</strong><span>La moto aparecerá aquí cuando el conductor active la ubicación para este servicio.</span></div>'}</div></div>
      ${renderWaitingInformation(rideRequest, tracking, "customer")}
      ${renderJourneyTimeline({ ...rideRequest, ...tracking })}
      <div class="customer-tracking-actions"><button class="button button-secondary small" type="button" data-refresh-customer-tracking>Actualizar mapa</button><button class="button button-secondary small" type="button" data-test-customer-voice>🔊 Probar voz</button>${renderSafetyAssistButton("customer")}<span>${hasDriverLocation ? "Se muestra la última ubicación compartida." : "No se muestra ninguna ubicación hasta que el conductor la comparta."}</span></div>
      <p class="customer-tracking-privacy">La ruta amarilla/negra se calcula por calles entre la última ubicación compartida, A y B. Para indicaciones giro a giro, abre la navegación del teléfono.</p>
    </article>`;
}

function renderCustomerRideActions(rideRequest) {
  if (rideRequest.status === "Pending") {
    return `<button class="button button-danger small" type="button" data-cancel-ride="${rideRequest.id}">Cancelar</button>`;
  }

  if (rideRequest.status === "CounterOfferPending") {
    return `
      <button class="button button-primary small" type="button" data-counter-offer-decision="accept" data-counter-offer-ride="${rideRequest.id}">Aceptar precio</button>
      <button class="button button-secondary small" type="button" data-counter-offer-decision="reject" data-counter-offer-ride="${rideRequest.id}">Rechazar precio</button>
      <button class="button button-danger small" type="button" data-cancel-ride="${rideRequest.id}">Cancelar</button>`;
  }

  if (activeCustomerRideStatuses.has(rideRequest.status)) {
    return `<button class="button button-secondary small" type="button" data-customer-nav="tracking">Ver seguimiento</button>`;
  }

  return "";
}

function renderCustomerRideHistoryItem(rideRequest, { showTimeline = false } = {}) {
  const actions = renderCustomerRideActions(rideRequest);
  const statusClass = String(rideRequest.status || "unknown").toLowerCase();
  const requestedAt = rideRequest.requestedAtUtc ? formatDateTime(rideRequest.requestedAtUtc) : "Fecha pendiente";
  const finalLabel = getCustomerRideFinalLabel(rideRequest);

  return `
    <li class="customer-history-item is-${escapeHtml(statusClass)}">
      <div class="customer-history-status">
        ${statusBadge(rideRequest.status)}
        <span>${escapeHtml(finalLabel)}</span>
      </div>
      <div class="customer-history-main">
        <div class="customer-history-route">
          <p><span class="route-letter route-letter-a">A</span><strong>${escapeHtml(rideRequest.pickupAddress)}</strong></p>
          <p><span class="route-letter route-letter-b">B</span><strong>${escapeHtml(rideRequest.destinationAddress)}</strong></p>
        </div>
        <div class="customer-history-meta">
          <span>${escapeHtml(label[rideRequest.serviceType] || rideRequest.serviceType)}</span>
          <span>${escapeHtml(rideRequest.operatingCityCode)}</span>
          <span>${formatCop(rideRequest.proposedPriceCop)}</span>
          <span>${escapeHtml(getRidePaymentMethodLabel(rideRequest))}</span>
          <span>${escapeHtml(getRideFareModeLabel(rideRequest))}</span>
          <span>${requestedAt}</span>
        </div>
        ${rideRequest.counterOfferPriceCop ? `<p class="customer-history-note">Contraoferta: ${formatCop(rideRequest.counterOfferPriceCop)}.</p>` : ""}
        ${rideRequest.cancellationReason ? `<p class="customer-history-note">Cancelación: ${escapeHtml(rideRequest.cancellationReason)}</p>` : ""}
        ${showTimeline ? renderJourneyTimeline(rideRequest) : ""}
        ${renderRideRatingWidget(rideRequest, "customer")}
      </div>
      ${actions ? `<div class="customer-history-actions">${actions}</div>` : ""}
    </li>`;
}

function renderCustomerRideHistoryPanel() {
  const rideRequests = getSortedCustomerRideRequests();
  const openRequests = rideRequests.filter(rideRequest => openCustomerRideStatuses.has(rideRequest.status));
  const closedRequests = rideRequests.filter(rideRequest => !openCustomerRideStatuses.has(rideRequest.status));
  const completedCount = rideRequests.filter(rideRequest => rideRequest.status === "Completed").length;
  const cancelledCount = rideRequests.filter(rideRequest => rideRequest.status === "Cancelled").length;
  const completedValue = rideRequests
    .filter(rideRequest => rideRequest.status === "Completed")
    .reduce((total, rideRequest) => total + (Number(rideRequest.proposedPriceCop) || 0), 0);
  const openList = openRequests.length
    ? openRequests.map(rideRequest => renderCustomerRideHistoryItem(rideRequest, { showTimeline: true })).join("")
    : '<li class="empty">No tienes solicitudes activas en este momento.</li>';
  const closedList = closedRequests.length
    ? closedRequests.map(rideRequest => renderCustomerRideHistoryItem(rideRequest)).join("")
    : '<li class="empty">Cuando finalices o canceles viajes aparecerán aquí.</li>';

  return `
    <article id="customer-history" class="card customer-history-card" data-reveal>
      <div class="section-title">
        <div><span class="eyebrow">Cliente · historial</span><h2>Mis viajes y solicitudes</h2><p class="muted">Separado del panel para pedir moto. Aquí revisas servicios activos, cerrados, cancelados y contraofertas.</p></div>
        ${renderPanelBackButton("Pedir moto")}
      </div>
      <div class="customer-history-primary-action"><button class="button button-primary" type="button" data-customer-nav="ride">Pedir moto</button></div>
      <div class="customer-history-metrics">
        <div><strong>${rideRequests.length}</strong><span>Total solicitudes</span></div>
        <div><strong>${completedCount}</strong><span>Finalizadas</span></div>
        <div><strong>${cancelledCount}</strong><span>Canceladas</span></div>
        <div><strong>${formatCop(completedValue)}</strong><span>Valor finalizado</span></div>
      </div>
      <section class="customer-history-section">
        <div class="section-title compact"><div><h3>Activas o pendientes</h3><p class="muted small-text">Las solicitudes pendientes y contraofertas conservan sus acciones aquí.</p></div></div>
        <ul class="customer-history-list">${openList}</ul>
      </section>
      <section class="customer-history-section">
        <div class="section-title compact"><div><h3>Cerradas</h3><p class="muted small-text">Registro informativo de servicios finalizados o cancelados.</p></div></div>
        <ul class="customer-history-list">${closedList}</ul>
      </section>
    </article>`;
}

function renderCustomerLaunchOfferPanel() {
  const completedCount = (state.rideRequests || []).filter(rideRequest => rideRequest.status === "Completed").length;
  const pendingOrActiveCount = (state.rideRequests || []).filter(rideRequest => openCustomerRideStatuses.has(rideRequest.status)).length;
  const isFirstRideCandidate = completedCount === 0;
  return `
    <section class="customer-launch-offer ${isFirstRideCandidate ? "is-first-ride" : ""}">
      <div class="launch-offer-badge">
        <strong>${isFirstRideCandidate ? "50%" : "Bono"}</strong>
        <span>${isFirstRideCandidate ? "Primera carrera" : "Referidos"}</span>
      </div>
      <div>
        <span class="eyebrow">Campaña de lanzamiento</span>
        <h3>${isFirstRideCandidate ? "Bono bienvenida para motivar instalación" : "Trae un amigo y gana beneficios"}</h3>
        <p>${isFirstRideCandidate ? "Propuesta comercial: primera carrera con bono de hasta 50%. En esta demo se muestra como campaña, todavía no descuenta automáticamente." : "La fase de producción puede activar bonos por referidos, viajes frecuentes y zonas de lanzamiento."}</p>
        <small>${pendingOrActiveCount > 0 ? "Tienes una solicitud activa; el bono se revisaría al cerrar el servicio." : "Listo para probarlo en una nueva solicitud de muestra."}</small>
      </div>
    </section>`;
}

function saveCustomerRideDraft(patch = {}) {
  state.customerRideDraft = {
    ...getDefaultCustomerRideDraft(),
    ...(state.customerRideDraft || {}),
    ...patch
  };
  sessionStorage.setItem(customerRideDraftKey, JSON.stringify(state.customerRideDraft));
  return state.customerRideDraft;
}

function clearCustomerRideDraft() {
  state.customerRideDraft = getDefaultCustomerRideDraft();
  sessionStorage.removeItem(customerRideDraftKey);
}

function buildAddressWithNeighborhood(address, neighborhood) {
  const base = String(address || "").trim();
  const detail = String(neighborhood || "").trim();
  return detail ? `${base} · Barrio: ${detail}` : base;
}

function renderDraftAddressSummary(address, neighborhood) {
  const detail = String(neighborhood || "").trim();
  return `${escapeHtml(address || "")}${detail ? `<small>Barrio: ${escapeHtml(detail)}</small>` : ""}`;
}

function setCustomerRideStep(step) {
  const nextStep = step === "details" ? "details" : "locations";
  state.customerRideStep = nextStep;
  sessionStorage.setItem(customerRideStepKey, nextStep);
  renderDashboard();
  if (nextStep === "details") {
    window.setTimeout(() => app.querySelector("#ride-proposed-price")?.select(), 50);
    void refreshCustomerRideQuote();
  }
}

function renderCustomerRideStepIndicator(activeStep) {
  const steps = [
    { id: "locations", number: "1", title: "Direcciones" },
    { id: "details", number: "2", title: "Tarifa y opciones" },
    { id: "search", number: "3", title: "Búsqueda" }
  ];
  return `
    <ol class="customer-ride-stepper" aria-label="Pasos para pedir una moto">
      ${steps.map(step => `
        <li class="${activeStep === step.id ? "is-active" : activeStep === "search" || (activeStep === "details" && step.id === "locations") ? "is-complete" : ""}">
          <span>${step.number}</span>
          <strong>${step.title}</strong>
        </li>`).join("")}
    </ol>`;
}

function renderHelpDot(text) {
  return `<span class="help-dot" tabindex="0" title="${escapeHtml(text)}" aria-label="${escapeHtml(text)}">?</span>`;
}

function renderCustomerRideLocationsStep() {
  const draft = state.customerRideDraft || getDefaultCustomerRideDraft();
  return `
    <section class="customer-ride-stage customer-ride-stage-locations">
      ${renderCustomerRideStepIndicator("locations")}
      ${renderPanelBackButton("Historial")}
      <div class="customer-ride-stage-heading">
        <span class="eyebrow">Pantalla 1 · ruta</span>
        <h3>¿De dónde y hacia dónde? ${renderHelpDot("Primero guarda A y B. Después revisas precio, pago y tipo de tarifa.")}</h3>
        <p>Dirección, barrio y referencia para ubicar mejor al conductor.</p>
      </div>
      <form id="ride-location-step-form" class="form-grid ride-request-form customer-ride-form">
        <div class="field wide route-entry route-entry-a">
          <label for="ride-pickup"><span aria-hidden="true">A</span> Punto de recogida</label>
          <input id="ride-pickup" name="pickupAddress" value="${escapeHtml(draft.pickupAddress)}" minlength="5" maxlength="250" autocomplete="street-address" placeholder="Ej.: Calle 72 # 10-07" required>
          <label class="sub-field-label" for="ride-pickup-neighborhood">Barrio</label>
          <input id="ride-pickup-neighborhood" name="pickupNeighborhood" value="${escapeHtml(draft.pickupNeighborhood)}" maxlength="120" autocomplete="address-level3" placeholder="Ej.: Cabecera">
          <div class="location-actions">
            <button class="location-button" type="button" data-capture-pickup-location>Mi ubicación</button>
            <button class="location-button" type="button" data-open-ride-map="pickup">Marcar en mapa</button>
          </div>
          <span id="pickup-location-status" class="small-text muted">${state.pendingPickupLocation ? "Punto A listo para esta solicitud." : "Escribe la dirección o toca el mapa para ubicar A."}</span>
        </div>
        <div class="field wide route-entry route-entry-b">
          <label for="ride-destination"><span aria-hidden="true">B</span> Destino</label>
          <input id="ride-destination" name="destinationAddress" value="${escapeHtml(draft.destinationAddress)}" minlength="5" maxlength="250" autocomplete="street-address" placeholder="Ej.: Centro Comercial Cacique" required>
          <label class="sub-field-label" for="ride-destination-neighborhood">Barrio del destino</label>
          <input id="ride-destination-neighborhood" name="destinationNeighborhood" value="${escapeHtml(draft.destinationNeighborhood)}" maxlength="120" autocomplete="address-level3" placeholder="Ej.: Provenza">
          <div class="location-actions">
            <button class="location-button" type="button" data-capture-destination-location>Mi ubicación</button>
            <button class="location-button" type="button" data-open-ride-map="destination">Marcar en mapa</button>
          </div>
          <span id="destination-location-status" class="small-text muted">${state.pendingDestinationLocation ? "Punto B listo para esta solicitud." : "Escribe la dirección o toca el mapa para ubicar B."}</span>
        </div>
        <section class="ride-location-picker ride-map-stage" data-ride-location-picker hidden aria-live="polite">
          <div class="ride-location-picker-heading">
            <div><span class="eyebrow">Mapa de la solicitud</span><h3>Elige A y B en el mapa</h3><p class="muted small-text" data-ride-map-instruction>Toca el mapa para ubicar el origen (A).</p></div>
            <button class="button button-quiet small" type="button" data-close-ride-map>Cerrar mapa</button>
          </div>
          <div class="ride-map-targets" role="group" aria-label="Punto que se va a marcar">
            <button class="button button-secondary small is-selected" type="button" data-select-ride-map-target="pickup" aria-pressed="true">A · Origen</button>
            <button class="button button-secondary small" type="button" data-select-ride-map-target="destination" aria-pressed="false">B · Destino</button>
          </div>
          <div class="ride-location-map-frame"><div class="ride-location-map" data-ride-location-map aria-label="Mapa para elegir origen o destino"></div></div>
          <p class="small-text muted">El mapa es opcional. Si editas una dirección después de marcarla, el punto se quitará para evitar enviar una ubicación equivocada.</p>
        </section>
        <div class="button-row customer-request-actions">
          <button class="button button-primary customer-next-step" type="submit">Continuar con tarifa <span aria-hidden="true">→</span></button>
        </div>
      </form>
    </section>`;
}

function renderCustomerRideDetailsStep(activePricingRules) {
  const draft = state.customerRideDraft || getDefaultCustomerRideDraft();
  const firstRule = activePricingRules[0];
  const defaultRuleKey = firstRule ? `${firstRule.cityCode}|${firstRule.serviceType}` : "";
  const selectedRuleKey = draft.pricingRuleKey || defaultRuleKey;
  const selectedRule = activePricingRules.find(rule => `${rule.cityCode}|${rule.serviceType}` === selectedRuleKey) || firstRule;
  const selectedRuleValue = selectedRule ? `${selectedRule.cityCode}|${selectedRule.serviceType}` : selectedRuleKey;
  const initialMinimumFare = Number(selectedRule?.minimumFareCop || 0);
  const savedPrice = Number(draft.proposedPriceCop);
  const proposedPrice = Number.isFinite(savedPrice) && savedPrice >= initialMinimumFare ? savedPrice : initialMinimumFare;
  const pricingOptions = activePricingRules.map(rule => {
    const value = `${rule.cityCode}|${rule.serviceType}`;
    return `<option value="${escapeHtml(value)}" data-minimum-fare="${rule.minimumFareCop}" ${value === selectedRuleValue ? "selected" : ""}>${escapeHtml(rule.cityCode)} · ${escapeHtml(label[rule.serviceType] || rule.serviceType)} · mínimo ${formatCop(rule.minimumFareCop)}</option>`;
  }).join("");
  const paymentMethod = draft.paymentMethod || "Cash";
  const fareMode = draft.fareMode || "DynamicFare";

  return `
    <section class="customer-ride-stage customer-ride-stage-details">
      ${renderCustomerRideStepIndicator("details")}
      ${renderPanelBackButton("Direcciones")}
      <div class="customer-ride-stage-heading">
        <span class="eyebrow">Pantalla 2 · propuesta</span>
        <h3>Define cómo quieres viajar ${renderHelpDot("La tarifa puede ser mínima, recomendada por distancia o una oferta mayor para atraer conductores.")}</h3>
        <p>Precio fuerte, pago claro y condiciones antes de enviar.</p>
      </div>
      <div class="customer-route-summary" aria-label="Resumen del recorrido">
        <div><span class="route-letter route-letter-a">A</span><p><small>Recogida</small><strong>${renderDraftAddressSummary(draft.pickupAddress, draft.pickupNeighborhood)}</strong></p></div>
        <div class="customer-route-summary-line" aria-hidden="true"></div>
        <div><span class="route-letter route-letter-b">B</span><p><small>Destino</small><strong>${renderDraftAddressSummary(draft.destinationAddress, draft.destinationNeighborhood)}</strong></p></div>
        <button class="button button-secondary small" type="button" data-customer-ride-step="locations">← Editar direcciones</button>
      </div>
      <form id="ride-request-form" class="form-grid ride-request-form customer-ride-form">
        <div class="field wide"><label for="ride-pricing-rule">Ciudad y servicio</label><select id="ride-pricing-rule" name="pricingRuleKey">${pricingOptions}</select></div>
        <div class="field wide ride-price-field ride-price-focus"><label for="ride-proposed-price">Escribe tu tarifa</label><input id="ride-proposed-price" name="proposedPriceCop" type="number" min="${initialMinimumFare}" step="1" value="${proposedPrice}" inputmode="numeric" autofocus required><span class="small-text">Valor sugerido: ${formatCop(proposedPrice)}. Puedes escribirlo de una vez.</span></div>
        <p id="minimum-fare-hint" class="callout wide">Mínimo ${formatCop(initialMinimumFare)}. Marca A y B en el mapa para calcular la tarifa sugerida por distancia.</p>
        <p id="ride-price-reference" class="ride-price-reference wide" aria-live="polite">${renderCustomerRideQuote()}</p>
        <fieldset class="ride-options-card wide">
          <legend>Pago y tarifa</legend>
          <p>El conductor verá esta forma de pago junto con el valor.</p>
          <div class="ride-choice-grid" role="group" aria-label="Método de pago">
            <label class="ride-choice-pill"><input type="radio" name="paymentMethod" value="Cash" ${paymentMethod === "Cash" ? "checked" : ""}><span>💵 Efectivo</span><small>Pago directo al conductor.</small></label>
            <label class="ride-choice-pill"><input type="radio" name="paymentMethod" value="Nequi" ${paymentMethod === "Nequi" ? "checked" : ""}><span>Nequi</span><small>Preferencia visible; el pago real se integrará después.</small></label>
          </div>
          <div class="payment-method-preview">
            <strong>Próximos pagos:</strong>
            <span>PSE</span><span>Tarjeta</span><span>Daviplata</span><span>Bancolombia</span>
          </div>
          <div class="ride-choice-grid" role="group" aria-label="Modo de tarifa">
            <label class="ride-choice-pill"><input type="radio" name="fareMode" value="PassengerOffer" ${fareMode === "PassengerOffer" ? "checked" : ""}><span>Tu oferta</span><small>El pasajero propone el valor.</small></label>
            <label class="ride-choice-pill"><input type="radio" name="fareMode" value="DynamicFare" ${fareMode === "DynamicFare" ? "checked" : ""}><span>Tarifa dinámica</span><small>Base preparada para cálculo automático.</small></label>
          </div>
        </fieldset>
        <div class="button-row customer-request-actions">
          <button class="button button-primary customer-submit-request" type="submit">Verificar y pedir moto <span aria-hidden="true">→</span></button>
        </div>
      </form>
    </section>`;
}

function renderCustomerSearchingPanel(rideRequest) {
  return `
    <section class="customer-search-card" aria-live="polite">
      <div class="customer-search-map" aria-hidden="true">
        <span class="search-map-road search-map-road-one"></span><span class="search-map-road search-map-road-two"></span>
        <span class="search-map-pin search-map-pin-a">A</span><span class="search-map-pin search-map-pin-b">B</span>
        <span class="search-map-pulse"></span><span class="search-map-bike">🏍</span>
      </div>
      <div class="customer-search-content">
        ${renderCustomerRideStepIndicator("search")}
        <span class="eyebrow">Pantalla 3 · despacho</span>
        <h3>Buscando conductor</h3>
        <p class="customer-search-lead">Tu solicitud ya fue enviada. HÁGALE la mostrará aquí cuando un conductor la vea o acepte.</p>
        <div class="customer-search-route">
          <div><span class="route-letter route-letter-a">A</span><strong>${escapeHtml(rideRequest.pickupAddress)}</strong></div>
          <div><span class="route-letter route-letter-b">B</span><strong>${escapeHtml(rideRequest.destinationAddress)}</strong></div>
        </div>
        <div class="customer-search-progress"><div><span>Despacho activo</span><strong>Esperando respuestas</strong></div><span class="customer-search-progress-track"><i></i></span></div>
        <div class="customer-search-offer"><span>Oferta enviada</span><strong>${formatCop(rideRequest.proposedPriceCop)}</strong><small>${escapeHtml(getRidePaymentMethodLabel(rideRequest))} · ${escapeHtml(getRideFareModeLabel(rideRequest))}</small></div>
        <div class="customer-search-actions">
          <button class="button button-secondary small" type="button" data-refresh-customer-status>Actualizar estado</button>
          <button class="button button-danger small" type="button" data-cancel-ride="${rideRequest.id}">Cancelar solicitud</button>
          <button class="button button-quiet small" type="button" data-customer-nav="history">Ver historial</button>
        </div>
      </div>
    </section>`;
}

function renderCustomerCounterOfferPanel(rideRequest) {
  const counterOffer = rideRequest.counterOfferPriceCop || rideRequest.proposedPriceCop;
  return `
    <section class="customer-counteroffer-card" aria-live="polite">
      <div class="customer-counteroffer-icon" aria-hidden="true">↔</div>
      <div class="customer-counteroffer-content">
        ${renderCustomerRideStepIndicator("search")}
        <span class="eyebrow">Respuesta del conductor</span>
        <h3>Tienes una propuesta para revisar</h3>
        <p>El conductor respondió a tu solicitud. Revisa el valor y decide antes de que se cierre esta oferta.</p>
        <div class="customer-counteroffer-route"><span>A</span><strong>${escapeHtml(rideRequest.pickupAddress)}</strong><span>B</span><strong>${escapeHtml(rideRequest.destinationAddress)}</strong></div>
        <div class="customer-counteroffer-price"><span>Contraoferta</span><strong>${formatCop(counterOffer)}</strong><small>Tu oferta original: ${formatCop(rideRequest.proposedPriceCop)}</small></div>
        <div class="customer-search-actions">${renderCustomerRideActions(rideRequest)}</div>
      </div>
    </section>`;
}

function renderRideRequestPanel() {
  const openRequest = getOpenCustomerRideRequest();
  const activePricingRules = (state.pricingRules || []).filter(rule => rule.isActive);
  const requestForm = openRequest
    ? openRequest.status === "Pending"
      ? renderCustomerSearchingPanel(openRequest)
      : openRequest.status === "CounterOfferPending"
        ? renderCustomerCounterOfferPanel(openRequest)
        : `<div class="request-in-progress"><div><strong>Servicio activo</strong><p>El seguimiento del viaje está separado en su propia pantalla para que solo veas lo importante.</p></div><div class="request-in-progress-actions">${statusBadge(openRequest.status)}<button class="button button-secondary small" type="button" data-customer-nav="tracking">Ver seguimiento</button><button class="button button-secondary small" type="button" data-customer-nav="history">Ver historial</button></div></div>`
    : activePricingRules.length
      ? state.customerRideStep === "details"
        ? renderCustomerRideDetailsStep(activePricingRules)
        : renderCustomerRideLocationsStep()
      : '<p class="empty">Todavía no hay una tarifa activa para solicitar este servicio. Un administrador debe configurarla primero.</p>';
  const stageTitle = openRequest
    ? openRequest.status === "Pending"
      ? "Tu solicitud está en búsqueda"
      : openRequest.status === "CounterOfferPending"
        ? "Revisa la respuesta"
        : "Tu solicitud actual"
    : state.customerRideStep === "details"
      ? "Elige tarifa y confirma"
      : "Primero ubica tu recorrido";
  const stageDescription = openRequest
    ? "Cada momento del servicio tiene su propia pantalla: búsqueda, respuesta del conductor y seguimiento."
    : "HÁGALE separa las decisiones para que pedir una moto sea rápido y fácil de operar.";

  return `
    <article id="customer-ride-request" class="card customer-request-card" data-reveal>
      <span class="eyebrow">HÁGALE · solicitar moto</span>
      <h2>${stageTitle}</h2>
      <p class="muted">${stageDescription}</p>
      ${requestForm}
      <div class="customer-request-footer"><button class="button button-secondary small" type="button" data-customer-nav="history">Ver historial completo</button><span>El historial está separado para que pedir una moto sea un panel limpio.</span></div>
    </article>`;
}

function renderDriverPanel(driver, isDriver) {
  if (!driver) {
    return `
      <article class="card">
        <span class="eyebrow">Conduce con HÁGALE</span>
        <h2>¿Quieres ser conductor?</h2>
        <p class="muted">Envía tu solicitud. Luego registra tu moto y los documentos obligatorios para que un administrador pueda revisarlos.</p>
        <button class="button button-primary" type="button" data-apply-driver>Solicitar habilitación como conductor</button>
      </article>`;
  }

  if (isDriver && driver.status === "Approved") {
    return renderDriverSettingsPanel(driver);
  }

  const vehicles = driver.vehicles.length
    ? driver.vehicles.map(vehicle => `<li class="list-item"><div><h3>${escapeHtml(vehicle.brand)} ${escapeHtml(vehicle.model)}</h3><p>${escapeHtml(vehicle.color)} · Placa ${escapeHtml(vehicle.plate)} · ${vehicle.year} · ${escapeHtml(vehicle.operatingCityCode)}</p></div><div class="button-row">${vehicle.isActive ? statusBadge("Active") : statusBadge("Inactive")}<button class="button button-secondary small" type="button" data-edit-vehicle="${vehicle.id}">Editar</button></div></li>`).join("")
    : '<li class="empty">Aún no has registrado un vehículo.</li>';
  const documents = renderDriverRequiredDocumentList(driver);
  const missingDocumentTypes = getMissingRequiredDriverDocumentTypes(driver);
  const missingDocumentLabel = missingDocumentTypes.map(type => label[type] || type).join(", ");
  const canToggleAvailability = isDriver && driver.status === "Approved";
  const vehicleEditor = driver.vehicles.length
    ? driver.vehicles.map(vehicle => renderVehicleForm(vehicle)).join("")
    : renderVehicleForm();
  const editorTitle = driver.vehicles.length ? "Editar datos de la moto" : "Completar datos de la moto";
  const editorHint = driver.vehicles.length
    ? "Si escribiste algo mal, corrígelo aquí. El cambio quedará registrado para validación administrativa."
    : "Completa estos datos para enviar tu solicitud a revisión.";
  const driverProgress = driver.status === "Approved"
    ? "Solicitud aprobada. Cierra sesión y vuelve a ingresar para activar el Modo conductor."
    : missingDocumentTypes.length > 0
      ? `Falta aprobación de: ${missingDocumentLabel}. Carga o corrige esos documentos para que Administración pueda revisarlos.`
      : driver.status === "UnderReview"
        ? "Tus documentos están en revisión administrativa. Cuando todos sean aprobados, Administración podrá activar tu rol de conductor."
        : "Completa los datos pendientes y espera la revisión administrativa.";

  return `
    <article class="card">
      <div class="status-row">
        <div><span class="eyebrow">Conductor</span><h2>Solicitud de habilitación</h2><p class="muted">Enviada el ${new Date(driver.appliedAtUtc).toLocaleDateString("es-CO")}.</p></div>
        <div class="role-list">${statusBadge(driver.status)} ${statusBadge(driver.availabilityStatus)}</div>
      </div>
      ${driver.administrativeNotes ? `<p class="callout">${escapeHtml(driver.administrativeNotes)}</p>` : ""}
      <p class="driver-progress"><strong>Qué falta:</strong> ${driverProgress}</p>
      <div class="divider"></div>
      <div class="stack">
        <div>
          <div class="section-title"><div><h3>Vehículos</h3><p class="muted small-text">Debes tener un vehículo activo para estar disponible.</p></div></div>
          <ul class="vehicle-list">${vehicles}</ul>
        </div>

        <div>
          <div class="section-title"><div><h3>Documentos obligatorios</h3><p class="muted small-text">PDF, JPG o PNG. Máximo 5 MB. Debes cargarlos para que Administración pueda aprobarte.</p></div></div>
          <ul class="document-list">${documents}</ul>
          ${renderAdditionalDriverDocumentList(driver)}
          ${renderDriverDocumentUploadCards(driver)}
        </div>

        <div>
          <h3>Disponibilidad</h3>
          <p class="muted small-text">${canToggleAvailability ? "Elige cuándo estás disponible. El estado ocupado solo lo gestionarán los viajes." : "Se habilitará cuando la solicitud esté aprobada y tu cuenta reciba el rol de conductor."}</p>
          ${renderDriverAvailabilitySlider(driver)}
        </div>
      </div>
      <details class="edit-panel" ${driver.vehicles.length ? "" : "open"}>
        <summary>${editorTitle}</summary>
        <p class="small-text">${editorHint}</p>
        <div class="stack">${vehicleEditor}</div>
      </details>
    </article>`;
}

function renderDriverSettingsPanel(driver) {
  const vehicles = driver.vehicles.length
    ? driver.vehicles.map(vehicle => `<li class="list-item"><div><h3>${escapeHtml(vehicle.brand)} ${escapeHtml(vehicle.model)}</h3><p>${escapeHtml(vehicle.color)} · Placa ${escapeHtml(vehicle.plate)} · ${vehicle.year} · ${escapeHtml(vehicle.operatingCityCode)}</p></div><div class="button-row">${vehicle.isActive ? statusBadge("Active") : statusBadge("Inactive")}<button class="button button-secondary small" type="button" data-edit-vehicle="${vehicle.id}">Editar</button></div></li>`).join("")
    : '<li class="empty">Aún no has registrado un vehículo.</li>';
  const documents = renderDriverRequiredDocumentList(driver);
  const vehicleEditor = driver.vehicles.length
    ? driver.vehicles.map(vehicle => renderVehicleForm(vehicle)).join("")
    : renderVehicleForm();

  return `
    <article id="driver-settings" class="card driver-settings-card">
      <div class="section-title"><div><span class="eyebrow">Configuración del conductor</span><h2>Tu moto y documentos</h2><p class="muted">Esta información permanece separada del tablero de solicitudes. Puedes corregir la moto cuando lo necesites.</p></div>${statusBadge(driver.status)}</div>
      <div class="driver-settings-grid">
        <div><h3>Vehículo activo</h3><ul class="vehicle-list">${vehicles}</ul></div>
        <div><h3>Documentos revisados</h3><ul class="document-list">${documents}</ul>${renderAdditionalDriverDocumentList(driver)}${renderDriverDocumentUploadCards(driver)}</div>
      </div>
      <details class="edit-panel">
        <summary>Editar datos de la moto</summary>
        <p class="small-text">Los cambios importantes pueden requerir una nueva validación administrativa.</p>
        <div class="stack">${vehicleEditor}</div>
      </details>
    </article>`;
}

function renderVehicleForm(vehicle = null) {
  const id = vehicle ? ` data-update-vehicle="${vehicle.id}"` : " id=\"vehicle-form\"";
  const buttonText = vehicle ? "Guardar cambios de moto" : "Guardar moto";
  return `<form class="form-grid vehicle-editor-form"${id}>
    <div class="field"><label>Marca</label><input name="brand" value="${escapeHtml(vehicle?.brand || "")}" required></div>
    <div class="field"><label>Modelo</label><input name="model" value="${escapeHtml(vehicle?.model || "")}" required></div>
    <div class="field"><label>Año</label><input name="year" type="number" min="1900" max="2100" value="${vehicle?.year || 2025}" required></div>
    <div class="field"><label>Color</label><input name="color" value="${escapeHtml(vehicle?.color || "")}" required></div>
    <div class="field"><label>Placa</label><input name="plate" maxlength="10" value="${escapeHtml(vehicle?.plate || "")}" required></div>
    <div class="field"><label>Ciudad</label><input name="operatingCityCode" maxlength="20" value="${escapeHtml(vehicle?.operatingCityCode || "BOG")}" required></div>
    <input name="type" type="hidden" value="${escapeHtml(vehicle?.type || "Motorcycle")}">
    <div class="button-row"><button class="button button-secondary" type="submit">${buttonText}</button></div>
  </form>`;
}

function getDeviceLocationErrorMessage(error) {
  if (error?.code === 1) {
    return "No se concedió permiso de ubicación. En el candado de la barra del navegador permite Ubicación para HÁGALE y vuelve a tocar Usar GPS.";
  }
  if (error?.code === 2) {
    return "El teléfono no puede determinar tu ubicación. Activa Ubicación/GPS y datos o Wi‑Fi, y prueba nuevamente.";
  }
  if (error?.code === 3) {
    return "El GPS tardó demasiado. Muévete a una zona con mejor señal o elige el punto directamente en el mapa.";
  }
  return "No fue posible obtener la ubicación en este momento.";
}

function getCurrentDevicePosition(options) {
  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, options);
  });
}

async function readDeviceLocation() {
  if (!navigator.geolocation) {
    throw new Error("Este navegador no ofrece GPS. Abre HÁGALE en Chrome, Firefox o Safari y permite la ubicación.");
  }
  if (!window.isSecureContext && window.location.hostname !== "localhost") {
    throw new Error("El GPS del teléfono requiere abrir HÁGALE mediante HTTPS; la dirección HTTP local solo muestra la interfaz.");
  }

  try {
    const precisePosition = await getCurrentDevicePosition({
      enableHighAccuracy: true,
      maximumAge: 15_000,
      timeout: 12_000
    });
    return precisePosition.coords;
  } catch (error) {
    if (error?.code !== 3) throw new Error(getDeviceLocationErrorMessage(error));
    try {
      const approximatePosition = await getCurrentDevicePosition({
        enableHighAccuracy: false,
        maximumAge: 60_000,
        timeout: 10_000
      });
      showNotice("Usamos una ubicación aproximada porque la señal GPS precisa tardó.");
      return approximatePosition.coords;
    } catch (fallbackError) {
      throw new Error(getDeviceLocationErrorMessage(fallbackError));
    }
  }
}

async function diagnoseDeviceLocation() {
  const buttons = [...app.querySelectorAll("[data-diagnose-gps]")];
  const statuses = [...app.querySelectorAll("[data-gps-diagnostic-status]")];
  buttons.forEach(button => { button.disabled = true; });
  statuses.forEach(status => { status.textContent = "Comprobando permiso y señal…"; });
  try {
    const coords = await readDeviceLocation();
    const accuracy = Number.isFinite(coords.accuracy) ? Math.round(coords.accuracy) : null;
    const message = accuracy
      ? `GPS listo · precisión aproximada ${accuracy} m.`
      : "GPS listo · ubicación recibida.";
    statuses.forEach(status => { status.textContent = message; });
    showNotice(message);
  } catch (error) {
    const message = error.message || "No se pudo comprobar el GPS.";
    statuses.forEach(status => { status.textContent = message; });
  } finally {
    buttons.forEach(button => { button.disabled = false; });
  }
}

async function captureRideLocation(target) {
  const isDestination = target === "destination";
  const button = app.querySelector(`[data-capture-${isDestination ? "destination" : "pickup"}-location]`);
  const status = app.querySelector(`#${isDestination ? "destination" : "pickup"}-location-status`);
  if (button) button.disabled = true;
  if (status) status.textContent = "Solicitando ubicación…";

  try {
    const coords = await readDeviceLocation();
    const latitude = Number(coords.latitude.toFixed(6));
    const longitude = Number(coords.longitude.toFixed(6));
    if (isDestination) {
      state.pendingDestinationLocation = { destinationLatitude: latitude, destinationLongitude: longitude };
    } else {
      state.pendingPickupLocation = { pickupLatitude: latitude, pickupLongitude: longitude };
    }
    if (status) status.textContent = isDestination
      ? "Punto B listo para esta solicitud."
      : "Punto A listo para esta solicitud.";
    if (!app.querySelector("[data-ride-location-picker]")?.hidden) mountCustomerRideMap();
    void refreshCustomerRideQuote();
    showNotice(isDestination ? "Ubicación de destino preparada." : "Ubicación de recogida preparada.");
  } catch (error) {
    if (status) status.textContent = error.message;
    showNotice(error.message, true);
  } finally {
    if (button) button.disabled = false;
  }
}

function capturePickupLocation() {
  return captureRideLocation("pickup");
}

async function updateDispatchLocation() {
  const buttons = [...app.querySelectorAll("[data-update-dispatch-location]")];
  buttons.forEach(button => { button.disabled = true; });
  try {
    const coords = await readDeviceLocation();
    const location = {
      latitude: Number(coords.latitude.toFixed(6)),
      longitude: Number(coords.longitude.toFixed(6))
    };
    await saveDriverLocation(location);
    state.lastDriverLocationSentAt = Date.now();
    await refreshDriverDispatch();
    startDriverLocationTracking();
    renderDashboard();
    showNotice("Ubicación actualizada. Ordenamos las solicitudes por cercanía.");
  } catch (error) {
    showNotice(error.message, true);
  } finally {
    buttons.forEach(button => { button.disabled = false; });
  }
}

function setDispatchRadius(radius) {
  const normalizedRadius = Number(radius);
  if (![3, 5, 8, 15].includes(normalizedRadius)) return;
  state.dispatchRadiusKilometers = normalizedRadius;
  sessionStorage.setItem(dispatchRadiusKey, String(normalizedRadius));
  refreshDriverDispatch()
    .then(() => renderDashboard())
    .catch(error => showNotice(error.message, true));
}

function dismissDriverOffer(rideRequestId) {
  state.hiddenDriverOfferIds.add(rideRequestId);
  if (state.selectedDriverOfferId === rideRequestId) state.selectedDriverOfferId = null;
  renderDashboard();
  showNotice("La solicitud quedó oculta por ahora; seguirá disponible para otros conductores.");
}

function selectDriverOffer(rideRequestId) {
  const offer = state.driverRideOffers.find(item => item.id === rideRequestId);
  if (!offer) return;
  state.selectedDriverOfferId = offer.id;
  renderDashboard();
  app.querySelector(".driver-request-sheet")?.scrollIntoView({ behavior: "smooth", block: "start" });
}

function closeDriverOffer() {
  if (!state.selectedDriverOfferId) return;
  state.selectedDriverOfferId = null;
  renderDashboard();
  app.querySelector("#driver-requests")?.scrollIntoView({ behavior: "smooth", block: "start" });
}

function navigateDriverPanel(destination) {
  const allowedDestinations = ["requests", "dispatch", "performance", "wallet", "settings", "account"];
  if (!allowedDestinations.includes(destination)) return;
  state.driverNav = destination;
  renderDashboard();
  app.scrollIntoView({ behavior: "smooth", block: "start" });
}

function goBackPanel() {
  if (state.activeMode === "Driver") {
    if ((state.driverNav || "requests") !== "requests") {
      navigateDriverPanel("requests");
    } else {
      setActiveMode("Customer");
    }
    return;
  }

  if (state.customerNav === "ride" && state.customerRideStep === "details") {
    setCustomerRideStep("locations");
    return;
  }
  const previousPanel = {
    tracking: "history",
    history: "ride",
    profile: "ride",
    safety: "ride",
    driver: "ride",
    admin: "ride"
  }[state.customerNav] || "ride";
  state.customerNav = previousPanel;
  sessionStorage.setItem(customerNavKey, previousPanel);
  renderDashboard();
  app.scrollIntoView({ behavior: "smooth", block: "start" });
}

function renderPlatformAppearanceAdminPanel() {
  const appearance = getPlatformAppearance();
  const field = (id, labelText, name, value, type = "text") =>
    '<div class="field"><label for="' + id + '">' + labelText + '</label><input id="' + id + '" name="' + name + '" type="' + type + '" value="' + escapeHtml(value) + '" required></div>';
  return '<article class="card wide design-center-card">' +
    '<span class="eyebrow">Administración · diseño</span><h2>Centro de diseño</h2>' +
    '<p class="muted small-text">Cambia los colores, nombres de estados y el aviso de voz desde aquí. Los cambios quedan guardados para todos los usuarios.</p>' +
    '<form id="platform-appearance-form" class="stack">' +
      '<div class="form-grid design-color-grid">' +
        field("appearance-accent", "Amarillo de marca", "accentColor", appearance.accentColor, "color") +
        field("appearance-action", "Verde de acciones", "actionColor", appearance.actionColor, "color") +
        field("appearance-busy", "Rojo OCUPADO", "busyColor", appearance.busyColor, "color") +
      '</div>' +
      '<div class="form-grid">' +
        field("appearance-customer-label", "Botón de cliente", "customerModeLabel", appearance.customerModeLabel) +
        field("appearance-driver-label", "Botón de conductor", "driverModeLabel", appearance.driverModeLabel) +
        field("appearance-free-label", "Estado disponible", "freeStatusLabel", appearance.freeStatusLabel) +
        field("appearance-busy-label", "Estado sin solicitudes", "busyStatusLabel", appearance.busyStatusLabel) +
        field("appearance-request-label", "Botón para pedir", "requestActionLabel", appearance.requestActionLabel) +
      '</div>' +
      '<div class="field"><label for="appearance-voice-template">Plantilla de voz del conductor</label><textarea id="appearance-voice-template" name="driverOfferVoiceTemplate" rows="3" maxlength="500" required>' + escapeHtml(appearance.driverOfferVoiceTemplate) + '</textarea><small class="muted">Variables disponibles: {origen}, {destino}, {valor}, {distancia}, {tiempo}.</small></div>' +
      '<div class="button-row"><button class="button button-primary" type="submit">Guardar diseño</button><span class="muted small-text">Se aplica al recargar el panel.</span></div>' +
    '</form></article>';
}
function renderAdminPanel() {
  const page = state.adminApplications;
  const items = page?.items || [];
  const list = items.length
    ? items.map(renderAdminApplication).join("")
    : '<li class="empty">No hay solicitudes con ese estado.</li>';

  return `
    <article id="admin-center" class="card wide admin-center-card">
      <span class="eyebrow">Administración</span>
      <h2>Centro de administración</h2>
      <h3>Bandeja de solicitudes</h3>
      <p class="muted small-text">Mostrando ${page?.totalCount || 0} solicitud(es). Revisa los documentos antes de aprobar una solicitud.</p>
      <div class="admin-controls">
        <div class="field"><label for="admin-status">Filtrar por estado</label><select id="admin-status"><option value="All" ${selected(state.adminStatus, "All")}>Todos</option><option value="Pending" ${selected(state.adminStatus, "Pending")}>Pendiente</option><option value="UnderReview" ${selected(state.adminStatus, "UnderReview")}>En revisión</option><option value="Approved" ${selected(state.adminStatus, "Approved")}>Aprobada</option><option value="Rejected" ${selected(state.adminStatus, "Rejected")}>Rechazada</option></select></div>
        <button class="button button-secondary" type="button" data-refresh-admin>Actualizar bandeja</button>
      </div>
      <ul class="application-list">${list}</ul>
    </article>
    ${renderPlatformAppearanceAdminPanel()}
    ${renderPricingAdminPanel()}
    ${renderEmergencyChannelsAdminPanel()}`;
}

async function updatePlatformAppearance(event) {
  event.preventDefault();
  try {
    state.platformAppearance = await request("/platform-appearance", {
      method: "PUT",
      data: Object.fromEntries(new FormData(event.currentTarget))
    });
    applyVisualMode();
    renderDashboard();
    showNotice("Diseño guardado para toda la plataforma.");
  } catch (error) {
    showNotice(error.message, true);
  }
}
function selected(value, expected) {
  return value === expected ? "selected" : "";
}

function renderServiceTypeOptions(selectedServiceType = "Motorcycle") {
  return ["Motorcycle", "MotorcyclePremium", "MotorcycleFuturisticPremium", "Delivery", "DeliveryPlus"]
    .map(serviceType => `<option value="${serviceType}" ${selected(selectedServiceType, serviceType)}>${label[serviceType]}</option>`)
    .join("");
}

function renderPricingAdminPanel() {
  const rules = state.pricingRules || [];
  const ruleForms = rules.length
    ? rules.map(rule => `
      <form class="pricing-rule-form card" data-pricing-rule-id="${rule.id}">
        <div class="section-title"><div><span class="eyebrow">Tarifa activa</span><h3>${escapeHtml(rule.cityCode)} · ${escapeHtml(label[rule.serviceType] || rule.serviceType)}</h3></div>${rule.isActive ? statusBadge("Approved") : statusBadge("Inactive")}</div>
        <div class="form-grid">
          <div class="field"><label for="pricing-minimum-${rule.id}">Tarifa mínima COP</label><input id="pricing-minimum-${rule.id}" name="minimumFareCop" type="number" min="1" value="${rule.minimumFareCop}" required></div>
          <div class="field"><label for="pricing-base-${rule.id}">Base COP</label><input id="pricing-base-${rule.id}" name="baseFareCop" type="number" min="0" value="${rule.baseFareCop}" required></div>
          <div class="field"><label for="pricing-kilometer-${rule.id}">COP por km</label><input id="pricing-kilometer-${rule.id}" name="farePerKilometerCop" type="number" min="0" value="${rule.farePerKilometerCop}" required></div>
          <div class="field"><label for="pricing-minute-${rule.id}">COP por minuto</label><input id="pricing-minute-${rule.id}" name="farePerMinuteCop" type="number" min="0" value="${rule.farePerMinuteCop}" required></div>
          <div class="field"><label for="pricing-fair-${rule.id}">Oferta justa desde (%)</label><input id="pricing-fair-${rule.id}" name="fairOfferMinimumPercent" type="number" min="1" max="1000" value="${rule.fairOfferMinimumPercent ?? 90}" required></div>
          <div class="field"><label for="pricing-favorable-${rule.id}">Oferta favorable desde (%)</label><input id="pricing-favorable-${rule.id}" name="favorableOfferMinimumPercent" type="number" min="1" max="1000" value="${rule.favorableOfferMinimumPercent ?? 105}" required></div>
        </div>
        <p class="muted small-text">Los porcentajes comparan la oferta con la referencia directa A–B. Por debajo de “justa” se clasifica como oferta baja.</p>
        <label class="checkbox-field"><input name="isActive" type="checkbox" ${rule.isActive ? "checked" : ""}> Regla activa para solicitudes nuevas</label>
        <div class="button-row"><button class="button button-secondary" type="submit">Guardar tarifa</button></div>
      </form>`).join("")
    : '<p class="empty">No hay reglas de tarifa configuradas.</p>';

  return `
    <article class="card wide">
      <span class="eyebrow">Tarifas</span>
      <h2>Motor de precios</h2>
      <p class="muted small-text">Las solicitudes validan la tarifa mínima configurada. Los valores por kilómetro y minuto se usarán en recomendaciones cuando exista una estimación de ruta confiable.</p>
      <div class="pricing-rule-list">${ruleForms}</div>
      <div class="divider"></div>
      <h3>Nueva regla de tarifa</h3>
      <form id="create-pricing-rule-form" class="form-grid">
        <div class="field"><label for="pricing-city-code">Ciudad</label><input id="pricing-city-code" name="cityCode" maxlength="20" placeholder="Ej.: BUC" required></div>
        <div class="field"><label for="pricing-service-type">Servicio</label><select id="pricing-service-type" name="serviceType">${renderServiceTypeOptions()}</select></div>
        <div class="field"><label for="pricing-minimum">Tarifa mínima COP</label><input id="pricing-minimum" name="minimumFareCop" type="number" min="1" required></div>
        <div class="field"><label for="pricing-base">Base COP</label><input id="pricing-base" name="baseFareCop" type="number" min="0" value="0" required></div>
        <div class="field"><label for="pricing-kilometer">COP por km</label><input id="pricing-kilometer" name="farePerKilometerCop" type="number" min="0" value="0" required></div>
        <div class="field"><label for="pricing-minute">COP por minuto</label><input id="pricing-minute" name="farePerMinuteCop" type="number" min="0" value="0" required></div>
        <div class="field"><label for="pricing-fair">Oferta justa desde (%)</label><input id="pricing-fair" name="fairOfferMinimumPercent" type="number" min="1" max="1000" value="90" required></div>
        <div class="field"><label for="pricing-favorable">Oferta favorable desde (%)</label><input id="pricing-favorable" name="favorableOfferMinimumPercent" type="number" min="1" max="1000" value="105" required></div>
        <label class="checkbox-field"><input name="isActive" type="checkbox" checked> Activar para solicitudes nuevas</label>
        <div class="button-row"><button class="button button-primary" type="submit">Crear regla</button></div>
      </form>
    </article>`;
}

function renderEmergencyChannelTypeOptions(selectedChannelType = "GeneralEmergency") {
  return ["GeneralEmergency", "MedicalEmergency", "FireEmergency"]
    .map(channelType => `<option value="${channelType}" ${selected(selectedChannelType, channelType)}>${label[channelType]}</option>`)
    .join("");
}

function renderEmergencyChannelsAdminPanel() {
  const channels = state.emergencyServiceChannels || [];
  const channelForms = channels.length
    ? channels.map(channel => `
      <form class="pricing-rule-form card" data-emergency-channel-id="${channel.id}">
        <div class="section-title"><div><span class="eyebrow">Canal configurado</span><h3>${escapeHtml(channel.cityCode)} · ${escapeHtml(label[channel.channelType] || channel.channelType)}</h3></div>${channel.isActive ? statusBadge("Approved") : statusBadge("Inactive")}</div>
        <div class="form-grid">
          <div class="field"><label for="emergency-name-${channel.id}">Nombre mostrado</label><input id="emergency-name-${channel.id}" name="displayName" maxlength="100" value="${escapeHtml(channel.displayName)}" required></div>
          <div class="field"><label for="emergency-number-${channel.id}">Número o canal</label><input id="emergency-number-${channel.id}" name="contactNumber" maxlength="20" value="${escapeHtml(channel.contactNumber)}" pattern="\\+?[0-9][0-9 -]{1,18}" required></div>
        </div>
        <label class="checkbox-field"><input name="isActive" type="checkbox" ${channel.isActive ? "checked" : ""}> Disponible en el futuro Centro de Seguridad</label>
        <div class="button-row"><button class="button button-secondary" type="submit">Guardar canal</button></div>
      </form>`).join("")
    : '<p class="empty">Aún no hay canales de emergencia configurados.</p>';

  return `
    <article class="card wide">
      <span class="eyebrow">Seguridad · administración</span>
      <h2>Canales de emergencia por ciudad</h2>
      <p class="muted small-text">Configura la información autorizada para cada ciudad. Estos datos quedan preparados para un futuro viaje activo; esta versión no inicia llamadas ni comparte ubicaciones.</p>
      <div class="pricing-rule-list">${channelForms}</div>
      <div class="divider"></div>
      <h3>Nuevo canal</h3>
      <form id="create-emergency-channel-form" class="form-grid">
        <div class="field"><label for="emergency-city-code">Ciudad</label><input id="emergency-city-code" name="cityCode" maxlength="20" placeholder="Ej.: BUC" required></div>
        <div class="field"><label for="emergency-channel-type">Tipo de ayuda</label><select id="emergency-channel-type" name="channelType">${renderEmergencyChannelTypeOptions()}</select></div>
        <div class="field"><label for="emergency-display-name">Nombre mostrado</label><input id="emergency-display-name" name="displayName" maxlength="100" placeholder="Ej.: Atención de emergencias" required></div>
        <div class="field"><label for="emergency-contact-number">Número o canal</label><input id="emergency-contact-number" name="contactNumber" maxlength="20" placeholder="Ej.: 123 o +576000000" pattern="\\+?[0-9][0-9 -]{1,18}" required></div>
        <label class="checkbox-field"><input name="isActive" type="checkbox" checked> Activar para el futuro Centro de Seguridad</label>
        <div class="button-row"><button class="button button-primary" type="submit">Crear canal</button></div>
      </form>
    </article>`;
}

function renderAdminApplication(item) {
  const canReview = item.status === "UnderReview";
  const requiredDocumentTypes = getRequiredDriverDocumentTypes(item);
  const approvedDocumentTypes = new Set(
    item.documents
      .filter(document => document.reviewStatus === "Approved")
      .map(document => document.type)
  );
  const missingRequiredDocuments = requiredDocumentTypes.filter(type => !approvedDocumentTypes.has(type));
  const allDocumentsApproved = missingRequiredDocuments.length === 0;
  const documents = item.documents.length
    ? item.documents.map(document => `
      <li class="review-document">
        <div><strong>${escapeHtml(label[document.type] || document.type)}</strong><span>${statusBadge(document.reviewStatus)}</span></div>
        ${document.reviewNotes ? `<p class="small-text muted">Nota: ${escapeHtml(document.reviewNotes)}</p>` : ""}
        <div class="button-row review-document-actions">
          <button class="button button-secondary small" type="button" data-open-admin-document="${item.id}" data-document-id="${document.id}">Ver archivo privado</button>
          ${canReview && document.reviewStatus === "Pending" ? `
            <button class="button button-primary small" type="button" data-document-review="${item.id}" data-document-id="${document.id}" data-decision="approve">Aprobar documento</button>
            <button class="button button-danger small" type="button" data-document-review="${item.id}" data-document-id="${document.id}" data-decision="reject">Rechazar documento</button>
          ` : ""}
        </div>
      </li>`).join("")
    : '<li class="empty">No hay documentos cargados.</li>';

  return `
    <li class="review-application">
      <div class="status-row">
        <div><h3>Solicitud ${escapeHtml(item.id.slice(0, 8))}</h3><p>${escapeHtml(item.vehicles.length)} vehículo(s) · ${escapeHtml(item.documents.length)} documento(s)</p></div>
        <div class="button-row">${statusBadge(item.status)}<button class="button button-secondary small" type="button" data-start-review="${item.id}" ${item.status === "Pending" ? "" : "disabled"}>Iniciar revisión</button></div>
      </div>
      <div class="review-details">
        <p class="small-text muted">Vehículo activo: ${item.vehicles.some(vehicle => vehicle.isActive) ? "Sí" : "No"}.</p>
        <ul class="review-documents">${documents}</ul>
        ${canReview ? `
          <div class="field"><label for="review-notes-${item.id}">Notas de revisión</label><textarea id="review-notes-${item.id}" maxlength="1000" placeholder="Obligatorias si rechazas un documento o solicitud."></textarea></div>
          <div class="button-row">
            <button class="button button-primary" type="button" data-application-review="${item.id}" data-decision="approve" ${allDocumentsApproved && item.vehicles.some(vehicle => vehicle.isActive) ? "" : "disabled"}>Aprobar solicitud</button>
            <button class="button button-danger" type="button" data-application-review="${item.id}" data-decision="reject">Rechazar solicitud</button>
          </div>
          ${!allDocumentsApproved ? `<p class="small-text muted">Faltan documentos obligatorios aprobados: ${escapeHtml(missingRequiredDocuments.map(type => label[type] || type).join(", "))}.</p>` : ""}` : ""}
      </div>
    </li>`;
}

function bindDriverEvents() {
  app.querySelectorAll("[data-driver-nav]").forEach(button => {
    button.addEventListener("click", () => navigateDriverPanel(button.dataset.driverNav));
  });
  app.querySelectorAll("[data-toggle-driver-alerts]").forEach(button => {
    button.addEventListener("click", toggleDriverOfferAlerts);
  });
  app.querySelectorAll("[data-view-driver-offer]").forEach(row => {
    row.addEventListener("click", event => {
      if (event.target.closest("[data-dismiss-offer]")) return;
      selectDriverOffer(row.dataset.viewDriverOffer);
    });
    row.addEventListener("keydown", event => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        selectDriverOffer(row.dataset.viewDriverOffer);
      }
    });
  });
  const closeOfferButton = app.querySelector("[data-close-driver-offer]");
  if (closeOfferButton) closeOfferButton.addEventListener("click", closeDriverOffer);
  const applyButton = app.querySelector("[data-apply-driver]");
  if (applyButton) applyButton.addEventListener("click", applyForDriver);
  const vehicleForm = app.querySelector("#vehicle-form");
  if (vehicleForm) vehicleForm.addEventListener("submit", addVehicle);
  app.querySelectorAll("[data-update-vehicle]").forEach(form => {
    form.addEventListener("submit", event => updateVehicle(event, form.dataset.updateVehicle));
  });
  app.querySelectorAll("[data-edit-vehicle]").forEach(button => {
    button.addEventListener("click", () => {
      const panel = app.querySelector(".edit-panel");
      const form = app.querySelector(`[data-update-vehicle="${button.dataset.editVehicle}"]`);
      if (panel) panel.open = true;
      form?.scrollIntoView({ behavior: "smooth", block: "center" });
      form?.querySelector("input")?.focus();
    });
  });
  const documentForm = app.querySelector("#document-form");
  if (documentForm) documentForm.addEventListener("submit", addDocument);
  app.querySelectorAll("[data-document-upload-form]").forEach(form => {
    form.addEventListener("submit", addDocument);
  });
  app.querySelectorAll("[data-document-upload-form] input[type='file']").forEach(input => {
    input.addEventListener("change", () => {
      const form = input.closest("[data-document-upload-form]");
      const indicator = form?.querySelector("[data-selected-document-file]");
      if (!indicator) return;
      const selectedFile = input.files?.[0];
      indicator.textContent = selectedFile?.name
        ? `Archivo seleccionado: ${selectedFile.name}`
        : "Sin archivo seleccionado.";
    });
  });
  const pickupLocationButton = app.querySelector("[data-capture-pickup-location]");
  if (pickupLocationButton) pickupLocationButton.addEventListener("click", capturePickupLocation);
  const destinationLocationButton = app.querySelector("[data-capture-destination-location]");
  if (destinationLocationButton) destinationLocationButton.addEventListener("click", () => captureRideLocation("destination"));
  app.querySelectorAll("[data-open-ride-map]").forEach(button => {
    button.addEventListener("click", () => openRideMapPicker(button.dataset.openRideMap));
  });
  app.querySelectorAll("[data-select-ride-map-target]").forEach(button => {
    button.addEventListener("click", () => selectRideMapPickerTarget(button.dataset.selectRideMapTarget));
  });
  const closeRideMapButton = app.querySelector("[data-close-ride-map]");
  if (closeRideMapButton) closeRideMapButton.addEventListener("click", closeRideMapPicker);
  const pickupInput = app.querySelector("#ride-pickup");
  if (pickupInput) pickupInput.addEventListener("input", () => {
    if (!state.pendingPickupLocation) return;
    state.pendingPickupLocation = null;
    state.customerRideQuote = null;
    const status = app.querySelector("#pickup-location-status");
    if (status) status.textContent = "La dirección cambió; vuelve a compartir ubicación si deseas usarla.";
    if (!app.querySelector("[data-ride-location-picker]")?.hidden) mountCustomerRideMap();
    void refreshCustomerRideQuote();
  });
  const destinationInput = app.querySelector("#ride-destination");
  if (destinationInput) destinationInput.addEventListener("input", () => {
    if (!state.pendingDestinationLocation) return;
    state.pendingDestinationLocation = null;
    state.customerRideQuote = null;
    const status = app.querySelector("#destination-location-status");
    if (status) status.textContent = "La dirección cambió; vuelve a marcar el destino si deseas usarlo en el mapa.";
    if (!app.querySelector("[data-ride-location-picker]")?.hidden) mountCustomerRideMap();
    void refreshCustomerRideQuote();
  });
  app.querySelectorAll("[data-update-dispatch-location]").forEach(button => {
    button.addEventListener("click", updateDispatchLocation);
  });
  app.querySelectorAll("[data-diagnose-gps]").forEach(button => {
    button.addEventListener("click", diagnoseDeviceLocation);
  });
  const dispatchRefreshButton = app.querySelector("[data-refresh-dispatch]");
  if (dispatchRefreshButton) dispatchRefreshButton.addEventListener("click", async () => {
    try {
      await refreshDriverDispatch();
      renderDashboard();
      showNotice("Solicitudes actualizadas.");
    } catch (error) { showNotice(error.message, true); }
  });
  app.querySelectorAll("[data-set-dispatch-radius]").forEach(button => {
    button.addEventListener("click", () => setDispatchRadius(button.dataset.setDispatchRadius));
  });
  app.querySelectorAll("[data-dismiss-offer]").forEach(button => {
    button.addEventListener("click", () => dismissDriverOffer(button.dataset.dismissOffer));
  });
  const rideLocationStepForm = app.querySelector("#ride-location-step-form");
  if (rideLocationStepForm) rideLocationStepForm.addEventListener("submit", continueRideRequestDetails);
  app.querySelectorAll("[data-customer-ride-step]").forEach(button => {
    button.addEventListener("click", () => setCustomerRideStep(button.dataset.customerRideStep));
  });
  const rideRequestForm = app.querySelector("#ride-request-form");
  if (rideRequestForm) rideRequestForm.addEventListener("submit", createRideRequest);
  const ridePricingRule = app.querySelector("#ride-pricing-rule");
  if (ridePricingRule) ridePricingRule.addEventListener("change", syncSelectedPricingRule);
  const ridePriceInput = app.querySelector("#ride-proposed-price");
  if (ridePriceInput) ridePriceInput.addEventListener("input", () => {
    saveCustomerRideDraft({ proposedPriceCop: ridePriceInput.value });
  });
  app.querySelectorAll("#ride-request-form input[name='paymentMethod'], #ride-request-form input[name='fareMode']").forEach(input => {
    input.addEventListener("change", () => {
      saveCustomerRideDraft({ [input.name]: input.value });
      if (input.name === "fareMode" && input.value === "DynamicFare") {
        applyDynamicFareRecommendation();
      }
    });
  });
  app.querySelectorAll("[data-cancel-ride]").forEach(button => {
    button.addEventListener("click", () => cancelRideRequest(button.dataset.cancelRide));
  });
  app.querySelectorAll("[data-accept-ride]").forEach(button => {
    button.addEventListener("click", () => acceptRideRequest(button.dataset.acceptRide));
  });
  app.querySelectorAll("[data-repeat-driver-route]").forEach(button => {
    button.addEventListener("click", () => repeatDriverRoute(button.dataset.repeatDriverRoute));
  });
  app.querySelectorAll("[data-test-customer-voice]").forEach(button => {
    button.addEventListener("click", testCustomerVoiceAlerts);
  });
  app.querySelectorAll("[data-open-safety-assist]").forEach(button => {
    button.addEventListener("click", () => openSafetyAssist(button.dataset.openSafetyAssist));
  });
  app.querySelectorAll("[data-counter-offer-ride]").forEach(element => {
    if (element.matches("form")) element.addEventListener("submit", event => makeCounterOffer(event, element.dataset.counterOfferRide));
  });
  app.querySelectorAll("[data-quick-counter-offer]").forEach(button => {
    button.addEventListener("click", () => sendCounterOffer(button.dataset.quickCounterOffer, button.dataset.counterOfferPrice));
  });
  app.querySelectorAll("[data-counter-offer-decision]").forEach(button => {
    button.addEventListener("click", () => decideCounterOffer(button.dataset.counterOfferRide, button.dataset.counterOfferDecision));
  });
  app.querySelectorAll("[data-journey-action]").forEach(button => {
    button.addEventListener("click", () => updateRideJourney(button.dataset.journeyAction, button.dataset.journeyRide));
  });
  app.querySelectorAll("[data-refresh-customer-tracking]").forEach(button => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      try {
        const tracking = await refreshCustomerRideTracking({ notifyJourneyChange: true });
        if (tracking && app.querySelector("[data-customer-ride-map]")) {
          showNotice("Mapa actualizado.");
        }
      } catch (error) {
        showNotice(error.message, true);
      } finally {
        if (button.isConnected) button.disabled = false;
      }
    });
  });
  app.querySelectorAll("[data-refresh-customer-status]").forEach(button => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      try {
        await refreshCustomerRideStatus({ notifyJourneyChange: true });
        if (button.isConnected) showNotice("Estado consultado.");
      } catch (error) {
        showNotice(error.message, true);
      } finally {
        if (button.isConnected) button.disabled = false;
      }
    });
  });
  app.querySelectorAll("[data-availability-slider]").forEach(slider => {
    let startX = null;
    let startY = null;
    slider.addEventListener("pointerdown", event => {
      if (slider.dataset.availabilityLocked === "true") return;
      startX = event.clientX;
      startY = event.clientY;
    });
    slider.addEventListener("pointerup", event => {
      if (startX === null || startY === null) return;
      const deltaX = event.clientX - startX;
      const deltaY = event.clientY - startY;
      startX = null;
      startY = null;
      if (Math.abs(deltaX) < 28 || Math.abs(deltaX) < Math.abs(deltaY)) return;
      const target = slider.querySelector('[data-availability="' + (deltaX > 0 ? "Available" : "Offline") + '"]');
      if (!target || target.disabled) return;
      target.click();
      slider.dataset.suppressClick = "true";
    });
    slider.addEventListener("pointercancel", () => {
      startX = null;
      startY = null;
    });
  });
  app.querySelectorAll("[data-availability]").forEach(button => {
    button.addEventListener("click", () => {
      const slider = button.closest("[data-availability-slider]");
      if (slider?.dataset.suppressClick === "true") { delete slider.dataset.suppressClick; return; }
      changeAvailability(button.dataset.availability);
    });
  });
}

function bindSafetyEvents() {
  const emergencyContactForm = app.querySelector("#emergency-contact-form");
  if (emergencyContactForm) emergencyContactForm.addEventListener("submit", createEmergencyContact);
  app.querySelectorAll("[data-archive-emergency-contact]").forEach(button => {
    button.addEventListener("click", () => archiveEmergencyContact(button.dataset.archiveEmergencyContact));
  });
}

function bindAdminEvents() {
  const refreshButton = app.querySelector("[data-refresh-admin]");
  if (refreshButton) refreshButton.addEventListener("click", async () => {
    try {
      await loadAdminApplications(app.querySelector("#admin-status").value);
      renderDashboard();
    } catch (error) { showNotice(error.message, true); }
  });
  app.querySelectorAll("[data-open-admin-document]").forEach(button => {
    button.addEventListener("click", () => openAdminDocument(
      button.dataset.openAdminDocument,
      button.dataset.documentId
    ));
  });
  app.querySelectorAll("[data-start-review]").forEach(button => {
    button.addEventListener("click", () => startReview(button.dataset.startReview));
  });
  app.querySelectorAll("[data-document-review]").forEach(button => {
    button.addEventListener("click", () => reviewDocument(
      button.dataset.documentReview,
      button.dataset.documentId,
      button.dataset.decision === "approve"));
  });
  app.querySelectorAll("[data-application-review]").forEach(button => {
    button.addEventListener("click", () => reviewApplication(
      button.dataset.applicationReview,
      button.dataset.decision === "approve"));
  });
  app.querySelectorAll("[data-pricing-rule-id]").forEach(form => {
    form.addEventListener("submit", event => updatePricingRule(event, form.dataset.pricingRuleId));
  });
  const createPricingRuleForm = app.querySelector("#create-pricing-rule-form");
  if (createPricingRuleForm) createPricingRuleForm.addEventListener("submit", createPricingRule);
  app.querySelectorAll("[data-emergency-channel-id]").forEach(form => {
    form.addEventListener("submit", event => updateEmergencyServiceChannel(event, form.dataset.emergencyChannelId));
  });
  const appearanceForm = app.querySelector("#platform-appearance-form");
  if (appearanceForm) appearanceForm.addEventListener("submit", updatePlatformAppearance);
  const createEmergencyChannelForm = app.querySelector("#create-emergency-channel-form");
  if (createEmergencyChannelForm) createEmergencyChannelForm.addEventListener("submit", createEmergencyServiceChannel);
}


async function deleteMyAccount() {
  const confirmed = window.confirm("¿Eliminar esta cuenta de cliente? Se cerrará la sesión y la cuenta quedará desactivada, conservando historial de servicios por seguridad.");
  if (!confirmed) return;
  try {
    await request("/profile/me", { method: "DELETE", data: {} });
    showNotice("Cuenta desactivada.");
    signOut(false);
  } catch (error) {
    showNotice(error.message || "No fue posible eliminar la cuenta.", true);
  }
}
async function updateProfile(event) {
  event.preventDefault();
  try {
    state.profile = await request("/profile/me", { method: "PUT", data: Object.fromEntries(new FormData(event.currentTarget)) });
    renderDashboard();
    showNotice("Perfil actualizado.");
  } catch (error) { showNotice(error.message, true); }
}

async function createEmergencyContact(event) {
  event.preventDefault();
  try {
    const emergencyContact = await request("/safety/emergency-contacts", {
      method: "POST",
      data: Object.fromEntries(new FormData(event.currentTarget))
    });
    state.emergencyContacts = [...state.emergencyContacts, emergencyContact]
      .sort((left, right) => left.name.localeCompare(right.name, "es"));
    renderDashboard();
    showNotice("Contacto de confianza guardado.");
  } catch (error) { showNotice(error.message, true); }
}

async function archiveEmergencyContact(emergencyContactId) {
  try {
    await request(`/safety/emergency-contacts/${emergencyContactId}/archive`, {
      method: "POST",
      data: {}
    });
    state.emergencyContacts = state.emergencyContacts.filter(contact => contact.id !== emergencyContactId);
    renderDashboard();
    showNotice("Contacto de confianza desactivado.");
  } catch (error) { showNotice(error.message, true); }
}

async function applyForDriver() {
  try {
    state.application = await request("/driver-application", { method: "POST", data: {} });
    renderDashboard();
    showNotice("Solicitud enviada. Continúa con vehículo y documentos.");
  } catch (error) { showNotice(error.message, true); }
}

async function addVehicle(event) {
  event.preventDefault();
  try {
    const form = Object.fromEntries(new FormData(event.currentTarget));
    form.year = Number(form.year);
    state.application = await request("/driver-application/vehicles", { method: "POST", data: form });
    renderDashboard();
    showNotice("Vehículo registrado. El panel quedó recogido.");
  } catch (error) { showNotice(error.message, true); }
}

async function updateVehicle(event, vehicleId) {
  event.preventDefault();
  try {
    const form = Object.fromEntries(new FormData(event.currentTarget));
    form.year = Number(form.year);
    state.application = await request(`/driver-application/vehicles/${vehicleId}`, { method: "PUT", data: form });
    renderDashboard();
    showNotice("Datos de la moto actualizados. El panel quedó recogido.");
  } catch (error) { showNotice(error.message, true); }
}

async function addDocument(event) {
  event.preventDefault();
  try {
    const form = buildDriverDocumentFormData(event.currentTarget);
    if (!form) return;
    state.application = await request("/driver-application/documents", { method: "POST", form });
    renderDashboard();
    showNotice("Documento enviado para revisión. El panel quedó recogido.");
  } catch (error) { showNotice(error.message, true); }
}

function buildDriverDocumentFormData(sourceForm) {
  const submitted = new FormData(sourceForm);
  const selectedFile = getSelectedDocumentFile(submitted);
  if (!selectedFile) {
    showNotice("Selecciona un archivo o toma una foto antes de enviar el documento.", true);
    return null;
  }

  const form = new FormData();
  form.set("type", submitted.get("type") || "Other");
  const expiresOn = submitted.get("expiresOn");
  if (expiresOn) form.set("expiresOn", expiresOn);
  form.set("file", selectedFile, selectedFile.name || `${submitted.get("type") || "documento"}.jpg`);
  return form;
}

function getSelectedDocumentFile(formData) {
  const candidates = [formData.get("file"), formData.get("cameraFile"), formData.get("uploadFile")];
  return candidates.find(value => value instanceof File && value.size > 0) || null;
}

function continueRideRequestDetails(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget));
  const pickupAddress = String(data.pickupAddress || "").trim();
  const destinationAddress = String(data.destinationAddress || "").trim();
  const pickupNeighborhood = String(data.pickupNeighborhood || "").trim();
  const destinationNeighborhood = String(data.destinationNeighborhood || "").trim();
  if (pickupAddress.length < 5 || destinationAddress.length < 5) {
    showNotice("Escribe una dirección válida para A y otra para B.", true);
    return;
  }

  saveCustomerRideDraft({ pickupAddress, pickupNeighborhood, destinationAddress, destinationNeighborhood });
  state.customerRideStep = "details";
  sessionStorage.setItem(customerRideStepKey, state.customerRideStep);
  renderDashboard();
  void refreshCustomerRideQuote();
  showNotice("Direcciones guardadas. Ahora revisa la tarifa y las opciones.");
}

async function createRideRequest(event) {
  event.preventDefault();
  try {
    const data = Object.fromEntries(new FormData(event.currentTarget));
    const draft = state.customerRideDraft || getDefaultCustomerRideDraft();
    const [operatingCityCode, serviceType] = data.pricingRuleKey.split("|");
    data.operatingCityCode = operatingCityCode;
    data.serviceType = serviceType;
    data.proposedPriceCop = Number(data.proposedPriceCop);
    saveCustomerRideDraft({
      ...draft,
      pricingRuleKey: data.pricingRuleKey,
      proposedPriceCop: String(data.proposedPriceCop),
      paymentMethod: data.paymentMethod || "Cash",
      fareMode: data.fareMode || "DynamicFare"
    });
    data.pickupAddress = buildAddressWithNeighborhood(draft.pickupAddress, draft.pickupNeighborhood);
    data.destinationAddress = buildAddressWithNeighborhood(draft.destinationAddress, draft.destinationNeighborhood);
    if (state.pendingPickupLocation) {
      Object.assign(data, state.pendingPickupLocation);
    }
    if (state.pendingDestinationLocation) {
      Object.assign(data, state.pendingDestinationLocation);
    }
    delete data.pricingRuleKey;
    const rideRequest = await request("/ride-requests", {
      method: "POST",
      data
    });
    state.rideRequests = [rideRequest, ...state.rideRequests];
    state.pendingPickupLocation = null;
    state.pendingDestinationLocation = null;
    state.customerRideQuote = null;
    state.customerRideQuoteVersion += 1;
    state.customerNav = "ride";
    state.customerRideStep = "locations";
    sessionStorage.setItem(customerNavKey, state.customerNav);
    sessionStorage.setItem(customerRideStepKey, state.customerRideStep);
    clearCustomerRideDraft();
    renderDashboard();
    showNotice("Solicitud creada. Aún no tiene conductor asignado.");
  } catch (error) { showNotice(error.message, true); }
}

function syncSelectedPricingRule(event) {
  const selectedOption = event.currentTarget.selectedOptions[0];
  const minimumFare = Number(selectedOption?.dataset.minimumFare);
  saveCustomerRideDraft({ pricingRuleKey: event.currentTarget.value });
  const priceInput = app.querySelector("#ride-proposed-price");
  const hint = app.querySelector("#minimum-fare-hint");
  if (!Number.isFinite(minimumFare) || minimumFare <= 0 || !priceInput || !hint) return;

  priceInput.min = String(minimumFare);
  priceInput.value = String(minimumFare);
  saveCustomerRideDraft({ proposedPriceCop: String(minimumFare) });
  const dynamicFareSelected = app.querySelector("#ride-request-form input[name='fareMode']:checked")?.value === "DynamicFare";
  hint.textContent = dynamicFareSelected
    ? "Calculando la tarifa dinámica con la ruta real…"
    : `Tarifa mínima vigente: ${formatCop(minimumFare)}. Las ubicaciones son opcionales; si las compartes, el despacho podrá mostrar los puntos A y B y ordenar por cercanía.`;
  void refreshCustomerRideQuote();
}

async function cancelRideRequest(rideRequestId) {
  try {
    const updated = await request(`/ride-requests/${rideRequestId}/cancel`, {
      method: "POST",
      data: { reason: null }
    });
    state.rideRequests = state.rideRequests.map(request => request.id === updated.id ? updated : request);
    await refreshCustomerRideTracking();
    state.customerNav = "history";
    sessionStorage.setItem(customerNavKey, state.customerNav);
    renderDashboard();
    showNotice("Solicitud cancelada.");
  } catch (error) { showNotice(error.message, true); }
}

async function acceptRideRequest(rideRequestId, source = "button") {
  stopDriverVoiceAcceptance();
  try {
    state.driverCurrentRideRequest = await request(`/driver/ride-requests/${rideRequestId}/accept`, {
      method: "POST",
      data: {}
    });
    state.selectedDriverOfferId = null;
    state.driverNav = "requests";
    state.driverRideOffers = state.driverRideOffers.filter(offer => offer.id !== rideRequestId);
    state.application = await request("/driver-application/me");
    renderDashboard();
    speakDriverAlert(buildDriverRouteVoiceMessage(state.driverCurrentRideRequest), true);
    showNotice(source === "voice"
      ? "Servicio aceptado por voz. Pasaste al panel de servicio activo."
      : "Solicitud aceptada. Pasaste al panel de servicio activo.");
  } catch (error) { showNotice(error.message, true); }
}

async function sendCounterOffer(rideRequestId, priceCop) {
  const normalizedPrice = Math.round(Number(priceCop));
  if (!Number.isFinite(normalizedPrice) || normalizedPrice <= 0) {
    showNotice("El valor de la contraoferta no es válido.", true);
    return;
  }
  try {
    state.driverCurrentRideRequest = await request(`/driver/ride-requests/${rideRequestId}/counter-offer`, {
      method: "POST",
      data: { priceCop: normalizedPrice }
    });
    state.selectedDriverOfferId = null;
    state.driverRideOffers = [];
    state.application = await request("/driver-application/me");
    renderDashboard();
    showNotice(`Contraoferta de ${formatCop(normalizedPrice)} enviada. Espera la decisión del pasajero.`);
  } catch (error) { showNotice(error.message, true); }
}

async function makeCounterOffer(event, rideRequestId) {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  await sendCounterOffer(rideRequestId, form.get("priceCop"));
}

async function decideCounterOffer(rideRequestId, decision) {
  try {
    const updated = await request(`/ride-requests/${rideRequestId}/counter-offer/${decision}`, {
      method: "POST",
      data: {}
    });
    state.rideRequests = state.rideRequests.map(request => request.id === updated.id ? updated : request);
    state.customerNav = activeCustomerRideStatuses.has(updated.status)
      ? "tracking"
      : updated.status === "Pending"
        ? "ride"
        : "history";
    sessionStorage.setItem(customerNavKey, state.customerNav);
    renderDashboard();
    showNotice(decision === "accept" ? "Contraoferta aceptada. Tu servicio ya tiene conductor." : "Contraoferta rechazada. La solicitud vuelve a estar disponible.");
  } catch (error) { showNotice(error.message, true); }
}

async function updateRideJourney(action, rideRequestId) {
  const pathByAction = {
    "en-route": "en-route",
    arrived: "arrived",
    start: "start",
    complete: "complete"
  };
  const messageByAction = {
    "en-route": "Marcaste que vas en camino.",
    arrived: "Confirmaste que llegaste a la recogida.",
    start: "Viaje iniciado.",
    complete: "Su viaje ha sido finalizado. Ya estás disponible nuevamente."
  };
  const path = pathByAction[action];
  if (!path) return;

  try {
    if (action === "complete") stopDriverVoiceAcceptance();
    await request(`/driver/ride-requests/${rideRequestId}/${path}`, {
      method: "POST",
      data: {}
    });
    await refreshDriverDispatch();
    renderDashboard();
    if (action === "complete") {
      announceRideNotification("Su viaje ha sido finalizado.", { forceVoice: true, forceSound: true });
    }
    showNotice(messageByAction[action]);
  } catch (error) { showNotice(error.message, true); }
}

async function changeAvailability(availabilityStatus) {
  try {
    state.application = await request("/driver-application/availability", { method: "PATCH", data: { availabilityStatus } });
    if (availabilityStatus === "Available") {
      await refreshDriverDispatch();
    } else {
      stopDriverVoiceAcceptance();
      stopDriverLocationTracking();
      state.driverRideOffers = [];
      state.driverCurrentRideRequest = null;
    }
    renderDashboard();
    showNotice(availabilityStatus === "Available" ? "Estás LIBRE y recibirás solicitudes cercanas." : "Quedaste OCUPADO y no recibirás nuevas solicitudes.");
  } catch (error) { showNotice(error.message, true); }
}

async function startReview(driverProfileId) {
  try {
    await request(`/admin/driver-applications/${driverProfileId}/start-review`, { method: "PUT", data: {} });
    await loadAdminApplications(state.adminStatus);
    renderDashboard();
    showNotice("Revisión iniciada.");
  } catch (error) { showNotice(error.message, true); }
}

function getReviewNotes(driverProfileId) {
  return app.querySelector(`#review-notes-${driverProfileId}`)?.value.trim() || "";
}

async function reviewDocument(driverProfileId, documentId, approve) {
  const notes = getReviewNotes(driverProfileId);
  if (!approve && !notes) {
    showNotice("Escribe una razón antes de rechazar el documento.", true);
    return;
  }

  try {
    await request(`/admin/driver-applications/${driverProfileId}/documents/${documentId}/review`, {
      method: "PUT",
      data: { approve, notes: notes || null }
    });
    await loadAdminApplications(state.adminStatus);
    renderDashboard();
    showNotice(approve ? "Documento aprobado." : "Documento rechazado.");
  } catch (error) { showNotice(error.message, true); }
}

async function reviewApplication(driverProfileId, approve) {
  const notes = getReviewNotes(driverProfileId);
  if (!approve && !notes) {
    showNotice("Escribe una razón antes de rechazar la solicitud.", true);
    return;
  }

  try {
    await request(`/admin/driver-applications/${driverProfileId}/review`, {
      method: "PUT",
      data: { approve, notes: notes || null }
    });
    await loadAdminApplications(state.adminStatus);
    renderDashboard();
    showNotice(approve ? "Solicitud aprobada y rol de conductor asignado." : "Solicitud rechazada.");
  } catch (error) { showNotice(error.message, true); }
}

function readPricingRuleData(form) {
  const data = Object.fromEntries(new FormData(form));
  return {
    ...data,
    minimumFareCop: Number(data.minimumFareCop),
    baseFareCop: Number(data.baseFareCop),
    farePerKilometerCop: Number(data.farePerKilometerCop),
    farePerMinuteCop: Number(data.farePerMinuteCop),
    fairOfferMinimumPercent: Number(data.fairOfferMinimumPercent),
    favorableOfferMinimumPercent: Number(data.favorableOfferMinimumPercent),
    isActive: data.isActive === "on"
  };
}

async function updatePricingRule(event, pricingRuleId) {
  event.preventDefault();
  try {
    await request(`/admin/pricing-rules/${pricingRuleId}`, {
      method: "PUT",
      data: readPricingRuleData(event.currentTarget)
    });
    state.pricingRules = await request("/admin/pricing-rules");
    renderDashboard();
    showNotice("Regla de tarifa actualizada.");
  } catch (error) { showNotice(error.message, true); }
}

async function createPricingRule(event) {
  event.preventDefault();
  try {
    await request("/admin/pricing-rules", {
      method: "POST",
      data: readPricingRuleData(event.currentTarget)
    });
    state.pricingRules = await request("/admin/pricing-rules");
    renderDashboard();
    showNotice("Regla de tarifa creada.");
  } catch (error) { showNotice(error.message, true); }
}

function readEmergencyServiceChannelData(form) {
  const data = Object.fromEntries(new FormData(form));
  return { ...data, isActive: data.isActive === "on" };
}

async function updateEmergencyServiceChannel(event, emergencyServiceChannelId) {
  event.preventDefault();
  try {
    await request(`/admin/safety/emergency-channels/${emergencyServiceChannelId}`, {
      method: "PUT",
      data: readEmergencyServiceChannelData(event.currentTarget)
    });
    state.emergencyServiceChannels = await request("/admin/safety/emergency-channels");
    renderDashboard();
    showNotice("Canal de emergencia actualizado.");
  } catch (error) { showNotice(error.message, true); }
}

async function createEmergencyServiceChannel(event) {
  event.preventDefault();
  try {
    await request("/admin/safety/emergency-channels", {
      method: "POST",
      data: readEmergencyServiceChannelData(event.currentTarget)
    });
    state.emergencyServiceChannels = await request("/admin/safety/emergency-channels");
    renderDashboard();
    showNotice("Canal de emergencia creado.");
  } catch (error) { showNotice(error.message, true); }
}

function signOut(notify = true) {
  if (notify && !window.confirm("¿Estás seguro de que quieres salir?")) return;
  stopDriverVoiceAcceptance();
  stopDriverLocationTracking();
  destroyDriverMap();
  destroyCustomerTrackingMap();
  window.speechSynthesis?.cancel();
  if (state.driverAudioContext) {
    void state.driverAudioContext.close().catch(() => {});
    state.driverAudioContext = null;
  }
  window.clearInterval(state.dispatchPollingTimer);
  window.clearInterval(state.customerTrackingPollingTimer);
  state.dispatchPollingTimer = null;
  state.customerTrackingPollingTimer = null;
  state.token = null;
  state.profile = null;
  state.application = null;
  state.rideRequests = [];
  state.driverRideOffers = [];
  state.driverCurrentRideRequest = null;
  state.driverCompletedRideRequests = [];
  state.driverActivitySummary = null;
  state.rideRatings = {};
  state.rideRatingLoadingIds.clear();
  state.selectedDriverOfferId = null;
  state.pricingRules = [];
  state.emergencyContacts = [];
  state.emergencyServiceChannels = [];
  state.adminApplications = null;
  state.adminStatus = "All";
  state.pendingPickupLocation = null;
  state.pendingDestinationLocation = null;
  state.customerRideQuote = null;
  state.customerRideQuoteVersion += 1;
  state.customerRideTracking = null;
  state.customerTrackingVersion += 1;
  state.rideMapPickerTarget = "pickup";
  state.customerNav = "ride";
  state.customerRideStep = "locations";
  clearCustomerRideDraft();
  state.driverNav = "requests";
  state.hiddenDriverOfferIds.clear();
  sessionStorage.removeItem(sessionKey);
  sessionStorage.removeItem(accountSplashSessionKey);
  sessionStorage.removeItem(modeKey);
  sessionStorage.removeItem(customerNavKey);
  sessionStorage.removeItem(customerRideStepKey);
  renderWelcome();
  if (notify) showNotice("Sesión cerrada.");
}

if (state.token) {
  loadDashboard();
} else {
  renderWelcome();
}




