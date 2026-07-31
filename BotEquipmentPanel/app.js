const PRIORITY = [7, 16, 60, 9, 1];
const $ = id => document.getElementById(id);

let gunCatalog = [];
let knifeCatalog = [];
let stickerCatalog = [];
let stickerWeaponSlots = {};
let stickersById = new Map();
let config = null;
let weapons = [];
let knives = [];
let activeWeapon = 7;
let activeKnife = 507;
let focusedGun = null;
let focusedKnife = null;
let gunDetailTab = "skin";
let activeStickerSlot = 0;
let selectedStickerPresetId = "";
let dirty = false;
let toastTimer = null;
let desktopRequestId = 0;
let uninstallInfo = null;
const desktopRequests = new Map();
const STICKER_RESULT_LIMIT = 160;
const STICKER_MODES = new Set(["random", "off", "custom"]);
const FINISH_LABELS = {
  paper: "普通",
  glitter: "闪亮",
  holo: "全息",
  foil: "闪亮",
  gold: "金色",
  lenticular: "透镜",
  embroidered: "刺绣"
};

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>'"]/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","'":"&#39;",'"':"&quot;"}[c]));
}

if (window.chrome?.webview) {
  window.chrome.webview.addEventListener("message", event => {
    let message = event.data;
    if (typeof message === "string") {
      try { message = JSON.parse(message); } catch { return; }
    }
    const pending = desktopRequests.get(message.id);
    if (!pending) return;
    desktopRequests.delete(message.id);
    if (message.ok) pending.resolve(message.data);
    else pending.reject(new Error(message.error || "桌面程序请求失败"));
  });
}

async function api(path, body) {
  if (window.chrome?.webview) {
    return new Promise((resolve, reject) => {
      const id = ++desktopRequestId;
      desktopRequests.set(id, {resolve, reject});
      window.chrome.webview.postMessage(JSON.stringify({id, path, body: body ?? null}));
    });
  }
  const response = await fetch(path, body === undefined ? {} : {
    method: "POST", headers: {"Content-Type":"application/json"}, body: JSON.stringify(body)
  });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(payload.error || `请求失败 (${response.status})`);
  return payload;
}

async function loadCatalog(path) {
  const response = await fetch(path);
  if (!response.ok) throw new Error(`皮肤目录载入失败 (${response.status})`);
  return response.json();
}

function showToast(message, error = false) {
  const toast = $("toast");
  toast.textContent = message;
  toast.className = `toast is-visible${error ? " is-error" : ""}`;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.className = "toast", 3200);
}

function groupCatalogs() {
  const gunMap = new Map();
  for (const skin of gunCatalog) {
    const id = Number(skin.weapon_defindex);
    if (!gunMap.has(id)) gunMap.set(id, {id, name: skin.weapon, skins: []});
    gunMap.get(id).skins.push(skin);
  }
  weapons = [...gunMap.values()].sort((a, b) => {
    const ai = PRIORITY.indexOf(a.id), bi = PRIORITY.indexOf(b.id);
    if (ai >= 0 || bi >= 0) return (ai < 0 ? 999 : ai) - (bi < 0 ? 999 : bi);
    return a.name.localeCompare(b.name, "zh-CN");
  });

  const knifeMap = new Map();
  for (const skin of knifeCatalog) {
    const id = Number(skin.knife_defindex);
    if (!knifeMap.has(id)) knifeMap.set(id, {id, name: skin.knife, skins: []});
    knifeMap.get(id).skins.push(skin);
  }
  knives = [...knifeMap.values()].sort((a, b) => a.id - b.id);
  if (!gunMap.has(activeWeapon)) activeWeapon = weapons[0]?.id ?? 0;
  if (!knifeMap.has(activeKnife)) activeKnife = knives[0]?.id ?? 0;
}

function normalizeConfig(value) {
  const next = value || {};
  next.knife_def_indexes = Array.isArray(next.knife_def_indexes) ? next.knife_def_indexes.map(Number) : [507, 515];
  next.knife_paint_kits = Array.isArray(next.knife_paint_kits) ? next.knife_paint_kits.map(Number) : [415, 416];
  next.knife_skin_settings = next.knife_skin_settings || {};
  next.knife_paint_kits_by_def_index = next.knife_paint_kits_by_def_index || {};
  next.knife_skin_settings_by_def_index = next.knife_skin_settings_by_def_index || {};
  next.weapon_paint_kits = next.weapon_paint_kits || {};
  next.weapon_skin_settings = next.weapon_skin_settings || {};
  next.stickers_enabled = true;
  next.sticker_presets = next.sticker_presets || {};
  delete next.auto_drop_bot_knife_copy;
  delete next.drop_delay_seconds;

  for (const bucket of Object.values(next.weapon_skin_settings)) {
    if (!bucket || typeof bucket !== "object") continue;
    for (const settings of Object.values(bucket)) {
      if (!settings || typeof settings !== "object") continue;
      settings.sticker_mode = STICKER_MODES.has(settings.sticker_mode) ? settings.sticker_mode : "random";
      settings.stickers = Array.isArray(settings.stickers)
        ? settings.stickers.map(normalizeStickerSlot).filter(Boolean)
        : [];
    }
  }
  for (const [defIndex, presets] of Object.entries(next.sticker_presets)) {
    next.sticker_presets[defIndex] = Array.isArray(presets) ? presets.map(preset => ({
      id: String(preset?.id || createPresetId()),
      name: String(preset?.name || "未命名方案").slice(0, 60),
      stickers: Array.isArray(preset?.stickers)
        ? preset.stickers.map(normalizeStickerSlot).filter(Boolean)
        : []
    })) : [];
  }

  if (!Object.keys(next.knife_paint_kits_by_def_index).length) {
    for (const defIndex of next.knife_def_indexes) {
      const valid = new Set(knives.find(item => item.id === defIndex)?.skins.map(skin => Number(skin.paint)) || []);
      next.knife_paint_kits_by_def_index[String(defIndex)] = next.knife_paint_kits.filter(paint => valid.has(paint));
    }
  }
  if (!Object.keys(next.knife_skin_settings_by_def_index).length) {
    for (const [defIndex, paints] of Object.entries(next.knife_paint_kits_by_def_index)) {
      const settings = {};
      for (const paint of paints) {
        if (next.knife_skin_settings[String(paint)]) settings[String(paint)] = next.knife_skin_settings[String(paint)];
      }
      if (Object.keys(settings).length) next.knife_skin_settings_by_def_index[defIndex] = settings;
    }
  }
  return next;
}

