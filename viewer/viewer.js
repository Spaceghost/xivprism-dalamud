// Generated from native Imba; edit viewer/viewer.imba.
(() => {
  // node_modules/imba/src/imba/runtime.mjs
  var __imba__$ = /* @__PURE__ */ Symbol.for("imba");
  var HAS = {
    SUPERCALLS: 1 << 3,
    CONSTRUCTOR: 1 << 4
  };
  var ClassFlags = {
    IsExtension: 1 << 0,
    IsTag: 1 << 1,
    HasDescriptors: 1 << 2,
    HasSuperCalls: 1 << 3,
    HasConstructor: 1 << 4,
    HasFields: 1 << 5,
    HasMixins: 1 << 6,
    HasInitor: 1 << 7,
    HasDecorators: 1 << 8,
    IsObjectExtension: 1 << 9,
    IsMixin: 1 << 10
  };
  var mmap = /* @__PURE__ */ new Map();
  var state = globalThis[__imba__$] ||= {
    counter: 0,
    classes: mmap
  };
  function iterable$(a) {
    return a?.toIterable?.() || a;
  }
  var sup = {
    cache: {},
    self: null,
    target: null,
    proxy: new Proxy({}, {
      apply: (_, thisArg, args) => {
        return Reflect.apply(sup.target, thisArg, args);
      },
      get: (_, key) => {
        let val = Reflect.get(sup.target, key, sup.self);
        if (val instanceof Function) return val.bind(sup.self);
        return val;
      },
      set: (_, key, value, receiver) => {
        return Reflect.set(sup.target, key, value, sup.self);
      }
    })
  };

  // viewer/profile.js
  var panels = ["player", "target", "party", "sources"];
  var defaults = {
    Peripheral: { player: { x: 0.01, y: 0.02, w: 0.29, h: 0.28 }, target: { x: 0.7, y: 0.02, w: 0.29, h: 0.28 }, party: { x: 0.01, y: 0.6, w: 0.29, h: 0.38 }, sources: { x: 0.7, y: 0.6, w: 0.29, h: 0.38 } },
    Desk: { player: { x: 0.02, y: 0.02, w: 0.46, h: 0.3 }, target: { x: 0.52, y: 0.02, w: 0.46, h: 0.3 }, party: { x: 0.02, y: 0.36, w: 0.46, h: 0.61 }, sources: { x: 0.52, y: 0.36, w: 0.46, h: 0.61 } }
  };
  function validProfiles(value) {
    if (!value || value.version != 1 || !value.profiles || !Object.keys(value.profiles).length || Object.keys(value.profiles).length > 16) {
      return false;
    }
    ;
    for (let [name, layout2] of iterable$(Object.entries(value.profiles))) {
      if (!name.trim() || name.length > 64 || ["__proto__", "constructor", "prototype"].includes(name) || !layout2 || Object.keys(layout2).length != 4) {
        return false;
      }
      ;
      for (let key of iterable$(panels)) {
        const r = layout2[key];
        if (!r || !["x", "y", "w", "h"].every(function(k) {
          return typeof r[k] == "number" && Number.isFinite(r[k]);
        }) || r.x < 0 || r.y < 0 || r.w < 0.1 || r.h < 0.1 || r.x + r.w > 1.001 || r.y + r.h > 1.001) {
          return false;
        }
        ;
      }
      ;
    }
    ;
    return true;
  }

  // viewer/viewer/viewer.js
  var byId = function(id) {
    return globalThis.document.getElementById(id);
  };
  var profiles = structuredClone(defaults);
  var selected = "Peripheral";
  var token = "";
  try {
    token = sessionStorage.getItem("xivprism.viewer") || "";
  } catch ($1) {
  }
  var lastReceipt = 0;
  var stateAge = 0;
  var live = false;
  var arranging = false;
  var suggested = "";
  try {
    const saved = JSON.parse(localStorage.getItem("xivprism.layouts") || "null");
    if (validProfiles(saved)) {
      profiles = saved.profiles;
    }
    ;
  } catch ($2) {
  }
  function rememberToken(value) {
    try {
      if (value) {
        return sessionStorage.setItem("xivprism.viewer", value);
      } else {
        return sessionStorage.removeItem("xivprism.viewer");
      }
      ;
    } catch ($3) {
      return void 0;
    }
    ;
  }
  function persist() {
    try {
      return localStorage.setItem("xivprism.layouts", JSON.stringify({ version: 1, profiles }));
    } catch ($4) {
      return byId("connection").textContent = "Layouts stay in memory here; export to keep a copy";
    }
    ;
  }
  function layout() {
    var $5;
    $5 = [];
    for (let [index, key] of iterable$(panels.entries())) {
      const card = globalThis.document.querySelector('[data-panel="' + key + '"]');
      const r = profiles[selected][key];
      const mobile = innerWidth < 600;
      card.style.left = (mobile ? 1 : r.x * 100) + "%";
      card.style.top = (mobile ? index * 25 : r.y * 100) + "%";
      card.style.width = (mobile ? 98 : r.w * 100) + "%";
      $5.push(card.style.height = (mobile ? 24 : r.h * 100) + "%");
    }
    ;
    return $5;
  }
  function choose() {
    byId("profile").replaceChildren();
    for (let name of iterable$(Object.keys(profiles))) {
      const option = globalThis.document.createElement("option");
      option.value = name;
      option.textContent = name;
      byId("profile").append(option);
    }
    ;
    if (!Object.hasOwn(profiles, selected)) {
      selected = Object.keys(profiles)[0];
    }
    ;
    byId("profile").value = selected;
    return layout();
  }
  function quiet(message) {
    var $6;
    live = false;
    byId("canvas").hidden = true;
    byId("idle").hidden = !token;
    byId("connection").textContent = message;
    $6 = [];
    for (let key of iterable$(panels)) {
      $6.push(globalThis.document.querySelector('[data-panel="' + key + '"] div').replaceChildren());
    }
    ;
    return $6;
  }
  function line(parent, text, style = "") {
    const p = globalThis.document.createElement("p");
    p.textContent = String(text);
    p.className = style;
    return parent.append(p);
  }
  function actor(parent, value) {
    if (!value) {
      line(parent, "No current reading", "muted");
      return;
    }
    ;
    line(parent, value.name);
    line(parent, value.job + " \xB7 Level " + value.level, "muted");
    line(parent, "HP " + value.hp.toLocaleString() + " / " + value.maxHp.toLocaleString());
    const bar = globalThis.document.createElement("div");
    bar.className = "resource";
    const fill = globalThis.document.createElement("span");
    fill.style.width = (value.maxHp > 0 ? Math.max(0, Math.min(100, value.hp / value.maxHp * 100)) : 0) + "%";
    bar.append(fill);
    parent.append(bar);
    return line(parent, "MP " + value.mp.toLocaleString() + " / " + value.maxMp.toLocaleString(), "muted");
  }
  function render(frame) {
    live = true;
    byId("canvas").hidden = false;
    byId("idle").hidden = true;
    byId("pairing").hidden = true;
    byId("connection").textContent = "Live \xB7 read-only";
    const contents = Object.fromEntries(panels.map(function(key) {
      return [key, globalThis.document.querySelector('[data-panel="' + key + '"] div')];
    }));
    Object.values(contents).forEach(function(node) {
      return node.replaceChildren();
    });
    actor(contents.player, frame.player);
    actor(contents.target, frame.target);
    for (let member of iterable$(frame.party)) {
      line(contents.party, member.name + " \xB7 " + member.job + " \xB7 HP " + member.hp.toLocaleString() + "/" + member.maxHp.toLocaleString());
    }
    ;
    if (!frame.party.length) {
      line(contents.party, "Travelling on your own", "muted");
    }
    ;
    for (let source of iterable$(frame.sources)) {
      line(contents.sources, source.name + " \xB7 " + (source.ready ? "ready \xB7 " + source.mode : source.available ? "waiting" : "not installed") + (source.count ? " \xB7 " + source.count + " quests" : "") + (source.info ? " \xB7 " + source.info : ""), source.ready ? "ready" : "muted");
    }
    ;
    byId("scene").textContent = frame.scene ? "Camp \xB7 " + frame.scene : "";
    suggested = frame.profile || "";
    byId("recommendation").hidden = !Object.hasOwn(profiles, suggested) || suggested == selected;
    return byId("recommendation").textContent = "Use Camp\u2019s " + suggested + " layout";
  }
  byId("pair").addEventListener("submit", async function(event) {
    event.preventDefault();
    byId("pair-error").textContent = "";
    try {
      const response = await fetch("/api/pair", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: byId("code").value }), signal: AbortSignal.timeout(3e3) });
      if (!response.ok) {
        throw new Error("Code expired or refused. Get a new code from the game and try again.");
      }
      ;
      const result = await response.json();
      token = result.token;
      rememberToken(token);
      byId("code").value = "";
      return byId("pairing").hidden = true;
    } catch (error) {
      return byId("pair-error").textContent = error.message;
    }
    ;
  });
  async function poll() {
    try {
      if (token && !globalThis.document.hidden) {
        const response = await fetch("/api/state", { headers: { Authorization: "Bearer " + token }, cache: "no-store", signal: AbortSignal.timeout(2e3) });
        if (response.status == 401) {
          token = "";
          rememberToken("");
          byId("pairing").hidden = false;
          return quiet("Pair again \xB7 viewer expired or revoked");
        } else {
          if (!response.ok) {
            throw new Error("Publisher unavailable");
          }
          ;
          const result = await response.json();
          if (result.version != 1 || !result.live || !result.state || result.ageMs > 2e3) {
            return quiet("Waiting for fresh game data");
          } else {
            lastReceipt = performance.now();
            stateAge = Math.max(0, result.ageMs);
            return render(result.state);
          }
          ;
        }
        ;
      }
      ;
    } catch ($7) {
      return quiet("Disconnected \xB7 details hidden");
    } finally {
      setTimeout(poll, 500);
    }
    ;
  }
  setInterval(function() {
    if (live && (globalThis.document.hidden || performance.now() - lastReceipt + stateAge > 2e3)) {
      return quiet("Stale \xB7 details hidden");
    }
    ;
  }, 200);
  byId("profile").addEventListener("change", function() {
    selected = byId("profile").value;
    return layout();
  });
  byId("recommendation").addEventListener("click", function() {
    if (Object.hasOwn(profiles, suggested)) {
      selected = suggested;
      byId("profile").value = selected;
      layout();
      return byId("recommendation").hidden = true;
    }
    ;
  });
  byId("arrange").addEventListener("click", function() {
    arranging = !arranging;
    globalThis.document.body.classList.toggle("arranging", arranging);
    return byId("arrange").textContent = arranging ? "Finish arranging" : "Arrange panels";
  });
  byId("save").addEventListener("click", function() {
    const name = prompt("Name this device\u2019s layout", selected);
    if (!name || name.length > 64 || ["__proto__", "constructor", "prototype"].includes(name) || !Object.hasOwn(profiles, name) && Object.keys(profiles).length >= 16) {
      return;
    }
    ;
    const canvas = byId("canvas").getBoundingClientRect();
    if (!canvas.width || !canvas.height) {
      return;
    }
    ;
    const result = {};
    for (let key of iterable$(panels)) {
      const r = globalThis.document.querySelector('[data-panel="' + key + '"]').getBoundingClientRect();
      const w = Math.max(0.1, Math.min(1, r.width / canvas.width));
      const h = Math.max(0.1, Math.min(1, r.height / canvas.height));
      result[key] = { x: Math.max(0, Math.min(1 - w, (r.left - canvas.left) / canvas.width)), y: Math.max(0, Math.min(1 - h, (r.top - canvas.top) / canvas.height)), w, h };
    }
    ;
    profiles[name] = result;
    selected = name;
    persist();
    return choose();
  });
  byId("forget").addEventListener("click", function() {
    token = "";
    rememberToken("");
    byId("pairing").hidden = false;
    return quiet("Disconnected");
  });
  byId("full").addEventListener("click", async function() {
    try {
      if (globalThis.document.fullscreenElement) {
        return await globalThis.document.exitFullscreen();
      } else {
        return await globalThis.document.documentElement.requestFullscreen();
      }
      ;
    } catch ($8) {
      return byId("connection").textContent = "Full screen is unavailable in this browser";
    }
    ;
  });
  byId("export").addEventListener("click", function() {
    const url = URL.createObjectURL(new Blob([JSON.stringify({ version: 1, profiles }, null, 2)], { type: "application/json" }));
    const a = globalThis.document.createElement("a");
    a.href = url;
    a.download = "xivprism-layouts.json";
    a.click();
    return setTimeout(function() {
      return URL.revokeObjectURL(url);
    }, 1e3);
  });
  byId("import").addEventListener("change", async function() {
    try {
      const file = byId("import").files[0];
      if (!file || file.size > 65536) {
        throw new Error("Choose a layout file smaller than 64 KiB");
      }
      ;
      const value = JSON.parse(await file.text());
      if (!validProfiles(value)) {
        throw new Error("Unsupported or invalid layout file");
      }
      ;
      profiles = value.profiles;
      persist();
      return choose();
    } catch (error) {
      return byId("connection").textContent = error.message;
    }
    ;
  });
  for (let card of iterable$(globalThis.document.querySelectorAll("[data-panel]"))) {
    card.querySelector("h2").addEventListener("pointerdown", function(event) {
      if (!arranging) {
        return;
      }
      ;
      const start = { x: event.clientX, y: event.clientY, left: card.offsetLeft, top: card.offsetTop };
      card.querySelector("h2").setPointerCapture(event.pointerId);
      const move = function(e) {
        card.style.left = Math.max(0, Math.min(byId("canvas").clientWidth - card.offsetWidth, start.left + e.clientX - start.x)) + "px";
        return card.style.top = Math.max(0, Math.min(byId("canvas").clientHeight - card.offsetHeight, start.top + e.clientY - start.y)) + "px";
      };
      const end = function() {
        return card.querySelector("h2").removeEventListener("pointermove", move);
      };
      card.querySelector("h2").addEventListener("pointermove", move);
      return card.querySelector("h2").addEventListener("pointerup", end, { once: true });
    });
  }
  addEventListener("resize", layout);
  choose();
  byId("pairing").hidden = !!token;
  quiet(token ? "Connecting\u2026" : "Not paired");
  poll();
  globalThis.document.addEventListener("visibilitychange", function() {
    if (globalThis.document.hidden) {
      return quiet("Paused while this viewer is hidden");
    }
    ;
  });
})();
