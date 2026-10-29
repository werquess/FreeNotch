(() => {
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
  const reduced = matchMedia("(prefers-reduced-motion: reduce)").matches;

  const REPO = "werquess/FreeNotch";
  const screen = $("#screen");
  const island = $("#island");
  const edgeDrop = $("[data-edge-drop]");
  const edgeZone = $("[data-edge-zone]");
  const body = document.body;

  const TRACKS = [
    {
      title: "Midnight Flow", artist: "The Demo Tapes", source: "Spotify", dot: "#1ed760", len: 214,
      art: "radial-gradient(circle at 30% 28%, #ffd6e6 0%, #ff4d8d 34%, #8a5cff 68%, #1e0d3a 100%)", glow: "rgba(255,77,141,.55)",
      lyrics: ["City lights are slowing down", "I can hear the engine hum", "Every window paints a song", "Somewhere a radio is on", "Hold the night a little longer", "Let the static keep us warm", "Midnight flow, midnight flow", "Carry me where the streetlights go", "Every corner knows our names", "Every signal sounds the same", "Midnight flow, midnight flow", "Turn it up and let it go"]
    },
    {
      title: "Low Tide Static", artist: "Harbor Lights", source: "YouTube in Brave", dot: "#ff3b30", len: 187,
      art: "radial-gradient(circle at 70% 26%, #d4f3ff 0%, #3fa9ff 36%, #2335a8 70%, #070b25 100%)", glow: "rgba(63,169,255,.55)",
      lyrics: ["Waves are folding into grey", "Tuning in from far away", "Low tide, low tide", "Static on the other side", "Count the boats along the bay", "Every light has gone to sleep", "Low tide, low tide", "Hold the signal, let it keep"]
    },
    {
      title: "Paper Lanterns", artist: "Sora and the Night Shift", source: "Apple Music", dot: "#fa2d48", len: 241,
      art: "conic-gradient(from 210deg at 50% 62%, #ffcf5a, #ff7a45, #ff4d8d, #8a5cff, #ffcf5a)", glow: "rgba(255,160,90,.5)",
      lyrics: ["Paper lanterns on the line", "Light them one at a time", "Every color, every street", "Folding up beneath our feet", "Carry one and carry two", "Every glow comes back to you", "Paper lanterns, paper light", "Hold them up into the night"]
    }
  ];

  const ART = {
    media: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><circle cx="146" cy="70" r="78" fill="#140c16"/>${[70, 63, 56, 49, 42, 35].map(r => `<circle cx="146" cy="70" r="${r}" fill="none" stroke="rgba(255,255,255,.08)" stroke-width="1.2"/>`).join("")}<circle cx="146" cy="70" r="24" fill="#fff"/><circle cx="146" cy="70" r="3" fill="#ff4d8d"/></svg>`,
    lyrics: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><g font-family="Archivo, sans-serif" font-weight="800" font-size="17" font-stretch="85%"><text x="14" y="34" fill="rgba(255,255,255,.32)">city lights are</text><text x="14" y="56" fill="rgba(255,255,255,.32)">slowing down</text><text x="14" y="84" fill="#fff" font-size="21">i can hear the</text><text x="14" y="108" fill="#fff" font-size="21">engine hum</text><text x="14" y="134" fill="rgba(255,255,255,.22)">every window</text></g></svg>`,
    shelf: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><rect x="70" y="22" width="92" height="112" rx="8" fill="#0b2a4a" transform="rotate(10 116 78)"/><rect x="54" y="30" width="92" height="112" rx="8" fill="#f3f6fb" transform="rotate(-4 100 86)"/><path d="M118 32 l26 26 h-26z" fill="#cfe6ff" transform="rotate(-4 100 86)"/><rect x="40" y="56" width="70" height="56" rx="6" fill="#ff4d8d" transform="rotate(-12 75 84)"/></svg>`,
    hud: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><text x="12" y="104" font-family="Archivo, sans-serif" font-weight="900" font-stretch="62%" font-size="112" fill="#0b0a10" letter-spacing="-2">VOL</text><text x="140" y="44" font-family="Archivo, sans-serif" font-weight="800" font-stretch="75%" font-size="30" fill="#3f5bff">62</text>${Array.from({length: 16}, (_, i) => `<rect x="${12 + i * 11.4}" y="120" width="7" height="16" fill="${i < 10 ? "#0b0a10" : "rgba(11,10,16,.16)"}"/>`).join("")}</svg>`,
    clipboard: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><g font-family="Archivo, sans-serif" font-weight="900" font-stretch="62%" font-size="44" letter-spacing="-1">${[0, 1, 2].map(i => `<text x="${10 + i * 14}" y="${46 + i * 36}" fill="#ff4d8d" opacity="${1 - i * 0.28}">COPY COPY</text>`).join("")}<text x="10" y="148" fill="#fff">PASTE</text></g></svg>`,
    notes: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><g font-family="Archivo, sans-serif" font-weight="800" font-stretch="75%" font-size="22" fill="rgba(255,255,255,.5)"><text x="12" y="40">•••• •••••• •••</text><text x="12" y="70">••••••• •• ••••</text><text x="12" y="100">•• •••••••• ••</text></g><text x="12" y="136" font-family="Archivo, sans-serif" font-weight="900" font-stretch="62%" font-size="40" fill="#fff">AES-GCM</text></svg>`,
    focus: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><text x="-6" y="150" font-family="Archivo, sans-serif" font-weight="900" font-stretch="62%" font-size="200" fill="#ff4d8d" letter-spacing="-6">25</text><text x="132" y="44" font-family="Archivo, sans-serif" font-weight="800" font-stretch="75%" font-size="30" fill="#0b0a10">:00</text></svg>`,
    system: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><defs><linearGradient id="sg" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#ff4d8d"/><stop offset=".5" stop-color="#8a5cff"/><stop offset="1" stop-color="#3fa9ff"/></linearGradient></defs>${[[18, 70], [42, 40], [66, 96], [90, 58], [114, 120], [138, 82], [162, 104]].map(([x, h]) => `<rect x="${x}" y="${132 - h}" width="16" height="${h}" rx="3" fill="url(#sg)"/>`).join("")}</svg>`,
    tools: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><g font-family="Archivo, sans-serif" font-weight="900" font-stretch="62%" font-size="46" letter-spacing="-1"><text x="12" y="48" fill="#fff">MIRROR</text><text x="12" y="92" fill="#0b0a10">AWAKE</text><text x="12" y="136" fill="#fff">#FF4D8D</text></g></svg>`,
    drop: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><defs><linearGradient id="dg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3a383e"/><stop offset="1" stop-color="#0c0c0f"/></linearGradient></defs><rect width="200" height="200" fill="url(#dg)"/><path d="M100 14 C100 14 84 44 84 58 a16 16 0 0 0 32 0 C116 44 100 14 100 14z" fill="#e9e6ef"/><rect x="44" y="96" width="112" height="30" rx="15" fill="#060608" stroke="rgba(255,255,255,.22)"/><path d="M60 140 q40 10 80 0" stroke="rgba(255,255,255,.18)" stroke-width="2" fill="none"/></svg>`,
    themes: `<svg viewBox="0 0 200 200" preserveAspectRatio="xMidYMid slice"><defs><linearGradient id="tg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#47444b"/><stop offset="1" stop-color="#101013"/></linearGradient></defs><rect width="100" height="200" fill="url(#tg)"/><rect x="100" width="100" height="200" fill="#000"/><rect x="30" y="44" width="140" height="34" rx="17" fill="#0d0d10" stroke="rgba(255,255,255,.2)"/><rect x="62" y="96" width="76" height="26" rx="13" fill="#000" stroke="rgba(255,255,255,.12)"/></svg>`
  };

  const SLEEVES = [
    { id: "media", title: "Now Playing", sub: "Spotify, Apple Music and your browser", c: "#ff4d8d", os: ["mac", "win"] },
    { id: "lyrics", title: "Synced Lyrics", sub: "Click a line to jump to it", c: "#8a5cff", fg: "#fff", os: ["mac"] },
    { id: "drop", title: "The Drop", sub: "Falls from the top edge", c: "#16151a", fg: "#fff", os: ["win"] },
    { id: "shelf", title: "The Shelf", sub: "A drop zone for files", c: "#3fa9ff", os: ["mac"] },
    { id: "hud", title: "Volume HUD", sub: "Replaces the system overlay", c: "#f4f2f7", os: ["mac", "win"] },
    { id: "themes", title: "Smoke and OLED", sub: "Two shades of dark", c: "#000", fg: "#fff", os: ["win"] },
    { id: "clipboard", title: "Clipboard", sub: "Everything you copied", c: "#0b0a10", fg: "#fff", os: ["mac"] },
    { id: "notes", title: "Locked Notes", sub: "Encrypted with AES-GCM", c: "#8a5cff", fg: "#fff", os: ["mac"] },
    { id: "focus", title: "Focus", sub: "Pomodoro and stopwatch", c: "#f4f2f7", os: ["mac"] },
    { id: "system", title: "System", sub: "CPU, memory, °C and RPM", c: "#15131c", fg: "#fff", os: ["mac"] },
    { id: "tools", title: "Tools", sub: "Mirror, Keep Awake, Color Picker", c: "#3fa9ff", os: ["mac"] }
  ];

  const SIZE = {
    mac: {
      idle: [200, 32], playing: [300, 32], pomo: [300, 32], hud: [340, 32],
      media: [620, 196], lyrics: [620, 236], shelf: [620, 200], clipboard: [620, 230], notes: [620, 250],
      focus: [620, 196], system: [620, 184], hud_panel: [620, 168], tools: [620, 214]
    },
    win: {
      idle: [132, 34], playing: [236, 40], pomo: [236, 40], hud: [280, 38],
      media: [580, 184], themes: [440, 156], drop: [500, 120]
    }
  };
  const MOBILE_H = { focus: 250, system: 250, media: 196, notes: 230, tools: 214 };

  const state = {
    os: "mac",
    feature: null,
    expanded: false,
    hover: false,
    playing: true,
    started: false,
    track: 0,
    pos: 61,
    vol: 0.56,
    muted: false,
    hudUntil: 0,
    focusMode: "pomo",
    pomoLeft: 1500,
    pomoRunning: false,
    pomoEnd: 0,
    watchRunning: false,
    watchStart: 0,
    watchAcc: 0,
    laps: [],
    theme: "smoke",
    interacted: false,
    dropping: false
  };

  const els = {
    panels: $$(".panel", island),
    tabs: $$("[data-tab]", island),
    arts: $$("[data-art]"),
    titles: $$("[data-title]"),
    artists: $$("[data-artist]"),
    source: $("[data-source]"),
    sourceDot: $(".source-dot"),
    scrub: $("[data-scrub]"),
    scrubFill: $("[data-scrub-fill]"),
    pos: $("[data-pos]"),
    len: $("[data-len]"),
    playIc: $("[data-play-ic]"),
    playBtn: $('[data-act="play"]'),
    lyrics: $("[data-lyrics]"),
    hudFill: $("[data-hud-fill]"),
    hudPct: $("[data-hud-pct]"),
    hudNum: $("[data-hud-num]"),
    vol: $("[data-vol]"),
    muteIc: $("[data-mute-ic]"),
    compactVolIc: $(".compact-vol-ic use"),
    pomoCompact: $("[data-pomo-compact]"),
    focusDigits: $("[data-focus-digits]"),
    laps: $("[data-laps]"),
    toast: $("[data-toast]"),
    sleeves: $("[data-sleeves]"),
    shelf: $("[data-shelf]"),
    dropzone: $("[data-dropzone]")
  };

  const fmt = s => { s = Math.max(0, Math.floor(s)); return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`; };
  const fmt2 = s => { s = Math.max(0, Math.ceil(s)); return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`; };
  const isSmall = () => screen.clientWidth < 700;

  let toastTimer;
  function toast(msg) {
    els.toast.textContent = msg;
    els.toast.classList.add("is-on");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => els.toast.classList.remove("is-on"), 2200);
  }

  function mode() {
    if (state.expanded) return "expanded";
    if (performance.now() < state.hudUntil) return "hud";
    if (state.pomoRunning) return "pomo";
    if (state.started) return "playing";
    return "idle";
  }

  function panelKey() {
    const f = state.feature || "media";
    if (state.os === "mac" && f === "hud") return "hud_panel";
    return f;
  }

  function sizeFor(m) {
    const table = SIZE[state.os];
    let w, h;
    if (m === "expanded") {
      [w, h] = table[panelKey()] || table.media;
      if (isSmall() && MOBILE_H[state.feature]) h = MOBILE_H[state.feature];
      w = Math.min(w, screen.clientWidth - (state.os === "mac" ? 28 : 20));
    } else {
      [w, h] = table[m];
      if (state.os === "mac" && isSmall()) w = { idle: 150, playing: 210, pomo: 210, hud: 236 }[m] || w;
      if (state.hover && state.os === "mac") { w += 14; h += 4; }
    }
    return { w, h };
  }

  function winPos(size, m) {
    const W = screen.clientWidth;
    const x = clamp((W - size.w) / 2, 8, Math.max(8, W - size.w - 8));
    const y = m === "expanded" ? 22 : m === "hud" ? 0 : -size.h - 12;
    return { x, y };
  }

  function render() {
    const m = mode();
    const size = sizeFor(m);
    island.dataset.mode = m;
    island.classList.toggle("is-expanded", m === "expanded");
    island.classList.toggle("is-playing", state.playing && state.started);
    island.classList.toggle("is-paused", !state.playing);
    island.setAttribute("aria-expanded", String(m === "expanded"));
    island.style.setProperty("--w", size.w + "px");
    island.style.setProperty("--h", size.h + "px");
    let r;
    if (state.os === "mac") r = m === "expanded" ? 34 : 12;
    else r = m === "expanded" ? 30 : 16;
    island.classList.toggle("is-notch", state.os === "win" && m === "hud");
    island.classList.toggle("is-tucked", state.os === "win" && m !== "expanded" && m !== "hud");
    edgeZone.hidden = state.os !== "win" || m === "expanded";
    if (!state.dropping) island.style.setProperty("--r", r + "px");
    if (state.os === "win" && !state.dropping) {
      const p = winPos(size, m);
      island.style.setProperty("--x", p.x + "px");
      island.style.setProperty("--y", p.y + "px");
    }
    const key = state.feature || "media";
    els.panels.forEach(p => p.classList.toggle("is-on", p.dataset.panel === key));
    const tabKey = key === "lyrics" ? "media" : key;
    els.tabs.forEach(t => t.setAttribute("aria-selected", String(t.dataset.tab === tabKey)));
    $$(".sleeve", els.sleeves).forEach(s => s.classList.toggle("is-pulled", state.expanded && s.dataset.id === key));
  }

  function setTrack(i, keepPos) {
    state.track = (i + TRACKS.length) % TRACKS.length;
    const t = TRACKS[state.track];
    if (!keepPos) state.pos = 0;
    els.arts.forEach(a => { a.style.setProperty("--art", t.art); a.style.setProperty("--art-glow", t.glow); });
    els.titles.forEach(n => n.textContent = t.title);
    els.artists.forEach(n => n.textContent = t.artist);
    els.source.textContent = t.source;
    els.sourceDot.style.background = t.dot;
    els.len.textContent = fmt(t.len);
    els.lyrics.innerHTML = t.lyrics.map((l, k) => `<li data-t="${6 + k * 5.5}">${l}</li>`).join("");
    lastLyric = -1;
    updateMedia();
  }

  let lastLyric = -1;
  function updateMedia() {
    const t = TRACKS[state.track];
    const p = clamp(state.pos / t.len, 0, 1);
    els.scrubFill.style.width = (p * 100).toFixed(2) + "%";
    els.scrub.setAttribute("aria-valuenow", String(Math.round(p * 100)));
    els.scrub.setAttribute("aria-valuetext", fmt(state.pos));
    els.pos.textContent = fmt(state.pos);
    const lines = els.lyrics.children;
    let cur = -1;
    for (let k = 0; k < lines.length; k++) if (state.pos >= +lines[k].dataset.t) cur = k;
    if (cur !== lastLyric) {
      lastLyric = cur;
      for (let k = 0; k < lines.length; k++) lines[k].classList.toggle("is-on", k === cur);
      const li = lines[Math.max(cur, 0)];
      if (li && els.lyrics.clientHeight) els.lyrics.scrollTo({ top: li.offsetTop - els.lyrics.clientHeight * 0.36, behavior: reduced ? "auto" : "smooth" });
    }
  }

  function setPlaying(on) {
    state.playing = on;
    els.playIc.setAttribute("href", on ? "#i-pause" : "#i-play");
    els.playBtn.setAttribute("aria-label", on ? "Pause" : "Play");
    render();
  }

  function setVolume(v, show) {
    state.vol = clamp(v, 0, 1);
    if (state.vol > 0) state.muted = false;
    const shown = state.muted ? 0 : state.vol;
    const pct = Math.round(shown * 100);
    els.hudFill.style.width = pct + "%";
    els.hudPct.textContent = pct;
    els.hudNum.textContent = pct;
    els.vol.value = pct;
    els.vol.style.setProperty("--p", pct + "%");
    const ic = shown === 0 ? "#i-mute" : "#i-vol";
    els.muteIc.setAttribute("href", ic);
    els.compactVolIc.setAttribute("href", ic);
    if (show) showHud();
  }

  let hudTimer;
  function showHud() {
    if (state.expanded) return;
    state.hudUntil = performance.now() + 1600;
    render();
    clearTimeout(hudTimer);
    hudTimer = setTimeout(render, 1650);
  }

  function open(feature) {
    if (state.os === "win" && !state.expanded && !reduced) { openWin(feature); return; }
    state.feature = feature || state.feature || "media";
    state.expanded = true;
    state.hover = false;
    if (state.feature !== "tools") stopMirror();
    render();
    if (state.feature === "lyrics") { lastLyric = -1; setTimeout(() => { lastLyric = -1; updateMedia(); }, 450); }
    if (state.feature === "system") startStats(); else stopStats();
  }

  function collapse() {
    if (!state.expanded || state.dropping) return;
    if (state.os === "win" && !reduced) { closeWin(); return; }
    state.expanded = false;
    stopMirror();
    stopStats();
    render();
  }

  function buildSleeves() {
    const order = state.os === "mac"
      ? SLEEVES.filter(s => s.os.includes("mac"))
      : [...SLEEVES.filter(s => s.os.includes("win")), ...SLEEVES.filter(s => !s.os.includes("win"))];
    els.sleeves.innerHTML = order.map(s => {
      const soon = !s.os.includes(state.os);
      return `<button type="button" class="sleeve${soon ? " is-soon" : ""}" role="listitem" data-id="${s.id}" style="--c:${s.c};${s.fg ? `--fg:${s.fg}` : ""}" aria-label="${s.title}. ${s.sub}${soon ? ". Coming to Windows" : ""}"${soon ? ' aria-disabled="true"' : ""}>
        <span class="sleeve-art" aria-hidden="true">${ART[s.id]}</span>
        <span class="sleeve-soon" aria-hidden="true">Coming to Windows</span>
        <span class="sleeve-title" aria-hidden="true">${s.title}<span class="sleeve-sub">${s.sub}</span></span>
      </button>`;
    }).join("");
  }

  function pull(id) {
    state.interacted = true;
    const def = SLEEVES.find(s => s.id === id);
    if (!def) return;
    if (!def.os.includes(state.os)) { toast("Coming to Windows in a later release"); return; }
    if (screen.getBoundingClientRect().top < -80) window.scrollTo({ top: 0, behavior: reduced ? "auto" : "smooth" });
    try { history.replaceState(null, "", "#" + id); } catch (e) {}
    if (state.os === "win" && id === "hud") {
      const show = () => { state.feature = "hud"; setVolume(state.vol, true); toast("Scroll on the top edge or press the arrow keys"); };
      state.expanded ? (collapse(), setTimeout(show, 600)) : show();
      return;
    }
    if (state.expanded && state.feature === id) { collapse(); return; }
    if (id === "drop") { playDrop("drop"); return; }
    open(id);
  }

  function setOS(os, opts = {}) {
    if (state.os === os && !opts.force) return;
    clearTimeout(demoTimer);
    state.os = os;
    body.classList.toggle("os-mac", os === "mac");
    body.classList.toggle("os-win", os === "win");
    $$(".os-switch [data-os]").forEach(b => b.setAttribute("aria-checked", String(b.dataset.os === os)));
    state.expanded = false;
    state.dropping = false;
    state.feature = null;
    state.hudUntil = 0;
    stopMirror();
    stopStats();
    buildSleeves();
    island.classList.add("no-trans");
    island.classList.remove("is-dropping");
    render();
    requestAnimationFrame(() => {
      requestAnimationFrame(() => island.classList.remove("no-trans"));
      if (os === "win") {
        if (opts.then) { opts.then(); return; }
        toast("Move the pointer to the top edge");
        demoTimer = setTimeout(() => {
          playDrop("media", () => {
            demoTimer = setTimeout(() => { if (!state.interacted) collapse(); }, 3200);
          });
        }, 900);
      } else {
        island.animate([{ transform: "translateX(-50%) translateY(-110%)" }, { transform: "translateX(-50%) translateY(0)" }], { duration: reduced ? 1 : 520, easing: "cubic-bezier(.16,1,.3,1)" });
        if (opts.then) opts.then();
      }
    });
  }

  let demoTimer, hoverTimer, leaveTimer, openedByHover = false;
  const spread = on => edgeDrop.classList.toggle("is-spread", on);

  function playDrop(feature, done) {
    const go = () => { spread(true); setTimeout(() => openWin(feature, done), reduced ? 0 : 420); };
    if (state.expanded) { collapse(); setTimeout(go, 640); } else go();
  }

  function openWin(feature, done) {
    if (state.dropping || state.os !== "win") return;
    state.feature = feature || state.feature || "media";
    state.expanded = true;
    stopMirror();
    if (state.feature === "system") startStats(); else stopStats();
    const size = sizeFor("expanded");
    const { x, y } = winPos(size, "expanded");
    const w = size.w, h = size.h, cx = x + w / 2;
    state.dropping = true;
    island.classList.add("no-trans", "is-dropping");
    island.style.setProperty("--r", "999px");
    render();
    spread(false);
    if (reduced) { finishOpen(done); return; }
    const settle = "cubic-bezier(.2,.9,.3,1)", accel = "cubic-bezier(.55,0,.95,.5)";
    const k = (offset, l, t, ww, hh, easing) => ({ offset, left: l + "px", top: t + "px", width: ww + "px", height: hh + "px", easing });
    const frames = [
      k(0, cx - 40, -6, 80, 16, settle),
      k(0.16, cx - 11, 2, 22, 26, accel),
      k(0.42, cx - 15, y + 8, 30, 30, settle),
      k(0.7, cx - w * 0.53, y, w * 1.06, h * 0.92, settle),
      k(0.86, cx - w * 0.4925, y, w * 0.985, h * 1.02, settle),
      k(1, x, y, w, h, "linear")
    ];
    const a = island.animate(frames, { duration: 880, fill: "both" });
    a.onfinish = () => { a.cancel(); finishOpen(done); };
  }

  function finishOpen(done) {
    state.dropping = false;
    render();
    requestAnimationFrame(() => {
      island.classList.remove("no-trans", "is-dropping");
      render();
      if (state.feature === "lyrics") { lastLyric = -1; updateMedia(); }
      done && done();
    });
  }

  function closeWin() {
    const r = island.getBoundingClientRect(), sr = screen.getBoundingClientRect();
    const x = r.left - sr.left, y = r.top - sr.top, w = r.width, h = r.height, cx = x + w / 2;
    state.dropping = true;
    openedByHover = false;
    stopMirror();
    stopStats();
    island.classList.add("no-trans", "is-dropping");
    island.style.setProperty("--r", "999px");
    if (reduced) { finishClose(); return; }
    const settle = "cubic-bezier(.2,.9,.3,1)", accel = "cubic-bezier(.55,0,.95,.5)";
    const k = (offset, l, t, ww, hh, easing, o = 1) => ({ offset, left: l + "px", top: t + "px", width: ww + "px", height: hh + "px", opacity: o, easing });
    const frames = [
      k(0, x, y, w, h, settle),
      k(0.22, cx - w * 0.52, y + 2, w * 1.04, h * 0.9, accel),
      k(0.55, cx - 14, y + 10, 28, 28, accel),
      k(0.8, cx - 10, 2, 20, 24, settle),
      k(1, cx - 40, -14, 80, 12, "linear", 0)
    ];
    const a = island.animate(frames, { duration: 560, fill: "both" });
    setTimeout(() => spread(true), 380);
    a.onfinish = () => { finishClose(); a.cancel(); setTimeout(() => spread(false), 160); };
  }

  function finishClose() {
    state.expanded = false;
    state.dropping = false;
    render();
    requestAnimationFrame(() => island.classList.remove("no-trans", "is-dropping"));
  }

  edgeZone.addEventListener("mouseenter", () => {
    if (state.os !== "win" || state.expanded || state.dropping) return;
    clearTimeout(demoTimer);
    state.interacted = true;
    spread(true);
    clearTimeout(hoverTimer);
    hoverTimer = setTimeout(() => { openedByHover = true; openWin("media"); }, 560);
  });
  edgeZone.addEventListener("mouseleave", () => {
    clearTimeout(hoverTimer);
    if (!state.dropping && !state.expanded) spread(false);
  });
  edgeZone.addEventListener("click", () => {
    if (state.os !== "win" || state.expanded || state.dropping) return;
    clearTimeout(hoverTimer);
    clearTimeout(demoTimer);
    state.interacted = true;
    openedByHover = false;
    openWin("media");
  });
  edgeZone.addEventListener("wheel", e => {
    e.preventDefault();
    state.interacted = true;
    setVolume(state.vol + (e.deltaY < 0 ? 0.0625 : -0.0625), true);
  }, { passive: false });
  island.addEventListener("mouseenter", () => clearTimeout(leaveTimer));
  island.addEventListener("mouseleave", () => {
    if (state.os !== "win" || !openedByHover || !state.expanded) return;
    clearTimeout(leaveTimer);
    leaveTimer = setTimeout(() => { if (openedByHover) collapse(); }, 700);
  });

  const INTERACTIVE = "[data-act],[data-tab],[data-focus],[data-theme],.scrub,textarea,input,.lyrics li,.clips button,.tool";
  island.addEventListener("click", e => {
    if (state.dropping) return;
    state.interacted = true;
    const act = e.target.closest("[data-act]");
    if (act) { e.stopPropagation(); handleAct(act.dataset.act, act); return; }
    const tab = e.target.closest("[data-tab]");
    if (tab) { open(tab.dataset.tab); return; }
    const line = e.target.closest(".lyrics li");
    if (line) { state.pos = +line.dataset.t + 0.05; lastLyric = -1; updateMedia(); return; }
    if (e.target.closest(INTERACTIVE)) return;
    if (!state.expanded) open(state.feature === "hud" && state.os === "win" ? "media" : state.feature || "media");
  });
  island.addEventListener("keydown", e => {
    if (e.target !== island) return;
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); state.expanded ? collapse() : open(state.feature || "media"); }
  });
  island.addEventListener("mouseenter", () => { if (state.os === "mac" && !state.expanded) { state.hover = true; render(); } });
  island.addEventListener("mouseleave", () => { if (state.hover) { state.hover = false; render(); } });

  document.addEventListener("pointerdown", e => {
    if (!state.expanded) return;
    if (island.contains(e.target) || e.target.closest(".sleeve, .desk-file, .os-switch, dialog")) return;
    collapse();
  });
  document.addEventListener("keydown", e => {
    if (e.key === "Escape" && state.expanded && !$("#download").open) collapse();
    const typing = e.target.closest && e.target.closest("textarea, input, dialog");
    if (typing) return;
    if ((e.key === "ArrowUp" || e.key === "ArrowDown") && (document.activeElement === island || state.feature === "hud")) {
      e.preventDefault();
      setVolume(state.vol + (e.key === "ArrowUp" ? 0.0625 : -0.0625), true);
    }
  });

  let wheelAcc = 0;
  island.addEventListener("wheel", e => {
    if (state.expanded && e.target.closest(".clips, .lyrics, .shelf-items, textarea")) return;
    e.preventDefault();
    wheelAcc += e.deltaY;
    if (Math.abs(wheelAcc) < 24) return;
    setVolume(state.vol + (wheelAcc < 0 ? 0.0625 : -0.0625), true);
    wheelAcc = 0;
    state.interacted = true;
  }, { passive: false });

  els.vol.addEventListener("input", () => setVolume(els.vol.value / 100, false));

  let scrubbing = false;
  const scrubTo = x => {
    const r = els.scrub.getBoundingClientRect();
    state.pos = clamp((x - r.left) / r.width, 0, 1) * TRACKS[state.track].len;
    lastLyric = -1;
    updateMedia();
  };
  els.scrub.addEventListener("pointerdown", e => { scrubbing = true; els.scrub.classList.add("is-active"); els.scrub.setPointerCapture(e.pointerId); scrubTo(e.clientX); });
  els.scrub.addEventListener("pointermove", e => { if (scrubbing) scrubTo(e.clientX); });
  els.scrub.addEventListener("pointerup", () => { scrubbing = false; els.scrub.classList.remove("is-active"); });
  els.scrub.addEventListener("keydown", e => {
    if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
      e.preventDefault();
      state.pos = clamp(state.pos + (e.key === "ArrowRight" ? 5 : -5), 0, TRACKS[state.track].len);
      lastLyric = -1;
      updateMedia();
    }
  });

  function handleAct(act, el) {
    switch (act) {
      case "play": setPlaying(!state.playing); break;
      case "next": setTrack(state.track + 1); break;
      case "prev": state.pos > 3 ? (state.pos = 0, lastLyric = -1, updateMedia()) : setTrack(state.track - 1); break;
      case "lyrics": open("lyrics"); break;
      case "media": open("media"); break;
      case "mute": state.muted = !state.muted || state.vol === 0; if (!state.muted && state.vol === 0) state.vol = 0.3; setVolume(state.vol, false); if (state.muted) { state.muted = true; els.hudNum.textContent = "0"; els.muteIc.setAttribute("href", "#i-mute"); els.vol.value = 0; els.vol.style.setProperty("--p", "0%"); } break;
      case "sample-file": addSample(); break;
      case "lock": toggleLock(el); break;
      case "focus-start": focusStart(); break;
      case "focus-alt": focusAlt(); break;
      case "mirror": toggleMirror(el); break;
      case "awake": toggleAwake(el); break;
      case "pick": pickColor(el); break;
      case "replay": playDrop("drop"); break;
    }
  }

  const CLIPS = [
    { ic: "i-link", text: "https://github.com/werquess/FreeNotch", when: "Just now" },
    { sw: "#ff4d8d", text: "#FF4D8D", when: "2 min" },
    { ic: "i-note", text: "Soundcheck moved to 7:30, booth 3", when: "14 min" },
    { ic: "i-image", text: "Screenshot of the setlist", when: "1 h", image: true }
  ];
  $("[data-clips]").innerHTML = CLIPS.map((c, k) => `<li><button type="button" data-clip="${k}">
    <span class="clip-ic${c.sw ? " clip-swatch" : ""}"${c.sw ? ` style="--sw:${c.sw}"` : ""}>${c.ic ? `<svg class="ic"><use href="#${c.ic}"/></svg>` : ""}</span>
    <span class="clip-text">${c.text}</span><span class="clip-when">${c.when}</span></button></li>`).join("");
  $("[data-clips]").addEventListener("click", async e => {
    const b = e.target.closest("[data-clip]");
    if (!b) return;
    e.stopPropagation();
    const c = CLIPS[+b.dataset.clip];
    let ok = true;
    if (c.image) { toast("Images go back to your clipboard in the app"); return; }
    try { await navigator.clipboard.writeText(c.text); } catch (err) { ok = false; }
    const when = $(".clip-when", b);
    b.classList.add("is-copied");
    when.textContent = ok ? "Copied" : c.when;
    toast(ok ? "Copied to your clipboard" : "Your browser blocked the clipboard");
    setTimeout(() => { b.classList.remove("is-copied"); when.textContent = c.when; }, 1600);
  });

  const SAMPLES = [
    { name: "Mixdown.wav", kind: "file" },
    { name: "Poster.png", kind: "image" },
    { name: "Contract.pdf", kind: "pdf" }
  ];
  function iconClass(kind) { return kind === "image" ? "img" : kind === "folder" ? "folder" : "pdf"; }
  function addToShelf(name, kind) {
    const li = document.createElement("li");
    li.innerHTML = `<span class="desk-file-icon ${iconClass(kind)}"></span><span>${name}</span>`;
    els.shelf.appendChild(li);
    island.classList.add("has-shelf");
    els.shelf.scrollLeft = els.shelf.scrollWidth;
  }
  function addSample() {
    const desk = $$(".desk-file:not(.is-gone)");
    if (desk.length && getComputedStyle($(".desk-files")).display !== "none") {
      const d = desk[0];
      d.classList.add("is-gone");
      addToShelf(d.dataset.file, d.dataset.kind);
      return;
    }
    const s = SAMPLES[els.shelf.children.length % SAMPLES.length];
    addToShelf(s.name, s.kind);
  }

  $$(".desk-file").forEach(d => {
    d.addEventListener("pointerdown", e => {
      if (e.button !== 0) return;
      e.preventDefault();
      const sx = e.clientX, sy = e.clientY;
      let ghost = null, over = false;
      const move = ev => {
        if (!ghost && Math.hypot(ev.clientX - sx, ev.clientY - sy) < 4) return;
        if (!ghost) {
          ghost = d.cloneNode(true);
          ghost.classList.add("drag-ghost");
          document.body.appendChild(ghost);
          if (!state.expanded || state.feature !== "shelf") open("shelf");
          state.interacted = true;
        }
        ghost.style.left = ev.clientX + "px";
        ghost.style.top = ev.clientY + "px";
        const r = island.getBoundingClientRect();
        over = ev.clientX > r.left && ev.clientX < r.right && ev.clientY > r.top && ev.clientY < r.bottom + 10;
        els.dropzone.classList.toggle("is-hot", over);
      };
      const up = () => {
        window.removeEventListener("pointermove", move);
        window.removeEventListener("pointerup", up);
        els.dropzone.classList.remove("is-hot");
        if (!ghost) return;
        if (over) {
          ghost.remove();
          d.classList.add("is-gone");
          addToShelf(d.dataset.file, d.dataset.kind);
        } else {
          const r = d.getBoundingClientRect();
          ghost.animate([{ left: ghost.style.left, top: ghost.style.top }, { left: r.left + r.width / 2 + "px", top: r.top + r.height / 2 + "px" }], { duration: 320, easing: "cubic-bezier(.16,1,.3,1)" }).onfinish = () => ghost.remove();
        }
      };
      window.addEventListener("pointermove", move);
      window.addEventListener("pointerup", up);
    });
  });

  function toggleLock(btn) {
    const notes = $(".notes", island);
    const locked = notes.classList.toggle("is-locked");
    $("span", btn).textContent = locked ? "Unlock" : "Lock note";
    $("use", btn).setAttribute("href", locked ? "#i-unlock" : "#i-lock");
    $("textarea", notes).disabled = locked;
  }

  function focusRender() {
    if (state.focusMode === "pomo") {
      const left = state.pomoRunning ? (state.pomoEnd - Date.now()) / 1000 : state.pomoLeft;
      els.focusDigits.textContent = fmt2(left);
      els.pomoCompact.textContent = fmt2(left);
      $('[data-act="focus-start"]').textContent = state.pomoRunning ? "Pause" : left < 1500 ? "Resume" : "Start";
      $('[data-act="focus-alt"]').textContent = "Reset";
      els.laps.innerHTML = "";
    } else {
      const t = state.watchAcc + (state.watchRunning ? performance.now() - state.watchStart : 0);
      const s = t / 1000;
      els.focusDigits.textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(Math.floor(s % 60)).padStart(2, "0")}.${String(Math.floor((t % 1000) / 100))}`;
      $('[data-act="focus-start"]').textContent = state.watchRunning ? "Stop" : t > 0 ? "Resume" : "Start";
      $('[data-act="focus-alt"]').textContent = state.watchRunning ? "Lap" : "Reset";
      els.laps.innerHTML = state.laps.slice(-4).map((l, k, arr) => `<li>Lap ${state.laps.length - arr.length + k + 1}  ${l}</li>`).join("");
    }
    if (state.pomoRunning) {
      const left = (state.pomoEnd - Date.now()) / 1000;
      els.pomoCompact.textContent = fmt2(left);
      if (left <= 0) { state.pomoRunning = false; state.pomoLeft = 1500; toast("Focus session done. Take a break."); render(); }
    }
  }
  function focusStart() {
    if (state.focusMode === "pomo") {
      if (state.pomoRunning) { state.pomoLeft = (state.pomoEnd - Date.now()) / 1000; state.pomoRunning = false; }
      else { state.pomoEnd = Date.now() + state.pomoLeft * 1000; state.pomoRunning = true; toast("Close the island to see the countdown in it"); }
    } else {
      if (state.watchRunning) { state.watchAcc += performance.now() - state.watchStart; state.watchRunning = false; }
      else { state.watchStart = performance.now(); state.watchRunning = true; }
    }
    focusRender();
  }
  function focusAlt() {
    if (state.focusMode === "pomo") { state.pomoRunning = false; state.pomoLeft = 1500; render(); }
    else if (state.watchRunning) { const t = state.watchAcc + performance.now() - state.watchStart; state.laps.push(fmt(t / 1000) + "." + Math.floor((t % 1000) / 100)); }
    else { state.watchAcc = 0; state.laps = []; }
    focusRender();
  }
  $$("[data-focus]").forEach(b => b.addEventListener("click", e => {
    e.stopPropagation();
    state.focusMode = b.dataset.focus;
    $$("[data-focus]").forEach(x => x.setAttribute("aria-selected", String(x === b)));
    focusRender();
  }));

  let statsTimer = null;
  const stats = { cpu: 7, mem: 54, temp: 43, fan: 0 };
  function paintStats() {
    stats.cpu = clamp(stats.cpu + (Math.random() - 0.5) * 6, 3, 22);
    stats.mem = clamp(stats.mem + (Math.random() - 0.5) * 1.6, 50, 61);
    stats.temp = clamp(stats.temp + (Math.random() - 0.5) * 1.4, 40, 49);
    stats.fan = stats.temp > 46 ? 1450 + Math.round(Math.random() * 300) : 0;
    $('[data-stat="cpu"]').textContent = Math.round(stats.cpu) + "%";
    $('[data-stat="mem"]').textContent = Math.round(stats.mem) + "%";
    $('[data-stat="temp"]').textContent = Math.round(stats.temp) + "°C";
    $('[data-stat="fan"]').textContent = stats.fan ? stats.fan.toLocaleString("en-US") + " rpm" : "Off";
    $('[data-meter="cpu"]').style.width = stats.cpu + "%";
    $('[data-meter="mem"]').style.width = stats.mem + "%";
    $('[data-meter="temp"]').style.width = ((stats.temp - 20) / 80) * 100 + "%";
    $('[data-meter="fan"]').style.width = (stats.fan / 6000) * 100 + "%";
  }
  function startStats() { paintStats(); if (!statsTimer) statsTimer = setInterval(paintStats, 1200); }
  function stopStats() { clearInterval(statsTimer); statsTimer = null; }

  let mirrorStream = null;
  async function toggleMirror(btn) {
    const v = $("[data-mirror]");
    const label = $("[data-mirror-state]");
    if (mirrorStream) { stopMirror(); return; }
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { label.textContent = "No camera here"; return; }
    label.textContent = "Asking…";
    try {
      mirrorStream = await navigator.mediaDevices.getUserMedia({ video: { width: 320, height: 320, facingMode: "user" }, audio: false });
      v.srcObject = mirrorStream;
      await v.play();
      btn.classList.add("is-on");
      label.textContent = "Tap to stop";
    } catch (err) {
      mirrorStream = null;
      label.textContent = "Camera not allowed";
    }
  }
  function stopMirror() {
    if (!mirrorStream) return;
    mirrorStream.getTracks().forEach(t => t.stop());
    mirrorStream = null;
    const btn = $('[data-act="mirror"]');
    btn.classList.remove("is-on");
    $("[data-mirror-state]").textContent = "Uses your camera";
  }

  let wakeLock = null;
  async function toggleAwake(btn) {
    const label = $("[data-awake-state]");
    if (wakeLock) { await wakeLock.release().catch(() => {}); wakeLock = null; btn.classList.remove("is-on"); label.textContent = "Off"; return; }
    if (!("wakeLock" in navigator)) { label.textContent = "Not in this browser"; return; }
    try {
      wakeLock = await navigator.wakeLock.request("screen");
      btn.classList.add("is-on");
      label.textContent = "On for this tab";
      wakeLock.addEventListener("release", () => { wakeLock = null; btn.classList.remove("is-on"); label.textContent = "Off"; });
    } catch (err) { label.textContent = "Not allowed"; }
  }

  async function pickColor() {
    const label = $("[data-pick-state]");
    const face = $("[data-swatch]");
    const apply = hex => {
      hex = hex.toUpperCase();
      face.style.setProperty("--picked", hex);
      face.classList.add("has-color");
      label.textContent = hex;
      if (navigator.clipboard) navigator.clipboard.writeText(hex).then(() => toast(`${hex} copied`), () => {});
    };
    if ("EyeDropper" in window) {
      try { const r = await new window.EyeDropper().open(); apply(r.sRGBHex); } catch (err) {}
      return;
    }
    const input = $("[data-color-fallback]");
    input.oninput = () => apply(input.value);
    input.click();
  }

  $$(".theme-opt").forEach(b => b.addEventListener("click", e => {
    e.stopPropagation();
    state.theme = b.dataset.theme;
    body.classList.toggle("theme-oled", state.theme === "oled");
    $$(".theme-opt").forEach(x => x.setAttribute("aria-pressed", String(x === b)));
  }));
  $('.theme-opt[data-theme="smoke"]').setAttribute("aria-pressed", "true");
  $('.theme-opt[data-theme="oled"]').setAttribute("aria-pressed", "false");

  els.sleeves.addEventListener("click", e => {
    const s = e.target.closest(".sleeve");
    if (s) pull(s.dataset.id);
  });
  els.sleeves.addEventListener("keydown", e => {
    if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
    const all = $$(".sleeve", els.sleeves);
    const i = all.indexOf(document.activeElement);
    if (i < 0) return;
    e.preventDefault();
    const n = all[clamp(i + (e.key === "ArrowRight" ? 1 : -1), 0, all.length - 1)];
    n.focus();
    n.scrollIntoView({ block: "nearest", inline: "center", behavior: reduced ? "auto" : "smooth" });
  });
  $$(".os-switch [data-os]").forEach(b => b.addEventListener("click", () => {
    state.interacted = true;
    setOS(b.dataset.os);
    try { history.replaceState(null, "", b.dataset.os === "win" ? "#windows" : location.pathname); } catch (e) {}
  }));

  function tickClock() {
    const d = new Date();
    const hm = d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });
    const day = d.toLocaleDateString("en-US", { weekday: "short" });
    $$("[data-clock]").forEach((c, k) => c.textContent = k === 0 ? `${day} ${hm}` : hm);
  }
  tickClock();
  setInterval(tickClock, 20000);

  let last = performance.now();
  function loop(now) {
    const dt = Math.min(0.5, (now - last) / 1000);
    last = now;
    if (state.playing && state.started && !scrubbing) {
      state.pos += dt;
      if (state.pos >= TRACKS[state.track].len) setTrack(state.track + 1);
      updateMedia();
    }
    if (state.pomoRunning || state.watchRunning || (state.expanded && state.feature === "focus")) focusRender();
    requestAnimationFrame(loop);
  }

  window.addEventListener("resize", () => { if (!state.dropping) render(); });

  const dl = $("#download");
  const dlLink = $("[data-dl-link]");
  const release = {
    mac: { version: "1.0.7", url: `https://github.com/${REPO}/releases/latest/download/FreeNotch.dmg`, size: 0, file: "FreeNotch.dmg" },
    win: {
      version: "0.1.1",
      x64: { url: `https://github.com/${REPO}/releases/download/windows-v0.1.1/FreeNotch-Windows-x64.zip`, size: 0, file: "FreeNotch-Windows-x64.zip" },
      arm64: { url: `https://github.com/${REPO}/releases/download/windows-v0.1.1/FreeNotch-Windows-arm64.zip`, size: 0, file: "FreeNotch-Windows-arm64.zip" }
    }
  };
  const mb = b => (b ? Math.round(b / 1048576) + " MB" : "");

  function dlRender() {
    const os = $('input[name="os"]:checked', dl)?.value || "mac";
    const arch = $('input[name="arch"]:checked', dl)?.value || "x64";
    $("[data-dl-arch]").hidden = os !== "win";
    const pick = os === "mac" ? release.mac : release.win[arch];
    dlLink.href = pick.url;
    $("[data-dl-file]").textContent = `Download ${pick.file}`;
    $("[data-dl-size]").textContent = mb(pick.size);
    $("[data-dl-note]").textContent = os === "mac"
      ? "Needs macOS 14 or later on Apple Silicon. Open the disk image and drag FreeNotch into Applications. It isn't notarized yet, so the first time, right-click it and choose Open."
      : "Windows 11 preview. Unzip it anywhere and run FreeNotch.exe. It isn't signed yet, so if SmartScreen asks, choose More info, then Run anyway.";
    $("[data-dl-mac-ver]").textContent = `Version ${release.mac.version}`;
    $("[data-dl-win-ver]").textContent = `Preview ${release.win.version}`;
  }

  async function detectArch() {
    try {
      if (navigator.userAgentData && navigator.userAgentData.getHighEntropyValues) {
        const v = await navigator.userAgentData.getHighEntropyValues(["architecture"]);
        if (v.architecture === "arm") return "arm64";
      }
    } catch (e) {}
    return "x64";
  }
  function detectOS() {
    const p = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || navigator.userAgent;
    if (/win/i.test(p)) return "win";
    if (/mac/i.test(p)) return "mac";
    return state.os;
  }

  $$("[data-download]").forEach(b => b.addEventListener("click", async () => {
    const os = detectOS();
    $(`input[name="os"][value="${os}"]`, dl).checked = true;
    if (os === "win") { const a = await detectArch(); $(`input[name="arch"][value="${a}"]`, dl).checked = true; }
    dlRender();
    dl.showModal();
  }));
  dl.addEventListener("change", dlRender);
  dl.addEventListener("click", e => { if (e.target === dl) dl.close(); });
  dlLink.addEventListener("click", () => setTimeout(() => dl.close(), 400));

  fetch(`https://api.github.com/repos/${REPO}/releases?per_page=30`, { headers: { Accept: "application/vnd.github+json" } })
    .then(r => (r.ok ? r.json() : []))
    .then(list => {
      if (!Array.isArray(list)) return;
      const mac = list.find(r => !r.draft && !r.prerelease && /^v\d/.test(r.tag_name));
      if (mac) {
        release.mac.version = mac.tag_name.replace(/^v/, "");
        const a = mac.assets.find(x => x.name === "FreeNotch.dmg");
        if (a) { release.mac.url = a.browser_download_url; release.mac.size = a.size; }
        $("[data-ver-mac]").textContent = `macOS ${release.mac.version}`;
      }
      const win = list.find(r => !r.draft && /^windows-v/.test(r.tag_name));
      if (win) {
        release.win.version = win.tag_name.replace(/^windows-v/, "");
        ["x64", "arm64"].forEach(k => {
          const a = win.assets.find(x => x.name === `FreeNotch-Windows-${k}.zip`);
          if (a) { release.win[k].url = a.browser_download_url; release.win[k].size = a.size; }
        });
      }
      if (dl.open) dlRender();
    })
    .catch(() => {});

  buildSleeves();
  setTrack(0, true);
  setVolume(state.vol, false);
  focusRender();
  render();
  requestAnimationFrame(loop);

  const hash = location.hash.replace("#", "");
  const startOS = hash === "windows" || ["drop", "themes"].includes(hash) ? "win" : "mac";
  const deep = SLEEVES.find(s => s.id === hash);

  setTimeout(() => {
    state.started = true;
    if (startOS === "win") {
      setOS("win", { force: true, then: deep && deep.os.includes("win") ? () => pull(hash) : null });
      return;
    }
    render();
    if (deep) { setTimeout(() => pull(hash), 500); return; }
    if (reduced) return;
    setTimeout(() => {
      if (state.interacted) return;
      open("media");
      setTimeout(() => { if (!state.interacted) collapse(); }, 3400);
    }, 900);
  }, 650);
})();