function setDirty(value = true) {
  dirty = value;
  $("saveState").textContent = value ? "有未保存的修改" : "配置已同步";
  $("saveState").classList.toggle("is-dirty", value);
}

function renderInstallationStatus(status) {
  const state = status || {};
  const label = $("modeState");
  const enable = $("enableBotMode");
  label.className = "mode-state";
  enable.hidden = true;

  if (!state.root_valid) {
    label.textContent = "未选择有效游戏目录";
    label.classList.add("is-warning");
  } else if (state.names_ready) {
    label.textContent = "Bot 模式与名字组件正常";
    label.classList.add("is-ready");
  } else if (!state.bot_mode_active) {
    label.textContent = "当前为普通联机模式";
    label.classList.add("is-warning");
    enable.hidden = !state.can_enable_bot_mode;
  } else {
    const count = Array.isArray(state.missing) ? state.missing.length : 0;
    label.textContent = `Bot 插件文件缺失 ${count} 项`;
    label.classList.add("is-warning");
  }
}

function defaultSettings(skin, statTrak = false) {
  const min = Number(skin.min_wear ?? 0), max = Number(skin.max_wear ?? 1);
  const settings = {wear: Math.min(max, Math.max(min, 0.01)), seed: 0};
  if (statTrak) Object.assign(settings, {
    stattrak: false,
    stattrak_min: 0,
    stattrak_max: 99999,
    sticker_mode: "random",
    stickers: []
  });
  return settings;
}

function normalizeStickerSlot(value) {
  if (!value || !Number.isInteger(Number(value.slot)) || !Number.isInteger(Number(value.def_index))) return null;
  const sticker = {
    slot: Math.max(0, Math.min(4, Number(value.slot))),
    def_index: Number(value.def_index),
    wear: Math.max(0, Math.min(1, Number(value.wear) || 0))
  };
  for (const key of ["rotation", "x", "y"]) {
    if (value[key] !== null && value[key] !== undefined && Number.isFinite(Number(value[key]))) sticker[key] = Number(value[key]);
  }
  return sticker;
}

function createPresetId() {
  return `preset-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 9)}`;
}

function selectedGunPaints(defIndex) {
  const values = config.weapon_paint_kits[String(defIndex)];
  return Array.isArray(values) ? values.map(Number) : [];
}

function gunSettings(skin, create = false) {
  const defKey = String(skin.weapon_defindex), paintKey = String(skin.paint);
  let bucket = config.weapon_skin_settings[defKey];
  if (!bucket && create) bucket = config.weapon_skin_settings[defKey] = {};
  let settings = bucket?.[paintKey];
  if (!settings && create) settings = bucket[paintKey] = defaultSettings(skin, true);
  return settings || defaultSettings(skin, true);
}

function stickerSlotCount(skin = focusedGun) {
  if (!skin) return 0;
  const metadata = stickerWeaponSlots[String(skin.weapon_defindex)];
  if (!metadata) return 5;
  const legacy = Array.isArray(metadata.legacyPaints)
    && metadata.legacyPaints.includes(Number(skin.paint));
  return Math.max(0, Math.min(5, Number(legacy ? metadata.legacy : metadata.standard) || 0));
}

function stickerSlots(settings) {
  if (!settings && focusedGun) settings = gunSettings(focusedGun, false);
  return Array.isArray(settings?.stickers) ? settings.stickers : [];
}

function stickerAt(settings, slot) {
  return stickerSlots(settings).find(sticker => Number(sticker.slot) === Number(slot)) || null;
}

function replaceStickerSlot(settings, value) {
  const slots = stickerSlots(settings).filter(sticker => Number(sticker.slot) !== Number(value.slot));
  slots.push(normalizeStickerSlot(value));
  settings.stickers = slots.filter(Boolean).sort((left, right) => left.slot - right.slot);
}

function cloneStickerSlots(values) {
  return (values || []).map(sticker => ({...sticker}));
}

function weaponStickerPresets(defIndex = activeWeapon, create = true) {
  const key = String(defIndex);
  if (!Array.isArray(config.sticker_presets[key])) {
    if (!create) return [];
    config.sticker_presets[key] = [];
  }
  return config.sticker_presets[key];
}

function selectedKnifePaints(defIndex) {
  const values = config.knife_paint_kits_by_def_index[String(defIndex)];
  return Array.isArray(values) ? values.map(Number) : [];
}

function knifeSettings(skin, create = false) {
  const defKey = String(skin.knife_defindex), paintKey = String(skin.paint);
  let bucket = config.knife_skin_settings_by_def_index[defKey];
  if (!bucket && create) bucket = config.knife_skin_settings_by_def_index[defKey] = {};
  let settings = bucket?.[paintKey];
  if (!settings && create) settings = bucket[paintKey] = defaultSettings(skin);
  return settings || defaultSettings(skin);
}

