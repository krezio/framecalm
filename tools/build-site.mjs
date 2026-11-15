// Generates the guide pages, guides index, privacy page, 404 page and sitemap for site/.
// Usage: node tools/build-site.mjs
import { mkdirSync, writeFileSync, readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { guides } from "./guides.mjs";

const SITE = join(dirname(fileURLToPath(import.meta.url)), "..", "site");
const BASE = "https://framecalm.krezio.xyz";
const UPDATED = "2026-11-13";
const DOWNLOAD = "https://github.com/krezio/framecalm-releases/releases/latest/download/Framecalm-Setup.exe";
const PORTABLE = "https://github.com/krezio/framecalm-releases/releases/latest/download/Framecalm-portable.zip";
const CF_TOKEN = "22094a4c9ca14cf6838a007582902d9c";
const TOOLS = dirname(fileURLToPath(import.meta.url));
const CATALOG = JSON.parse(readFileSync(join(TOOLS, "..", "src", "Framecalm.Windows", "Data", "tweaks.json"), "utf8"));

const esc = (s) => s.replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");

function head({ title, description, url, root, jsonld, type = "website", robots = "index, follow, max-image-preview:large" }) {
  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${esc(title)}</title>
  <meta name="description" content="${esc(description)}">
  <link rel="canonical" href="${url}">
  <meta name="robots" content="${robots}">
  <meta name="theme-color" content="#100E33">
  <link rel="icon" href="${root}favicon.ico" sizes="any">
  <link rel="icon" href="${root}assets/logo.svg" type="image/svg+xml">
  <link rel="apple-touch-icon" href="${root}assets/apple-touch-icon.png">
  <meta property="og:type" content="${type}">
  <meta property="og:site_name" content="Framecalm">
  <meta property="og:title" content="${esc(title)}">
  <meta property="og:description" content="${esc(description)}">
  <meta property="og:url" content="${url}">
  <meta property="og:image" content="${BASE}/assets/og.png">
  <meta name="twitter:card" content="summary_large_image">
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Onest:wght@300..800&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="${root}styles.css?v=${createHash("sha256").update(readFileSync(join(SITE, "styles.css"))).digest("hex").slice(0, 10)}">
${jsonld ? `  <script type="application/ld+json">${JSON.stringify(jsonld)}</script>\n` : ""}</head>
<body>
  <a class="skip" href="#main">Skip to content</a>
  <header class="top">
    <nav class="wrap nav" aria-label="Main">
      <a class="brand" href="${root}"><img src="${root}assets/logo.svg" alt="" width="28" height="28"><span>framecalm</span></a>
      <div class="nav-links">
        <a href="${root}#auto">One click</a>
        <a href="${root}tweaks/">Tweaks</a>
        <a href="${root}#app">The app</a>
        <a href="${root}#safety">Safety</a>
        <a href="${root}guides/">Guides</a>
      </div>
      <a class="btn btn-ghost btn-sm" href="${DOWNLOAD}">Download</a>
    </nav>
  </header>
`;
}

function foot(root) {
  return `
  <footer class="footer">
    <div class="wrap footer-in">
      <span class="brand brand-sm"><img src="${root}assets/logo.svg" alt="" width="20" height="20"><span>framecalm</span></span>
      <nav aria-label="Footer"><a href="${root}guides/">Guides</a><a href="${root}privacy.html">Privacy</a><a href="https://github.com/krezio/framecalm-releases/issues">Report a problem</a><a href="https://krezio.xyz">Made by krezio</a></nav>
    </div>
  </footer>
  <script defer src="https://static.cloudflareinsights.com/beacon.min.js" data-cf-beacon='{"token": "${CF_TOKEN}"}'></script>
</body>
</html>
`;
}

const cta = (root) => `
    <aside class="cta-box">
      <img src="${root}assets/logo.svg" alt="" width="48" height="48">
      <div>
        <h2>Or let Framecalm do it</h2>
        <p>One click builds a plan for your PC, or pick from ${CATALOG.tweaks.length} Windows tweaks yourself. PC fixes, per-game settings and measured benchmarks too. Every change can be switched back. Free for Windows 10 and 11.</p>
      </div>
      <a class="btn btn-primary" href="${DOWNLOAD}">Download free</a>
    </aside>`;

function write(rel, html) {
  const file = join(SITE, rel);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, html);
}

// ---------- guide pages ----------
for (const g of guides) {
  const url = `${BASE}/guides/${g.slug}/`;
  const root = "../../";
  const others = guides.filter((o) => o.slug !== g.slug).slice(0, 4);
  const jsonld = {
    "@context": "https://schema.org",
    "@graph": [
      {
        "@type": "Article",
        headline: g.title,
        description: g.description,
        url,
        mainEntityOfPage: url,
        datePublished: UPDATED,
        dateModified: UPDATED,
        image: `${BASE}/assets/og.png`,
        author: { "@type": "Organization", name: "krezio", url: "https://krezio.xyz" },
        publisher: { "@type": "Organization", name: "krezio", logo: { "@type": "ImageObject", url: `${BASE}/assets/icon-512.png` } },
      },
      {
        "@type": "BreadcrumbList",
        itemListElement: [
          { "@type": "ListItem", position: 1, name: "Framecalm", item: `${BASE}/` },
          { "@type": "ListItem", position: 2, name: "Guides", item: `${BASE}/guides/` },
          { "@type": "ListItem", position: 3, name: g.nav, item: url },
        ],
      },
    ],
  };
  write(`guides/${g.slug}/index.html`,
    head({ title: g.title, description: g.description, url, root, jsonld, type: "article" }) + `
  <main class="article" id="main">
    <p class="label"><a href="${root}" style="color:inherit">Framecalm</a> › <a href="../" style="color:inherit">Guides</a></p>
    <h1>${g.title}</h1>
    <p class="label">Updated November 2026 · ${g.minutes} min read</p>
    <div class="tldr"><p class="tldr-h">Short answer</p><p>${g.tldr}</p></div>
${g.body}
${cta(root)}
    <h2>More guides</h2>
    <ul class="guide-list">
${others.map((o) => `      <li><a href="../${o.slug}/">${o.nav}</a></li>`).join("\n")}
    </ul>
  </main>
` + foot(root));
}

// ---------- guides index ----------
write("guides/index.html",
  head({
    title: "PC gaming performance guides | Framecalm",
    description: "Plain-English guides to more FPS on Windows: best settings for Minecraft, Fortnite and Rocket League, the wrong-GPU problem on laptops, CPU vs GPU bottlenecks, and how to measure FPS properly.",
    url: `${BASE}/guides/`,
    root: "../",
    jsonld: {
      "@context": "https://schema.org",
      "@type": "CollectionPage",
      name: "Framecalm guides",
      url: `${BASE}/guides/`,
      hasPart: guides.map((g) => ({ "@type": "Article", headline: g.title, url: `${BASE}/guides/${g.slug}/` })),
    },
  }) + `
  <main class="article">
    <p class="label"><a href="../" style="color:inherit">Framecalm</a> › Guides</p>
    <h1>More FPS, explained</h1>
    <p class="muted">What actually makes games run faster on Windows, what doesn't, and how to check the difference yourself.</p>
    <ul class="guide-list" style="margin-top:28px">
${guides.map((g) => `      <li><a href="${g.slug}/">${g.nav}</a><p>${g.description}</p></li>`).join("\n")}
    </ul>
${cta("../")}
  </main>
` + foot("../"));

// ---------- privacy ----------
write("privacy.html",
  head({ title: "Privacy | Framecalm", description: "Framecalm has no account, no ads and no telemetry. Here's exactly what the app and this website do with data.", url: `${BASE}/privacy.html`, root: "" }) + `
  <main class="article">
    <p class="label">Privacy</p>
    <h1>Privacy</h1>
    <p>Short version: the Framecalm app doesn't collect anything, and this website counts visits without cookies.</p>
    <h2>The app</h2>
    <ul>
      <li>No account, no sign-in, no ads, no telemetry, no crash reporting.</li>
      <li>Everything Framecalm reads (your hardware, your launchers' game lists, your games' settings files, frame-time captures) stays on your PC.</li>
      <li>Its own data (backups, history, captures, settings) lives in <code>%LOCALAPPDATA%\\Framecalm</code>. Delete that folder and it's gone.</li>
      <li>Tweaks you apply are Windows settings on your PC. Framecalm keeps their old values locally so it can put them back; nothing about them leaves your PC.</li>
      <li>It works offline. It makes two kinds of network request, both of which you can switch off in Settings: downloading each game's official cover image once from Steam's or the Microsoft Store's image servers (on by default; those servers see your IP address and which cover was asked for, like any website), and a check for a newer version on GitHub when the app opens (<b>you choose at first launch</b>, and can switch it off in Settings). Neither sends anything about you or your PC.</li>
      <li>Links to graphics driver pages or Windows Settings open in your own browser or Settings app.</li>
    </ul>
    <h2>This website</h2>
    <ul>
      <li>Hosted on Cloudflare Pages. Cloudflare may keep standard server logs (such as IP addresses) for security; see Cloudflare's privacy policy.</li>
      <li>Visits are counted with <b>Cloudflare Web Analytics</b>, which doesn't use cookies, doesn't fingerprint you and doesn't track you across sites. It reports things like page views, referrers and country.</li>
      <li>Fonts come from Google Fonts, the animation libraries from cdnjs and jsDelivr, and the game covers on the home page from Steam's and Microsoft's image servers, so your browser asks those servers for them.</li>
      <li>The tweak demo on the home page runs entirely in your browser. Nothing you click in it is sent anywhere, and nothing on your PC is changed.</li>
    </ul>
    <h2>Contact</h2>
    <p>Questions or concerns: <a href="https://github.com/krezio/framecalm-releases/issues">open an issue on GitHub</a>.</p>
    <p class="label">Last updated November 2026</p>
  </main>
` + foot(""));

// ---------- 404 ----------
write("404.html",
  head({ title: "Page not found | Framecalm", description: "This page doesn't exist.", url: `${BASE}/404.html`, root: "/", robots: "noindex" }) + `
  <main class="article" style="text-align:center">
    <p class="label accent">404</p>
    <h1>Dropped frame</h1>
    <p class="muted">This page doesn't exist.</p>
    <p><a class="btn btn-primary" href="/">Back to Framecalm</a> <a class="btn" href="/guides/">Read the guides</a></p>
  </main>
` + foot("/"));


// ---------- home page (from tools/index.template.html and the tweak catalog) ----------
const tiers = ["safe", "advanced", "extreme"];
const meter = (tier) => `<span class="meter t-${tier}" aria-hidden="true"><i></i><i></i><i></i></span>`;
const impactText = { high: "Big difference", medium: "Noticeable", low: "Small gain", situational: "Fixes specific problems" };
const svcStart = { disabled: "4", manual: "3", automatic: "2", delayed: "2" };
function keyLines(t) {
  return t.atoms.map((a) => {
    if (a.service) return { where: `HKLM\\SYSTEM\\CurrentControlSet\\Services\\${a.service}\\Start`, value: `${svcStart[a.start]} (${a.start})` };
    if (a.power) return { where: `powercfg ${a.alias}`, value: String(a.value) };
    const v = a.dword !== undefined ? (a.dword === 4294967295 ? "0xFFFFFFFF" : String(a.dword)) : `"${a.sz}"`;
    return { where: `${a.reg.replace("{nic}", "{each network adapter}")}\\${a.name}`, value: v };
  });
}
const needsAdmin = (t) => t.atoms.some((a) => a.power || a.service || (a.reg && (a.reg.startsWith("HKLM") || a.reg.includes("\\Policies\\"))));
const T = CATALOG.tweaks;
const count = T.length;

const consoleCats = [
  ["power", "Power", ["usb-suspend-off", "cpu-min-100", "core-parking-off", "power-throttling-off"]],
  ["graphics", "Graphics", ["hags-on", "mmcss-games", "game-bar-off", "fso-off"]],
  ["network", "Network", ["delivery-opt-off", "network-throttling-off", "nagle-off", "wu-drivers-off"]],
  ["system", "System", ["mouse-accel-off", "background-apps-off", "svc-sysmain", "vbs-off"]],
];
const tabs = consoleCats.map(([id, name], i) =>
  `          <button role="tab" id="tab-${id}" aria-controls="panel-${id}" aria-selected="${i === 0}" tabindex="${i === 0 ? 0 : -1}">${name}</button>`).join("\n");
const panels = consoleCats.map(([id, , ids], i) => {
  const rows = ids.map((tid) => {
    const t = T.find((x) => x.id === tid);
    if (!t) throw new Error("console tweak missing: " + tid);
    const k = keyLines(t)[0];
    const more = t.atoms.length > 1 ? ` <span class="plus">and ${t.atoms.length - 1} more</span>` : "";
    return `          <li class="trow" data-tier="${t.tier}">
            ${meter(t.tier)}
            <div class="trow-text"><span class="trow-name">${esc(t.name)}</span><span class="trow-key"><code>${esc(k.where)} = ${esc(k.value)}</code>${more}</span></div>
            <button class="switch" role="switch" aria-checked="false" aria-label="${esc(t.name)}" data-id="${t.id}" data-tier="${t.tier}" data-admin="${needsAdmin(t)}" data-restart="${!!t.restart}" data-values="${t.atoms.length}"></button>
          </li>`;
  }).join("\n");
  return `        <ul class="console-rows" role="tabpanel" id="panel-${id}" aria-labelledby="tab-${id}"${i === 0 ? "" : " hidden"}>
${rows}
        </ul>`;
}).join("\n");

const presets = CATALOG.presets.map((p, i) => {
  const mine = T.filter((t) => t.presets.includes(p.id));
  const prev = i === 0 ? [] : T.filter((x) => x.presets.includes(CATALOG.presets[i - 1].id));
  const shown = mine.filter((t) => !prev.includes(t)).slice(0, 5);
  const list = shown.map((t) => `<li>${esc(t.name)}</li>`).join("");
  return `        <article class="preset p-${tiers[i]}">
          ${meter(tiers[i])}
          <h3>${p.name}</h3>
          <p class="preset-n"><b>${mine.length}</b> tweaks</p>
          <p>${esc(p.about)}</p>
          <p class="preset-sub">${i === 0 ? "Includes" : "Adds, among others"}</p>
          <ul>${list}</ul>
        </article>`;
}).join("\n");

const catalogHtml = CATALOG.categories.map((c) => {
  const mine = T.filter((t) => t.category === c.id);
  const rows = mine.map((t) => {
    const keys = keyLines(t).map((k) => `<li><code>${esc(k.where)}</code><span>set to <code>${esc(k.value)}</code></span></li>`).join("");
    return `        <li class="tw" data-tier="${t.tier}" data-text="${esc((t.name + " " + t.what).toLowerCase())}">
          <details>
            <summary>${meter(t.tier)}<span class="tw-name">${esc(t.name)}</span><span class="tw-impact">${impactText[t.impact]}</span><span class="tw-tier">${t.tier[0].toUpperCase() + t.tier.slice(1)}</span></summary>
            <div class="tw-body">
              <p>${esc(t.what)}</p>
              <p class="tw-trade"><b>Trade-off.</b> ${esc(t.tradeoff)}</p>
              <ul class="tw-keys">${keys}</ul>
            </div>
          </details>
        </li>`;
  }).join("\n");
  return `      <div class="cat" data-cat="${c.id}">
        <div class="cat-head"><h3>${c.name}</h3><p>${esc(c.about)}</p></div>
        <ul class="tw-list">
${rows}
        </ul>
      </div>`;
}).join("\n");

const leftout = CATALOG.leftOut.map((l) => `        <div><dt>${esc(l.name)}</dt><dd>${esc(l.why)}</dd></div>`).join("\n");
const data = JSON.stringify(T.map((t) => ({ id: t.id, tier: t.tier, name: t.name, restart: !!t.restart, atoms: keyLines(t) }))).replace(/</g, "\\u003c");
const jsonld = JSON.stringify({
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Framecalm",
  operatingSystem: "Windows 10, Windows 11",
  applicationCategory: "UtilitiesApplication",
  offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
  description: `Free Windows optimizer: one-click plans for your hardware and ${count} tweaks from safe to extreme, each explained and undoable.`,
  url: `${BASE}/`,
  downloadUrl: DOWNLOAD,
  publisher: { "@type": "Organization", name: "krezio", url: "https://krezio.xyz" },
});
// game covers for the moving strip: official art, loaded from the publishers' store image servers
const MS = (id) => `https://store-images.s-microsoft.com/image/apps.${id}?w=264&h=396&q=85`;
const ST = (id) => `https://cdn.cloudflare.steamstatic.com/steam/apps/${id}/library_600x900.jpg`;
const covers = [
  ["Minecraft", MS("808.14492077886571533.be42f4bd-887b-4430-8ed0-622341b4d2b0.c8274c53-105e-478b-9f4b-41b8088210a3")],
  ["Fortnite", MS("52548.70702278257994163.f7265b8a-f10e-49c4-8115-73c4d800af63.a0e057ff-c216-4206-a6b9-65bfc7d54fb0")],
  ["Rocket League", ST(252950)],
  ["Roblox", MS("53518.13510798883386282.a1e5df65-8dd7-427c-9da9-506afa37b254.706886cc-3ee8-42ab-b3d1-2faff7915f6f")],
  ["VALORANT", MS("43432.13663857844271189.005b5800-25c5-439e-8a6e-fdb02b928900.96ac9b34-3c66-40bf-a23c-7251edc524dc")],
  ["Counter-Strike 2", ST(730)],
  ["Apex Legends", ST(1172470)],
  ["Overwatch 2", ST(2357570)],
  ["Rainbow Six Siege", ST(359550)],
  ["PUBG", ST(578080)],
];
const coverImg = ([name, src], hidden) => `<img src="${src}" alt="${hidden ? "" : esc(name) + " cover"}" loading="eager" decoding="async" referrerpolicy="no-referrer" width="160" height="240" onerror="this.remove()">`;
// each row is the list twice so the loop is seamless; the second copy is hidden from screen readers
const row = (list) => `<div class="marquee-track">${list.map((c) => coverImg(c, false)).join("")}<span class="dup" aria-hidden="true">${list.map((c) => coverImg(c, true)).join("")}</span></div>`;
const gamesStrip = `        <div class="marquee" aria-label="Games Framecalm supports">${row(covers)}</div>
        <div class="marquee reverse" aria-hidden="true">${row([...covers.slice(5), ...covers.slice(0, 5)])}</div>`;


// nine category panels for the sideways strip
const catIcon = { power: "p-chip", graphics: "p-gpu", network: "p-router", input: "p-mouse", background: "p-cube", services: "p-ram", visual: "p-crystal", privacy: "p-fan", system: "p-ssd" };
const catsHtml = CATALOG.categories.map((c) => {
  const mine = T.filter((t) => t.category === c.id);
  const tiersHere = ["safe", "advanced", "extreme"].map((tr) => mine.filter((t) => t.tier === tr).length);
  const sample = mine.slice(0, 4).map((t) => `<li>${meter(t.tier)}<span>${esc(t.name)}</span></li>`).join("");
  return `          <article class="cat-panel">
            <svg class="cat-art" aria-hidden="true"><use href="#${catIcon[c.id] ?? "p-cube"}"/></svg>
            <p class="cat-n"><b>${mine.length}</b> tweaks</p>
            <h3>${c.name}</h3>
            <p class="cat-about">${esc(c.about)}</p>
            <ul>${sample}</ul>
            <p class="cat-tiers"><span class="t-s">${tiersHere[0]} safe</span><span class="t-a">${tiersHere[1]} advanced</span><span class="t-e">${tiersHere[2]} extreme</span></p>
          </article>`;
}).join("\n");

let html = readFileSync(join(TOOLS, "index.template.html"), "utf8");
const fill = {
  COUNT: String(count), DOWNLOAD, PORTABLE, CF_TOKEN, JSONLD: jsonld, DATA: data,
  CONSOLE_TABS: tabs, GAMES: gamesStrip, CATS: catsHtml, CONSOLE_PANELS: panels, PRESETS: presets, CATALOG: catalogHtml, LEFTOUT: leftout,
  N_SAFE: String(T.filter((t) => t.tier === "safe").length),
  N_ADVANCED: String(T.filter((t) => t.tier === "advanced").length),
  N_EXTREME: String(T.filter((t) => t.tier === "extreme").length),
};
html = html.replace(/\{\{([A-Z_]+)\}\}/g, (m, k) => { if (!(k in fill)) throw new Error("unfilled " + k); return fill[k]; });
// cache-busting: styles.css and main.js get a content hash, so a deploy never mixes new HTML with an old cached stylesheet
const ver = (f) => createHash("sha256").update(readFileSync(join(SITE, f))).digest("hex").slice(0, 10);
html = html.replace('href="styles.css"', `href="styles.css?v=${ver("styles.css")}"`).replace('src="main.js"', `src="main.js?v=${ver("main.js")}"`);
write("index.html", html);

// ---------- /tweaks/: levels, the full list, what's left out ----------
let tw = readFileSync(join(TOOLS, "tweaks.template.html"), "utf8")
  .replace(/\{\{([A-Z_]+)\}\}/g, (m, k) => { if (!(k in fill)) throw new Error("unfilled " + k); return fill[k]; });
const twUrl = `${BASE}/tweaks/`;
write("tweaks/index.html",
  head({ title: `All ${count} Windows tweaks, with their registry keys | Framecalm`, description: `Every one of Framecalm's ${count} Windows tweaks: what it does, what it costs, its Safe, Advanced or Extreme tier and the exact registry value or power setting it writes.`, url: twUrl, root: "../" })
  + tw.replaceAll('href="tweaks/"', 'href="./"')
  + `  <script type="application/json" id="tweak-data">${data}</script>\n  <script src="../main.js?v=${ver("main.js")}" defer></script>\n`
  + foot("../"));



// ---------- sitemap ----------
const urls = [
  { loc: `${BASE}/`, priority: "1.0" },
  { loc: `${BASE}/tweaks/`, priority: "0.9" },
  { loc: `${BASE}/guides/`, priority: "0.8" },
  ...guides.map((g) => ({ loc: `${BASE}/guides/${g.slug}/`, priority: "0.7" })),
  { loc: `${BASE}/privacy.html`, priority: "0.3" },
];
write("sitemap.xml", `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls.map((u) => `  <url><loc>${u.loc}</loc><lastmod>${UPDATED}</lastmod><priority>${u.priority}</priority></url>`).join("\n")}
</urlset>
`);

console.log(`Built home page (${count} tweaks), ${guides.length} guides, guides index, privacy, 404 and sitemap (${urls.length} URLs).`);
