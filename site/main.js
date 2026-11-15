// Framecalm site motion. Works without the libraries too: GSAP, ScrollTrigger and Lenis only add
// smooth scrolling, pinned sections and scrubbed reveals on top of a page that already reads fine.
(() => {
  const reduce = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const fine = matchMedia("(pointer: fine)").matches;
  const gsap = window.gsap, ST = window.ScrollTrigger;

  // ---------- intro: count to 100 while the line settles, once per visit ----------
  const loader = document.querySelector(".loader");
  const count = loader?.querySelector("[data-count]");
  const startPage = () => document.documentElement.classList.add("ready");
  let seen = false;
  try { seen = sessionStorage.getItem("fc-intro") === "1" || location.search.includes("nointro"); sessionStorage.setItem("fc-intro", "1"); } catch { }
  if (loader && !reduce && !seen) {
    const t0 = performance.now(), dur = 1500;
    const tick = (t) => {
      const p = Math.min(1, (t - t0) / dur);
      count.textContent = Math.round((1 - Math.pow(1 - p, 3)) * 100);
      if (p < 1) requestAnimationFrame(tick);
      else { loader.classList.add("done"); setTimeout(() => { loader.remove(); reveal(document.querySelector(".hero")); }, 450); startPage(); }
    };
    requestAnimationFrame(tick);
  } else {
    loader?.remove();
    startPage();
  }

  // ---------- reveals: lines rise and paragraphs fade in as they enter ----------
  function reveal(el) { el?.querySelectorAll(".mega, .big, .reveal").forEach((n) => n.classList.add("in")); }
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
  }, { rootMargin: "0px 0px -12% 0px" });
  document.querySelectorAll(".big, .reveal").forEach((n) => { if (!n.closest(".hero")) io.observe(n); });
  if (!loader?.isConnected || reduce || seen) reveal(document.querySelector(".hero"));

  // ---------- one-click demo: the plan's boxes tick one by one ----------
  const plan = document.querySelector("[data-plan]");
  const go = plan?.querySelector("[data-plan-go]");
  const note = plan?.querySelector("[data-plan-note]");
  go?.addEventListener("click", () => {
    if (plan.classList.contains("in")) {
      plan.classList.remove("in");
      go.textContent = "Optimize";
      note.textContent = "37 changes. Restore point first.";
      return;
    }
    go.disabled = true;
    go.textContent = "Working";
    note.textContent = "Making a restore point…";
    requestAnimationFrame(() => plan.classList.add("in"));
    setTimeout(() => { note.textContent = "37 changes applied and checked. Nothing on your PC was touched."; go.textContent = "Reset"; go.disabled = false; }, reduce ? 0 : 1100);
  });

  // ---------- smooth scrolling ----------
  let lenis = null;
  if (window.Lenis && !reduce) {
    lenis = new Lenis({ lerp: 0.09, wheelMultiplier: 1 });
    const raf = (t) => { lenis.raf(t); requestAnimationFrame(raf); };
    requestAnimationFrame(raf);
    if (ST) lenis.on("scroll", ST.update);
    document.querySelectorAll('a[href^="#"]').forEach((a) => a.addEventListener("click", (e) => {
      const id = a.getAttribute("href");
      if (id.length > 1 && document.querySelector(id)) { e.preventDefault(); lenis.scrollTo(id, { offset: -72 }); }
    }));
  }

  // ---------- nav: solid after the top, hides while scrolling down ----------
  const top = document.querySelector(".top");
  let lastY = scrollY;
  const onScroll = () => {
    const y = scrollY;
    top?.classList.toggle("solid", y > 40);
    top?.classList.toggle("hide", y > 400 && y > lastY + 2);
    if (y < lastY - 2) top?.classList.remove("hide");
    lastY = y;
  };
  addEventListener("scroll", onScroll, { passive: true });
  onScroll();

  // ---------- pinned sections ----------
  if (gsap && ST && !reduce) {
    gsap.registerPlugin(ST);

    // the app window grows from a card to the full screen
    const win = document.querySelector(".zoom-win");
    if (win) gsap.fromTo(win, { "--zs": 0.55 }, { "--zs": 1, ease: "none", scrollTrigger: { trigger: "[data-zoom]", start: "top top", end: "60% top", scrub: true } });
    gsap.fromTo(".zoom-kicker", { opacity: 1 }, { opacity: 0, ease: "none", scrollTrigger: { trigger: "[data-zoom]", start: "top top", end: "25% top", scrub: true } });

    // the nine categories slide sideways while the section is pinned (desktop sizes only)
    const mm = gsap.matchMedia();
    mm.add("(min-width: 900px)", () => {
      const sec = document.querySelector("[data-hscroll]");
      const track = sec?.querySelector("[data-track]");
      if (!track) return;
      sec.classList.add("js-pinned");
      const dist = () => track.scrollWidth - innerWidth + 80;
      const tw = gsap.to(track, { x: () => -dist(), ease: "none", scrollTrigger: { trigger: sec, start: "top top", end: () => "+=" + dist(), pin: ".cats-stick", scrub: 0.6, invalidateOnRefresh: true } });
      return () => { tw.scrollTrigger?.kill(); tw.kill(); sec.classList.remove("js-pinned"); gsap.set(track, { x: 0 }); };
    });

    // stacking cards: each one shrinks slightly as the next slides over it
    gsap.utils.toArray(".card").forEach((card, i, all) => {
      if (i === all.length - 1) return;
      gsap.to(card, { scale: 0.94, opacity: 0.5, ease: "none", scrollTrigger: { trigger: all[i + 1], start: "top bottom", end: "top 20%", scrub: true } });
    });

    // the wordmark rises at the very end
    gsap.fromTo(".wordmark", { yPercent: 40 }, { yPercent: 0, ease: "none", scrollTrigger: { trigger: ".end", start: "top bottom", end: "bottom bottom", scrub: true } });
  }

  // ---------- parts drift with the pointer ----------
  if (!reduce) {
    const parts = [...document.querySelectorAll("[data-depth]")];
    let mx = 0, my = 0, raf = 0;
    const paint = () => { raf = 0; for (const el of parts) { const d = +el.dataset.depth; el.style.setProperty("--px", `${(mx * 30 * d).toFixed(1)}px`); el.style.setProperty("--py", `${(my * 22 * d).toFixed(1)}px`); } };
    addEventListener("pointermove", (e) => { mx = e.clientX / innerWidth - 0.5; my = e.clientY / innerHeight - 0.5; if (!raf) raf = requestAnimationFrame(paint); }, { passive: true });
  }

  // ---------- magnetic buttons (mouse only) ----------
  if (fine && !reduce) {
    for (const m of document.querySelectorAll(".magnetic")) {
      m.addEventListener("pointermove", (e) => {
        const r = m.getBoundingClientRect();
        m.style.transform = `translate(${(e.clientX - r.left - r.width / 2) * 0.2}px, ${(e.clientY - r.top - r.height / 2) * 0.3}px)`;
      });
      m.addEventListener("pointerleave", () => { m.style.transition = "transform .6s cubic-bezier(.16,1,.3,1)"; m.style.transform = ""; setTimeout(() => (m.style.transition = ""), 600); });
    }
  }

  // ---------- star dust: glowing particles at three depths, only drawn while on screen ----------
  for (const cv of document.querySelectorAll("[data-stars]")) {
    const ctx = cv.getContext("2d");
    const colors = ["241,240,255", "143,139,255", "255,178,36", "124,245,192"];
    let w = 0, h = 0, dpr = 1, stars = [], visible = false, mx = 0, my = 0;
    const resize = () => {
      dpr = Math.min(devicePixelRatio || 1, 2);
      w = cv.clientWidth; h = cv.clientHeight;
      cv.width = w * dpr; cv.height = h * dpr;
      const n = Math.round(Math.min(220, (w * h) / 6000));
      stars = Array.from({ length: n }, () => ({
        x: Math.random() * w, y: Math.random() * h, z: Math.random() * 0.9 + 0.1,
        r: Math.random() * 1.6 + 0.4, c: colors[Math.random() < 0.8 ? (Math.random() < 0.6 ? 0 : 1) : (Math.random() < 0.5 ? 2 : 3)],
        p: Math.random() * Math.PI * 2, s: Math.random() * 0.02 + 0.005,
      }));
    };
    const draw = () => {
      if (!visible) return;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, w, h);
      for (const st of stars) {
        if (!reduce) { st.y -= st.z * 0.25; st.p += st.s; if (st.y < -10) { st.y = h + 10; st.x = Math.random() * w; } }
        const tw = 0.55 + Math.sin(st.p) * 0.45;
        const x = st.x + mx * 30 * st.z, y = st.y + my * 20 * st.z;
        const g = ctx.createRadialGradient(x, y, 0, x, y, st.r * 6);
        g.addColorStop(0, `rgba(${st.c},${0.9 * tw})`);
        g.addColorStop(0.3, `rgba(${st.c},${0.25 * tw})`);
        g.addColorStop(1, `rgba(${st.c},0)`);
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.arc(x, y, st.r * 6, 0, Math.PI * 2); ctx.fill();
      }
      if (!reduce) requestAnimationFrame(draw);
    };
    new IntersectionObserver(([e]) => { const was = visible; visible = e.isIntersecting; if (visible && !was) requestAnimationFrame(draw); }).observe(cv);
    addEventListener("resize", () => { resize(); if (reduce) draw(); }, { passive: true });
    addEventListener("pointermove", (e) => { mx = e.clientX / innerWidth - 0.5; my = e.clientY / innerHeight - 0.5; }, { passive: true });
    resize();
  }

  // ---------- cover strip pause ----------
  const stripBtn = document.querySelector("[data-strip-toggle]");
  stripBtn?.addEventListener("click", () => {
    const strips = document.querySelector(".marquees");
    const paused = strips.classList.toggle("paused");
    stripBtn.setAttribute("aria-pressed", String(paused));
    stripBtn.textContent = paused ? "Play covers" : "Pause covers";
  });

  // ---------- hero console ----------
  const con = document.querySelector("[data-console]");
  if (con) {
    const tabs = [...con.querySelectorAll('[role="tab"]')];
    const panels = [...con.querySelectorAll('[role="tabpanel"]')];
    const switches = [...con.querySelectorAll(".switch")];
    const onEl = con.querySelector("[data-on]");
    const totalEl = con.querySelector("[data-total]");
    const foot = con.querySelector("[data-foot]");
    const stagedEl = con.querySelector("[data-staged]");
    const noteEl = con.querySelector("[data-note]");
    const apply = con.querySelector("[data-apply]");
    const log = con.querySelector("[data-log]");
    const on = new Set();
    totalEl.textContent = switches.length;

    // a few start out on, like a real PC would
    for (const id of ["hags-on", "delivery-opt-off"]) {
      const s = con.querySelector(`.switch[data-id="${id}"]`);
      if (s) { on.add(id); s.setAttribute("aria-checked", "true"); s.closest(".trow").classList.add("on"); }
    }

    const select = (i, focus) => {
      tabs.forEach((t, j) => { t.setAttribute("aria-selected", String(i === j)); t.tabIndex = i === j ? 0 : -1; });
      panels.forEach((p, j) => { p.hidden = i !== j; });
      if (focus) tabs[i].focus();
    };
    tabs.forEach((t, i) => {
      t.addEventListener("click", () => select(i));
      t.addEventListener("keydown", (e) => {
        if (e.key === "ArrowRight") select((i + 1) % tabs.length, true);
        if (e.key === "ArrowLeft") select((i - 1 + tabs.length) % tabs.length, true);
      });
    });

    const staged = () => switches.filter((s) => (s.getAttribute("aria-checked") === "true") !== on.has(s.dataset.id));

    const render = () => {
      const st = staged();
      onEl.textContent = on.size;
      switches.forEach((s) => {
        const row = s.closest(".trow");
        const checked = s.getAttribute("aria-checked") === "true";
        row.classList.toggle("staged", checked !== on.has(s.dataset.id));
        row.classList.toggle("on", checked && on.has(s.dataset.id));
      });
      if (st.length === 0) {
        stagedEl.textContent = "Nothing staged";
        noteEl.textContent = "Flip a switch to see exactly what it would change.";
        apply.textContent = "Apply";
        apply.disabled = true;
        foot.classList.remove("live");
        return;
      }
      const turnOn = st.filter((s) => s.getAttribute("aria-checked") === "true").length;
      const turnOff = st.length - turnOn;
      stagedEl.textContent = [turnOn && `${turnOn} to turn on`, turnOff && `${turnOff} to turn off`].filter(Boolean).join(", ");
      const notes = [];
      if (st.some((s) => s.dataset.admin === "true")) notes.push("One admin prompt, restore point first.");
      if (st.some((s) => s.dataset.tier === "extreme" && s.getAttribute("aria-checked") === "true")) notes.push("Includes an Extreme change.");
      if (st.some((s) => s.dataset.restart === "true")) notes.push("Some need a restart.");
      noteEl.textContent = notes.join(" ") || "Takes effect right away.";
      apply.textContent = st.length === 1 ? "Apply 1 change" : `Apply ${st.length} changes`;
      apply.disabled = false;
      foot.classList.add("live");
    };

    switches.forEach((s) => s.addEventListener("click", () => {
      s.setAttribute("aria-checked", String(s.getAttribute("aria-checked") !== "true"));
      log.classList.remove("open");
      render();
    }));

    apply.addEventListener("click", async () => {
      const st = staged();
      if (!st.length) return;
      const values = st.reduce((n, s) => n + Number(s.dataset.values || 1), 0);
      const admin = st.some((s) => s.dataset.admin === "true");
      const lines = [
        ...(admin ? ["Restore point created"] : []),
        `Saved ${values} old value${values === 1 ? "" : "s"} to the journal`,
        `Wrote ${values} value${values === 1 ? "" : "s"}, read back, all match`,
        `Done. Switch any of them back from History`,
      ];
      apply.disabled = true;
      log.innerHTML = "";
      log.classList.add("open");
      for (const text of lines) {
        const li = document.createElement("li");
        li.textContent = text;
        log.append(li);
        if (!reduce) await new Promise((r) => setTimeout(r, 380));
      }
      st.forEach((s) => { if (s.getAttribute("aria-checked") === "true") on.add(s.dataset.id); else on.delete(s.dataset.id); });
      render();
      stagedEl.textContent = "Applied in this demo only";
      noteEl.textContent = "Nothing on your PC was changed.";
    });

    render();
  }

  // ---------- tweak list filter ----------
  const segs = [...document.querySelectorAll(".seg [data-tier]")];
  const search = document.querySelector("[data-search]");
  const items = [...document.querySelectorAll(".tw")];
  const cats = [...document.querySelectorAll(".cat")];
  const empty = document.querySelector("[data-empty]");
  let tier = "all";
  const filter = () => {
    const q = (search?.value || "").trim().toLowerCase();
    let shown = 0;
    items.forEach((it) => {
      const ok = (tier === "all" || it.dataset.tier === tier) && (!q || it.dataset.text.includes(q));
      it.hidden = !ok;
      if (ok) shown++;
    });
    cats.forEach((c) => { c.hidden = !c.querySelector(".tw:not([hidden])"); });
    if (empty) empty.hidden = shown > 0;
  };
  segs.forEach((b) => b.addEventListener("click", () => {
    tier = b.dataset.tier;
    segs.forEach((x) => x.setAttribute("aria-pressed", String(x === b)));
    filter();
  }));
  search?.addEventListener("input", filter);
})();