function hydrateSettings() {
  for (const [defKey, paints] of Object.entries(config.weapon_paint_kits)) {
    const weapon = weapons.find(item => item.id === Number(defKey));
    for (const paint of Array.isArray(paints) ? paints : []) {
      const skin = weapon?.skins.find(item => Number(item.paint) === Number(paint));
      if (skin) gunSettings(skin, true);
    }
  }
  for (const [defKey, paints] of Object.entries(config.knife_paint_kits_by_def_index)) {
    const knife = knives.find(item => item.id === Number(defKey));
    for (const paint of Array.isArray(paints) ? paints : []) {
      const skin = knife?.skins.find(item => Number(item.paint) === Number(paint));
      if (skin) knifeSettings(skin, true);
    }
  }
}

function toggleGunSkin(skin, force) {
  const key = String(skin.weapon_defindex), current = selectedGunPaints(skin.weapon_defindex);
  const shouldSelect = force ?? !current.includes(Number(skin.paint));
  config.weapon_paint_kits[key] = shouldSelect
    ? [...new Set([...current, Number(skin.paint)])].sort((a, b) => a - b)
    : current.filter(id => id !== Number(skin.paint));
  if (shouldSelect) gunSettings(skin, true);
  else {
    delete config.weapon_skin_settings[key]?.[String(skin.paint)];
    if (config.weapon_skin_settings[key] && !Object.keys(config.weapon_skin_settings[key]).length) delete config.weapon_skin_settings[key];
    if (!config.weapon_paint_kits[key].length) delete config.weapon_paint_kits[key];
  }
  setDirty(); renderGuns();
}

function toggleKnifeSkin(skin, force) {
  const key = String(skin.knife_defindex), current = selectedKnifePaints(skin.knife_defindex);
  const shouldSelect = force ?? !current.includes(Number(skin.paint));
  config.knife_paint_kits_by_def_index[key] = shouldSelect
    ? [...new Set([...current, Number(skin.paint)])].sort((a, b) => a - b)
    : current.filter(id => id !== Number(skin.paint));
  if (shouldSelect) knifeSettings(skin, true);
  else {
    delete config.knife_skin_settings_by_def_index[key]?.[String(skin.paint)];
    if (config.knife_skin_settings_by_def_index[key] && !Object.keys(config.knife_skin_settings_by_def_index[key]).length)
      delete config.knife_skin_settings_by_def_index[key];
  }
  setDirty(); renderKnives();
}

function filteredGunSkins() {
  const query = $("skinSearch").value.trim().toLocaleLowerCase("zh-CN");
  return (weapons.find(item => item.id === activeWeapon)?.skins || []).filter(skin =>
    !query || skin.name.toLocaleLowerCase("zh-CN").includes(query) || String(skin.paint) === query);
}

function filteredKnifeSkins() {
  const query = $("knifeSkinSearch").value.trim().toLocaleLowerCase("zh-CN");
  return (knives.find(item => item.id === activeKnife)?.skins || []).filter(skin =>
    !query || skin.name.toLocaleLowerCase("zh-CN").includes(query) || String(skin.paint) === query);
}

function skinCards(items, selected, focused, kind) {
  return items.length ? items.map(skin => {
    const chosen = selected.includes(Number(skin.paint));
    const isFocused = focused && Number(focused.paint) === Number(skin.paint);
    return `<button class="skin-card${chosen ? " is-selected" : ""}${isFocused ? " is-focused" : ""}" data-${kind}-paint="${skin.paint}">
      <span class="skin-image"><img loading="lazy" src="${escapeHtml(skin.image)}" alt="${escapeHtml(skin.name)}"></span>
      <span class="skin-check" data-toggle="1">✓</span>
      <span class="skin-info"><strong>${escapeHtml(skin.name)}</strong><small><span style="color:${escapeHtml(skin.color)}">${escapeHtml(skin.rarity)}</span><span>#${skin.paint}</span></small></span>
    </button>`;
  }).join("") : `<div class="empty-grid">没有匹配的皮肤</div>`;
}

function renderGuns() {
  const query = $("weaponSearch").value.trim().toLocaleLowerCase("zh-CN");
  $("weaponList").innerHTML = weapons.filter(item => !query || item.name.toLocaleLowerCase("zh-CN").includes(query)).map(item => {
    const count = selectedGunPaints(item.id).length;
    return `<button class="weapon-item${item.id === activeWeapon ? " is-active" : ""}" data-weapon="${item.id}"><span>${escapeHtml(item.name)}</span><span class="${count ? "has-selection" : ""}">${count || item.skins.length}</span></button>`;
  }).join("");
  const weapon = weapons.find(item => item.id === activeWeapon), selected = selectedGunPaints(activeWeapon);
  $("weaponTitle").textContent = weapon?.name || "武器";
  $("selectionSummary").textContent = selected.length ? `从已选 ${selected.length} 款中随机` : `全部 ${weapon?.skins.length || 0} 款随机`;
  $("skinGrid").innerHTML = skinCards(filteredGunSkins(), selected, focusedGun, "gun");
  const total = Object.values(config.weapon_paint_kits).reduce((sum, list) => sum + (Array.isArray(list) ? list.length : 0), 0);
  $("gunTotal").textContent = total || "全部";
  renderGunDetail();
}

function renderGunDetail() {
  if (!focusedGun) { $("detailEmpty").hidden = false; $("detailContent").hidden = true; return; }
  const selected = selectedGunPaints(focusedGun.weapon_defindex).includes(Number(focusedGun.paint));
  const settings = gunSettings(focusedGun, selected);
  $("detailEmpty").hidden = true; $("detailContent").hidden = false;
  $("detailContent").classList.toggle("is-disabled", !selected);
  $("detailImage").src = focusedGun.image; $("detailImage").alt = `${focusedGun.weapon} ${focusedGun.name}`;
  $("detailWeapon").textContent = focusedGun.weapon; $("detailName").textContent = focusedGun.name;
  $("detailMeta").textContent = `${focusedGun.rarity} · Paint Kit ${focusedGun.paint}`;
  $("toggleFocused").textContent = selected ? "从随机池移除" : "加入当前武器随机池";
  $("toggleFocused").classList.toggle("is-remove", selected);
  fillWearControls(focusedGun, settings, "");
  $("seedNumber").value = settings.seed;
  $("statTrak").checked = !!settings.stattrak; $("statMin").value = settings.stattrak_min; $("statMax").value = settings.stattrak_max;
  $("statRange").style.opacity = settings.stattrak ? "1" : ".4";
  $("statRange").style.pointerEvents = settings.stattrak ? "auto" : "none";
  $("gunSkinTab").classList.toggle("is-active", gunDetailTab === "skin");
  $("gunStickerTab").classList.toggle("is-active", gunDetailTab === "stickers");
  $("gunSkinPanel").hidden = gunDetailTab !== "skin";
  $("gunStickerPanel").hidden = gunDetailTab !== "stickers";
  renderStickerEditor(settings, selected);
}

function renderStickerEditor(settings, selected) {
  const mode = STICKER_MODES.has(settings.sticker_mode) ? settings.sticker_mode : "random";
  const slotCount = stickerSlotCount();
  if (activeStickerSlot >= slotCount) activeStickerSlot = Math.max(0, slotCount - 1);
  $("stickersEnabled").checked = config.stickers_enabled !== false;
  $("stickerGlobalState").textContent = config.stickers_enabled === false ? "已对全部武器关闭" : "对全部武器生效";
  document.querySelectorAll("[data-sticker-mode]").forEach(button => {
    button.classList.toggle("is-active", button.dataset.stickerMode === mode);
    button.disabled = !selected;
  });

  const presets = weaponStickerPresets(focusedGun.weapon_defindex, false);
  if (!presets.some(preset => preset.id === selectedStickerPresetId)) selectedStickerPresetId = "";
  $("stickerPresetSelect").innerHTML = [
    '<option value="">选择已保存方案</option>',
    ...presets.map(preset => `<option value="${escapeHtml(preset.id)}">${escapeHtml(preset.name)}</option>`)
  ].join("");
  $("stickerPresetSelect").value = selectedStickerPresetId;
  const selectedPreset = presets.find(preset => preset.id === selectedStickerPresetId);
  if (document.activeElement !== $("stickerPresetName")) $("stickerPresetName").value = selectedPreset?.name || "";
  $("applyStickerPreset").disabled = !selected || !selectedPreset;
  $("updateStickerPreset").disabled = !selected || !selectedPreset;
  $("deleteStickerPreset").disabled = !selectedPreset;
  $("saveStickerPreset").disabled = !selected;

  $("stickerSlots").innerHTML = Array.from({length: slotCount}, (_, slot) => {
    const configured = stickerAt(settings, slot);
    const metadata = configured ? stickersById.get(Number(configured.def_index)) : null;
    const image = metadata?.image
      ? `<img loading="lazy" src="${escapeHtml(metadata.image)}" alt="${escapeHtml(metadata.name)}">`
      : "";
    return `<button class="sticker-slot${slot === activeStickerSlot ? " is-active" : ""}" data-sticker-slot="${slot}">
      <span class="sticker-slot-index">${slot + 1}</span>
      <span class="sticker-slot-image">${image}</span>
      <span class="sticker-slot-text"><strong>${escapeHtml(metadata?.name || "空槽位")}</strong><small>${configured ? `${FINISH_LABELS[metadata?.finish] || metadata?.finish || "印花"} · #${configured.def_index}` : "未设置印花"}</small></span>
    </button>`;
  }).join("");

  const active = stickerAt(settings, activeStickerSlot);
  const custom = mode === "custom";
  $("chooseSticker").disabled = !selected || !custom;
  $("clearStickerSlot").disabled = !selected || !custom || !active;
  $("applyStickerFour").disabled = !selected || !custom || !active;
  $("applyStickerAll").disabled = !selected || !custom || !active;
  $("stickerParameters").classList.toggle("is-disabled", !selected || !custom || !active);
  const wear = Math.round((Number(active?.wear) || 0) * 100);
  const rotation = Math.round(Number(active?.rotation) || 0);
  $("stickerWear").value = wear;
  $("stickerWearValue").textContent = `${wear}%`;
  $("stickerRotation").value = rotation;
  $("stickerRotationValue").textContent = `${rotation}°`;
  $("stickerX").value = Number(active?.x) || 0;
  $("stickerY").value = Number(active?.y) || 0;
}

function renderKnives() {
  const query = $("knifeTypeSearch").value.trim().toLocaleLowerCase("zh-CN");
  $("knifeTypeList").innerHTML = knives.filter(item => !query || item.name.toLocaleLowerCase("zh-CN").includes(query)).map(item => {
    const count = selectedKnifePaints(item.id).length, enabled = config.knife_def_indexes.includes(item.id);
    return `<button class="weapon-item${item.id === activeKnife ? " is-active" : ""}" data-knife-type="${item.id}"><span class="knife-type-name"><input type="checkbox" data-knife-enable="${item.id}" ${enabled ? "checked" : ""}><span>${escapeHtml(item.name)}</span></span><span class="${count ? "has-selection" : ""}">${count || item.skins.length}</span></button>`;
  }).join("");
  const knife = knives.find(item => item.id === activeKnife), selected = selectedKnifePaints(activeKnife);
  $("knifeTitle").textContent = knife?.name || "刀具";
  $("knifeSelectionSummary").textContent = selected.length ? `从已选 ${selected.length} 款中随机` : `全部 ${knife?.skins.length || 0} 款随机`;
  $("knifeSkinGrid").innerHTML = skinCards(filteredKnifeSkins(), selected, focusedKnife, "knife");
  const total = Object.values(config.knife_paint_kits_by_def_index).reduce((sum, list) => sum + (Array.isArray(list) ? list.length : 0), 0);
  $("knifeTotal").textContent = total || "全部";
  renderKnifeDetail();
}

function renderKnifeDetail() {
  if (!focusedKnife) { $("knifeDetailEmpty").hidden = false; $("knifeDetailContent").hidden = true; return; }
  const selected = selectedKnifePaints(focusedKnife.knife_defindex).includes(Number(focusedKnife.paint));
  const settings = knifeSettings(focusedKnife, selected);
  $("knifeDetailEmpty").hidden = true; $("knifeDetailContent").hidden = false;
  $("knifeDetailContent").classList.toggle("is-disabled", !selected);
  $("knifeDetailImage").src = focusedKnife.image; $("knifeDetailImage").alt = `${focusedKnife.knife} ${focusedKnife.name}`;
  $("knifeDetailWeapon").textContent = focusedKnife.knife; $("knifeDetailName").textContent = focusedKnife.name;
  $("knifeDetailMeta").textContent = `${focusedKnife.rarity} · Paint Kit ${focusedKnife.paint}`;
  $("knifeToggleFocused").textContent = selected ? "从随机池移除" : "加入当前刀型随机池";
  $("knifeToggleFocused").classList.toggle("is-remove", selected);
  fillWearControls(focusedKnife, settings, "knife");
  $("knifeSeedNumber").value = settings.seed;
}

function fillWearControls(skin, settings, prefix) {
  const min = Number(skin.min_wear ?? 0), max = Number(skin.max_wear ?? 1);
  const base = prefix ? `${prefix}Wear` : "wear";
  $(`${base}Range`).textContent = `${min.toFixed(6)} - ${max.toFixed(6)}`;
  for (const suffix of ["Slider", "Number"]) {
    const control = $(`${base}${suffix}`); control.min = min; control.max = max; control.value = settings.wear;
  }
}

function renderAll() { renderGuns(); renderKnives(); }

function setStickerMode(mode) {
  if (!focusedGun || !STICKER_MODES.has(mode)) return;
  const settings = gunSettings(focusedGun, true);
  settings.sticker_mode = mode;
  if (!Array.isArray(settings.stickers)) settings.stickers = [];
  setDirty();
  renderGunDetail();
}

function updateActiveSticker(patch) {
  if (!focusedGun) return;
  const settings = gunSettings(focusedGun, true);
  const current = stickerAt(settings, activeStickerSlot);
  if (!current) return;
  replaceStickerSlot(settings, {...current, ...patch, slot: activeStickerSlot});
  setDirty();
  renderStickerEditor(settings, true);
}

function fillStickerSlots(limit) {
  if (!focusedGun) return;
  const settings = gunSettings(focusedGun, true);
  const source = stickerAt(settings, activeStickerSlot);
  if (!source) return;
  const count = Math.min(limit, stickerSlotCount());
  const copyPosition = $("copyStickerPosition").checked;
  for (let slot = 0; slot < count; slot++) {
    const previous = stickerAt(settings, slot);
    replaceStickerSlot(settings, {
      ...source,
      slot,
      x: copyPosition ? source.x : previous?.x,
      y: copyPosition ? source.y : previous?.y
    });
  }
  settings.sticker_mode = "custom";
  setDirty();
  renderGunDetail();
}

function saveStickerPreset() {
  if (!focusedGun) return;
  const name = $("stickerPresetName").value.trim();
  if (!name) { showToast("请输入方案名称", true); $("stickerPresetName").focus(); return; }
  const settings = gunSettings(focusedGun, true);
  if (!stickerSlots(settings).length) { showToast("当前没有可保存的印花", true); return; }
  const preset = {id: createPresetId(), name: name.slice(0, 60), stickers: cloneStickerSlots(settings.stickers)};
  weaponStickerPresets(focusedGun.weapon_defindex).push(preset);
  selectedStickerPresetId = preset.id;
  setDirty(); renderGunDetail(); showToast(`已保存方案：${preset.name}`);
}

function updateStickerPreset() {
  if (!focusedGun || !selectedStickerPresetId) return;
  const preset = weaponStickerPresets(focusedGun.weapon_defindex).find(item => item.id === selectedStickerPresetId);
  if (!preset) return;
  const name = $("stickerPresetName").value.trim();
  if (!name) { showToast("请输入方案名称", true); return; }
  preset.name = name.slice(0, 60);
  preset.stickers = cloneStickerSlots(gunSettings(focusedGun, true).stickers);
  setDirty(); renderGunDetail(); showToast(`已更新方案：${preset.name}`);
}

function applyStickerPreset() {
  if (!focusedGun || !selectedStickerPresetId) return;
  const preset = weaponStickerPresets(focusedGun.weapon_defindex).find(item => item.id === selectedStickerPresetId);
  if (!preset) return;
  const settings = gunSettings(focusedGun, true);
  settings.sticker_mode = "custom";
  settings.stickers = cloneStickerSlots(preset.stickers)
    .filter(sticker => sticker.slot < stickerSlotCount());
  setDirty(); renderGunDetail(); showToast(`已应用方案：${preset.name}`);
}

function deleteStickerPreset() {
  if (!focusedGun || !selectedStickerPresetId) return;
  const presets = weaponStickerPresets(focusedGun.weapon_defindex);
  const index = presets.findIndex(item => item.id === selectedStickerPresetId);
  if (index < 0) return;
  const [removed] = presets.splice(index, 1);
  selectedStickerPresetId = "";
  setDirty(); renderGunDetail(); showToast(`已删除方案：${removed.name}`);
}

function normalizeSearch(value) {
  return String(value || "").toLocaleLowerCase("zh-CN").replace(/[\s|（）()·._-]+/g, "");
}

function renderStickerResults() {
  const terms = String($("stickerSearch").value || "")
    .split(/[\s|,，]+/)
    .map(normalizeSearch)
    .filter(Boolean);
  const finish = $("stickerFinish").value;
  const matched = [];
  for (const sticker of stickerCatalog) {
    if (finish && sticker.finish !== finish) continue;
    if (terms.length && !terms.every(term => sticker.search.includes(term))) continue;
    matched.push(sticker);
  }
  const visible = matched.slice(0, STICKER_RESULT_LIMIT);
  $("stickerResultState").textContent = matched.length > visible.length
    ? `找到 ${matched.length} 款，显示前 ${visible.length} 款`
    : `找到 ${matched.length} 款`;
  $("stickerResults").innerHTML = visible.length ? visible.map(sticker => {
    const image = sticker.image
      ? `<img loading="lazy" src="${escapeHtml(sticker.image)}" alt="${escapeHtml(sticker.name)}">`
      : "";
    return `<button class="sticker-result" data-sticker-result="${sticker.id}">
      <span class="sticker-result-image">${image}</span>
      <span class="sticker-result-text"><strong>${escapeHtml(sticker.name)}</strong><small>${escapeHtml(sticker.english)} · ${FINISH_LABELS[sticker.finish] || sticker.finish}</small></span>
      <span class="sticker-result-id">#${sticker.id}</span>
    </button>`;
  }).join("") : '<div class="empty-grid">没有匹配的印花</div>';
}

function openStickerPicker() {
  if (!focusedGun) return;
  $("stickerPickerSlot").textContent = `${focusedGun.weapon} · 槽位 ${activeStickerSlot + 1}`;
  $("stickerSearch").value = "";
  $("stickerFinish").value = "";
  $("stickerPickerModal").hidden = false;
  renderStickerResults();
  $("stickerSearch").focus();
}

function closeStickerPicker() {
  $("stickerPickerModal").hidden = true;
}

function chooseSticker(defIndex) {
  if (!focusedGun || !stickersById.has(defIndex)) return;
  const settings = gunSettings(focusedGun, true);
  const previous = stickerAt(settings, activeStickerSlot);
  replaceStickerSlot(settings, {
    def_index: defIndex,
    slot: activeStickerSlot,
    wear: previous?.wear || 0,
    rotation: previous?.rotation,
    x: previous?.x,
    y: previous?.y
  });
  settings.sticker_mode = "custom";
  setDirty(); closeStickerPicker(); renderGunDetail();
}

function applyLoaded(payload) {
  config = normalizeConfig(payload.config);
  hydrateSettings();
  $("rootPath").value = payload.root || "";
  focusedGun = null; focusedKnife = null;
  gunDetailTab = "skin"; activeStickerSlot = 0; selectedStickerPresetId = "";
  renderInstallationStatus(payload.status);
  setDirty(false); renderAll();
}

document.addEventListener("click", event => {
  const stickerResult = event.target.closest("[data-sticker-result]");
  if (stickerResult) { chooseSticker(Number(stickerResult.dataset.stickerResult)); return; }
  const stickerSlot = event.target.closest("[data-sticker-slot]");
  if (stickerSlot) { activeStickerSlot = Number(stickerSlot.dataset.stickerSlot); renderGunDetail(); return; }
  const stickerMode = event.target.closest("[data-sticker-mode]");
  if (stickerMode) { setStickerMode(stickerMode.dataset.stickerMode); return; }
  const nav = event.target.closest("[data-view]");
  if (nav) {
    document.querySelectorAll("[data-view]").forEach(item => item.classList.toggle("is-active", item === nav));
    $("gunsView").hidden = nav.dataset.view !== "guns"; $("knivesView").hidden = nav.dataset.view !== "knives"; return;
  }
  const weaponButton = event.target.closest("[data-weapon]");
  if (weaponButton) { activeWeapon = Number(weaponButton.dataset.weapon); focusedGun = null; activeStickerSlot = 0; selectedStickerPresetId = ""; $("skinSearch").value = ""; renderGuns(); return; }
  const knifeButton = event.target.closest("[data-knife-type]");
  if (knifeButton && !event.target.closest("[data-knife-enable]")) { activeKnife = Number(knifeButton.dataset.knifeType); focusedKnife = null; $("knifeSkinSearch").value = ""; renderKnives(); return; }
  const gunCard = event.target.closest("[data-gun-paint]");
  if (gunCard) {
    focusedGun = weapons.find(item => item.id === activeWeapon)?.skins.find(skin => Number(skin.paint) === Number(gunCard.dataset.gunPaint));
    activeStickerSlot = 0;
    if (focusedGun && event.target.closest("[data-toggle]")) toggleGunSkin(focusedGun); else renderGuns(); return;
  }
  const knifeCard = event.target.closest("[data-knife-paint]");
  if (knifeCard) {
    focusedKnife = knives.find(item => item.id === activeKnife)?.skins.find(skin => Number(skin.paint) === Number(knifeCard.dataset.knifePaint));
    if (focusedKnife && event.target.closest("[data-toggle]")) toggleKnifeSkin(focusedKnife); else renderKnives();
  }
});

document.addEventListener("change", event => {
  const enable = event.target.closest("[data-knife-enable]");
  if (!enable) return;
  const id = Number(enable.dataset.knifeEnable), index = config.knife_def_indexes.indexOf(id);
  if (enable.checked && index < 0) config.knife_def_indexes.push(id);
  if (!enable.checked && index >= 0) config.knife_def_indexes.splice(index, 1);
  if (!(String(id) in config.knife_paint_kits_by_def_index)) config.knife_paint_kits_by_def_index[String(id)] = [];
  config.knife_def_indexes.sort((a, b) => a - b); setDirty(); renderKnives();
});

$("weaponSearch").addEventListener("input", renderGuns); $("skinSearch").addEventListener("input", renderGuns);
$("knifeTypeSearch").addEventListener("input", renderKnives); $("knifeSkinSearch").addEventListener("input", renderKnives);
$("toggleFocused").addEventListener("click", () => focusedGun && toggleGunSkin(focusedGun));
$("knifeToggleFocused").addEventListener("click", () => focusedKnife && toggleKnifeSkin(focusedKnife));
$("gunSkinTab").addEventListener("click", () => { gunDetailTab = "skin"; renderGunDetail(); });
$("gunStickerTab").addEventListener("click", () => { gunDetailTab = "stickers"; renderGunDetail(); });

$("stickersEnabled").addEventListener("change", event => { config.stickers_enabled = event.target.checked; setDirty(); renderGunDetail(); });
$("stickerPresetSelect").addEventListener("change", event => {
  selectedStickerPresetId = event.target.value;
  const preset = weaponStickerPresets().find(item => item.id === selectedStickerPresetId);
  $("stickerPresetName").value = preset?.name || "";
  renderGunDetail();
});
$("saveStickerPreset").addEventListener("click", saveStickerPreset);
$("updateStickerPreset").addEventListener("click", updateStickerPreset);
$("applyStickerPreset").addEventListener("click", applyStickerPreset);
$("deleteStickerPreset").addEventListener("click", deleteStickerPreset);
$("chooseSticker").addEventListener("click", openStickerPicker);
$("applyStickerFour").addEventListener("click", () => fillStickerSlots(4));
$("applyStickerAll").addEventListener("click", () => fillStickerSlots(stickerSlotCount()));
$("clearStickerSlot").addEventListener("click", () => {
  if (!focusedGun) return;
  const settings = gunSettings(focusedGun, true);
  settings.stickers = stickerSlots(settings).filter(sticker => sticker.slot !== activeStickerSlot);
  setDirty(); renderGunDetail();
});
$("stickerWear").addEventListener("input", event => {
  const value = clampInt(event.target.value, 0, 100);
  $("stickerWearValue").textContent = `${value}%`;
  updateActiveSticker({wear: value / 100});
});
$("stickerRotation").addEventListener("input", event => {
  const value = clampInt(event.target.value, -180, 180);
  $("stickerRotationValue").textContent = `${value}°`;
  updateActiveSticker({rotation: value});
});
for (const [id, key] of [["stickerX", "x"], ["stickerY", "y"]]) {
  $(id).addEventListener("change", event => {
    const value = Math.max(-1, Math.min(1, Number(event.target.value) || 0));
    event.target.value = value;
    updateActiveSticker({[key]: value});
  });
}
$("resetStickerTransform").addEventListener("click", () => updateActiveSticker({wear: 0, rotation: 0, x: 0, y: 0}));
$("stickerSearch").addEventListener("input", renderStickerResults);
$("stickerFinish").addEventListener("change", renderStickerResults);
$("closeStickerPicker").addEventListener("click", closeStickerPicker);
$("stickerPickerModal").addEventListener("click", event => { if (event.target === $("stickerPickerModal")) closeStickerPicker(); });

$("wearSlider").addEventListener("input", event => { $("wearNumber").value = event.target.value; updateGunSettings({wear: Number(event.target.value)}); });
$("wearNumber").addEventListener("change", event => clampWear(event, $("wearSlider"), updateGunSettings));
$("seedNumber").addEventListener("change", event => updateSeed(event, updateGunSettings));
$("statTrak").addEventListener("change", event => { updateGunSettings({stattrak: event.target.checked}); renderGunDetail(); });
$("statMin").addEventListener("change", event => { const value = clampInt(event.target.value, 0, 999999); event.target.value = value; updateGunSettings({stattrak_min: value}); });
$("statMax").addEventListener("change", event => { const value = clampInt(event.target.value, Number($("statMin").value), 999999); event.target.value = value; updateGunSettings({stattrak_max: value}); });
$("knifeWearSlider").addEventListener("input", event => { $("knifeWearNumber").value = event.target.value; updateKnifeSettings({wear: Number(event.target.value)}); });
$("knifeWearNumber").addEventListener("change", event => clampWear(event, $("knifeWearSlider"), updateKnifeSettings));
$("knifeSeedNumber").addEventListener("change", event => updateSeed(event, updateKnifeSettings));

function updateGunSettings(patch) {
  if (!focusedGun || !selectedGunPaints(focusedGun.weapon_defindex).includes(Number(focusedGun.paint))) return;
  Object.assign(gunSettings(focusedGun, true), patch); setDirty();
}
function updateKnifeSettings(patch) {
  if (!focusedKnife || !selectedKnifePaints(focusedKnife.knife_defindex).includes(Number(focusedKnife.paint))) return;
  Object.assign(knifeSettings(focusedKnife, true), patch); setDirty();
}
function clampInt(value, min, max) { return Math.min(max, Math.max(min, Math.round(Number(value)))); }
function clampWear(event, slider, update) {
  const value = Math.min(Number(event.target.max), Math.max(Number(event.target.min), Number(event.target.value)));
  event.target.value = value; slider.value = value; update({wear: value});
}
function updateSeed(event, update) { const value = clampInt(event.target.value, 0, 1000); event.target.value = value; update({seed: value}); }

$("selectVisible").addEventListener("click", () => {
  const key = String(activeWeapon), visible = filteredGunSkins();
  config.weapon_paint_kits[key] = [...new Set([...selectedGunPaints(activeWeapon), ...visible.map(skin => Number(skin.paint))])].sort((a, b) => a - b);
  for (const skin of visible) gunSettings(skin, true); setDirty(); renderGuns();
});
$("clearWeapon").addEventListener("click", () => { delete config.weapon_paint_kits[String(activeWeapon)]; delete config.weapon_skin_settings[String(activeWeapon)]; setDirty(); renderGuns(); });
$("knifeSelectVisible").addEventListener("click", () => {
  const key = String(activeKnife), visible = filteredKnifeSkins();
  config.knife_paint_kits_by_def_index[key] = [...new Set([...selectedKnifePaints(activeKnife), ...visible.map(skin => Number(skin.paint))])].sort((a, b) => a - b);
  for (const skin of visible) knifeSettings(skin, true); setDirty(); renderKnives();
});
$("knifeClear").addEventListener("click", () => { config.knife_paint_kits_by_def_index[String(activeKnife)] = []; delete config.knife_skin_settings_by_def_index[String(activeKnife)]; setDirty(); renderKnives(); });

$("browse").addEventListener("click", async () => { try { applyLoaded(await api("/api/browse", {})); } catch (error) { showToast(error.message, true); } });
$("reload").addEventListener("click", async () => { try { applyLoaded(await api("/api/load", {root: $("rootPath").value.trim()})); showToast("配置已重新加载"); } catch (error) { showToast(error.message, true); } });
$("save").addEventListener("click", async () => {
  if (!config.knife_def_indexes.length) { showToast("至少启用一个刀型", true); return; }
  try {
    const result = await api("/api/save", {root: $("rootPath").value.trim(), config});
    renderInstallationStatus(result.status); setDirty(false);
    if (!result.status?.names_ready) showToast("配置已保存，但 Bot 模式或名字组件未就绪，游戏内插件可能不会生效", true);
    else showToast(`已保存：${result.path}`);
  }
  catch (error) { showToast(error.message, true); }
});
$("enableBotMode").addEventListener("click", async () => {
  try {
    const result = await api("/api/enable-bot-mode", {root: $("rootPath").value.trim()});
    renderInstallationStatus(result.status);
    showToast("已启用 Bot 模式，请完全退出并重新启动 CS2");
  } catch (error) { showToast(error.message, true); }
});
$("openOriginal").addEventListener("click", async () => { try { await api("/api/launch-original", {}); } catch (error) { showToast(error.message, true); } });
$("uninstall").addEventListener("click", async () => {
  try {
    uninstallInfo = await api("/api/uninstall-info", {root: $("rootPath").value.trim()});
    if (!uninstallInfo.can_uninstall) {
      showToast(`请先退出：${uninstallInfo.blockers.join("、")}`, true);
      return;
    }
    $("uninstallCount").textContent = uninstallInfo.file_count;
    $("uninstallRoot").textContent = uninstallInfo.root;
    setUninstallStep(1);
    $("uninstallModal").hidden = false;
    $("uninstallCancel").focus();
  } catch (error) { showToast(error.message, true); }
});
$("uninstallCancel").addEventListener("click", closeUninstallModal);
$("uninstallContinue").addEventListener("click", () => setUninstallStep(2));
$("uninstallConfirm").addEventListener("click", async () => {
  const hadUnsavedChanges = dirty;
  try {
    $("uninstallConfirm").disabled = true;
    $("uninstallCancel").disabled = true;
    setDirty(false);
    await api("/api/uninstall", {root: uninstallInfo.root});
  } catch (error) {
    $("uninstallConfirm").disabled = false;
    $("uninstallCancel").disabled = false;
    setDirty(hadUnsavedChanges);
    closeUninstallModal();
    showToast(error.message, true);
  }
});
$("uninstallModal").addEventListener("click", event => { if (event.target === $("uninstallModal")) closeUninstallModal(); });
document.addEventListener("keydown", event => {
  if (event.key !== "Escape") return;
  if (!$("stickerPickerModal").hidden) closeStickerPicker();
  else if (!$("uninstallModal").hidden) closeUninstallModal();
});
document.addEventListener("error", event => {
  if (event.target instanceof HTMLImageElement) event.target.classList.add("sticker-image-failed");
}, true);
$("shutdown").addEventListener("click", async () => { try { await api("/api/shutdown", {}); } catch {} });
window.addEventListener("beforeunload", event => { if (dirty) { event.preventDefault(); event.returnValue = ""; } });

function setUninstallStep(step) {
  $("uninstallFirst").hidden = step !== 1;
  $("uninstallSecond").hidden = step !== 2;
  $("uninstallContinue").hidden = step !== 1;
  $("uninstallConfirm").hidden = step !== 2;
  $("uninstallSubtitle").textContent = step === 1 ? "卸载前请确认以下事项" : "请再次确认卸载范围";
  if (step === 2) $("uninstallConfirm").focus();
}

function closeUninstallModal() {
  $("uninstallModal").hidden = true;
  uninstallInfo = null;
  $("uninstallConfirm").disabled = false;
  $("uninstallCancel").disabled = false;
}

(async function init() {
  try {
    const [bootstrap, guns, knifeSkins, stickerDocument] = await Promise.all([
      api("/api/bootstrap"),
      loadCatalog("/catalog.json"),
      loadCatalog("/knife-catalog.json"),
      loadCatalog("/sticker-catalog.json")
    ]);
    gunCatalog = guns;
    knifeCatalog = knifeSkins;
    stickerCatalog = Array.isArray(stickerDocument?.stickers) ? stickerDocument.stickers : [];
    stickerWeaponSlots = stickerDocument?.weaponSlots || {};
    for (const sticker of stickerCatalog) {
      sticker.search = normalizeSearch(`${sticker.name} ${sticker.english} ${sticker.id} ${sticker.finish} ${FINISH_LABELS[sticker.finish] || ""}`);
    }
    stickersById = new Map(stickerCatalog.map(sticker => [Number(sticker.id), sticker]));
    groupCatalogs();
    $("openOriginal").disabled = !bootstrap.original_panel_available; applyLoaded(bootstrap);
  } catch (error) { $("saveState").textContent = "载入失败"; showToast(error.message || "装备面板载入失败", true); }
})();
