// Generates the guide pages, guides index, privacy page, 404 page and sitemap for site/.
// Usage: node tools/build-site.mjs
import { mkdirSync, writeFileSync, readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { guides } from "./guides.mjs";

const SITE = join(dirname(fileURLToPath(import.meta.url)), "..", "site");
const BASE = "https://framecalm.krezio.xyz";
const UPDATED = "2026-11-16";
const DOWNLOAD = "https://github.com/krezio/framecalm/releases/latest/download/Framecalm-Setup.exe";
const PORTABLE = "https://github.com/krezio/framecalm/releases/latest/download/Framecalm-portable.zip";
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
      <nav aria-label="Footer"><a href="https://github.com/krezio/framecalm">Source code</a><a href="${root}download/">Download</a><a href="${root}tweaks/">All tweaks</a><a href="${root}guides/">Guides</a><a href="${root}faq/">FAQ</a><a href="${root}changelog/">Changelog</a><a href="${root}press/">Press</a><a href="${root}privacy.html">Privacy</a><a href="https://github.com/krezio/framecalm/issues">Report a problem</a><a href="https://roomy.krezio.xyz">Roomy</a><a href="https://krezio.xyz">Made by krezio</a></nav>
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
        <p>One click builds a plan for your PC, or pick from ${CATALOG.tweaks.length} Windows tweaks yourself. PC fixes, per-game settings and measured benchmarks too. Every change can be switched back. Free and open source, for Windows 10 and 11.</p>
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
    <p>Questions or concerns: <a href="https://github.com/krezio/framecalm/issues">open an issue on GitHub</a>.</p>
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
              <p class="tw-more"><a href="/tweaks/${t.id}/">How to do it by hand, and undo it</a></p>
              <ul class="tw-keys">${keys}</ul>
            </div>
          </details>
        </li>`;
  }).join("\n");
  return `      <div class="cat" data-cat="${c.id}">
        <div class="cat-head"><h3><a href="/tweaks/${c.id}/">${c.name}</a></h3><p>${esc(c.about)}</p></div>
        <ul class="tw-list">
${rows}
        </ul>
      </div>`;
}).join("\n");

const leftout = CATALOG.leftOut.map((l) => `        <div><dt>${esc(l.name)}</dt><dd>${esc(l.why)}</dd></div>`).join("\n");
const data = JSON.stringify(T.map((t) => ({ id: t.id, tier: t.tier, name: t.name, restart: !!t.restart, atoms: keyLines(t) }))).replace(/</g, "\\u003c");
const homeFaq = [...readFileSync(join(TOOLS, "index.template.html"), "utf8").matchAll(/<details><summary>(.*?)<\/summary><p>(.*?)<\/p><\/details>/g)]
  .map((m) => ({ "@type": "Question", name: m[1].replace(/<[^>]+>/g, ""), acceptedAnswer: { "@type": "Answer", text: m[2].replace(/<[^>]+>/g, "") } }));
const jsonld = JSON.stringify({
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "SoftwareApplication",
      name: "Framecalm",
      operatingSystem: "Windows 10, Windows 11",
      applicationCategory: "UtilitiesApplication",
      applicationSubCategory: "System optimizer",
      offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
      description: `Free, open source Windows optimizer: one-click plans for your hardware and ${count} tweaks from safe to extreme, each explained and undoable.`,
      url: `${BASE}/`,
      downloadUrl: DOWNLOAD,
      license: "https://opensource.org/licenses/MIT",
      isAccessibleForFree: true,
      screenshot: `${BASE}/assets/og.png`,
      featureList: ["One-click optimization plan", `${count} Windows tweaks with registry values`, "Restore point and full undo", "PC fixes", "Startup apps", "Game settings tuning", "Before and after benchmarks"],
      publisher: { "@id": `${BASE}/#org` },
    },
    { "@type": "Organization", "@id": `${BASE}/#org`, name: "krezio", url: "https://krezio.xyz", logo: `${BASE}/assets/icon-512.png`, sameAs: ["https://github.com/krezio", "https://github.com/krezio/framecalm"] },
    { "@type": "WebSite", name: "Framecalm", url: `${BASE}/`, publisher: { "@id": `${BASE}/#org` } },
    { "@type": "FAQPage", mainEntity: homeFaq },
  ],
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



// ---------- one page per tweak, one per category, FAQ, download, changelog, llms.txt ----------
const extra = buildSeoPages();

// ---------- sitemap ----------
const urls = [
  { loc: `${BASE}/`, priority: "1.0" },
  { loc: `${BASE}/download/`, priority: "0.9" },
  { loc: `${BASE}/tweaks/`, priority: "0.9" },
  { loc: `${BASE}/guides/`, priority: "0.8" },
  { loc: `${BASE}/faq/`, priority: "0.7" },
  ...guides.map((g) => ({ loc: `${BASE}/guides/${g.slug}/`, priority: "0.7" })),
  ...extra,
  { loc: `${BASE}/changelog/`, priority: "0.4" },
  { loc: `${BASE}/privacy.html`, priority: "0.3" },
];
write("sitemap.xml", `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls.map((u) => `  <url><loc>${u.loc}</loc><lastmod>${UPDATED}</lastmod><priority>${u.priority}</priority></url>`).join("\n")}
</urlset>
`);

console.log(`Built home page (${count} tweaks), ${guides.length} guides, guides index, privacy, 404 and sitemap (${urls.length} URLs).`);

// =====================================================================================
// Search pages: every tweak and category gets its own page, plus FAQ, download, changelog
// and llms.txt for AI search. Everything is generated from tweaks.json, so it stays true.
// =====================================================================================
function buildSeoPages() {
  const VERSION = (readFileSync(join(TOOLS, "..", "Directory.Build.props"), "utf8").match(/<Version>([^<]+)<\/Version>/) || [])[1] || "";
  const catOf = (id) => CATALOG.categories.find((c) => c.id === id);
  const tierText = {
    safe: "Safe: a clear benefit and nothing most people would miss.",
    advanced: "Advanced: trades some power use or a convenience for lower latency.",
    extreme: "Extreme: turns off a Windows protection or feature. Only for people who know what they give up.",
  };
  const presetNames = (t) => CATALOG.presets.filter((p) => t.presets.includes(p.id)).map((p) => p.name);
  const svcSc = { disabled: "disabled", manual: "demand", automatic: "auto", delayed: "delayed-auto" };
  const fmtDword = (n) => (n === 4294967295 ? "0xFFFFFFFF" : String(n));
  const label = (t) => {
    const a = t.atoms[0];
    if (a.service) return `${t.atoms.map((x) => x.service).slice(0, 2).join(", ")} service${t.atoms.length > 1 ? "s" : ""}`;
    if (a.power) return a.alias.split("/").pop().trim();
    return [...new Set(t.atoms.map((x) => x.name))].slice(0, 2).join(", ");
  };
  const defText = (a) => {
    if (a.service) return a.default ?? "not set";
    if (a.power) return a.default === undefined || a.default === null ? "depends on the power plan" : String(a.default);
    if (a.default === null || a.default === undefined) return "not set (the value doesn't exist)";
    return a.dword !== undefined ? fmtDword(a.default) : `"${a.default}"`;
  };
  const rows = (t) => t.atoms.map((a, i) => {
    const k = keyLines(t)[i];
    return `      <li><code class="where">${esc(k.where)}</code><span>Set to <code>${esc(k.value)}</code> · Windows default: ${esc(defText(a))}</span></li>`;
  }).join("\n");
  function commands(t) {
    const doIt = [], undo = [];
    let nic = false;
    for (const a of t.atoms) {
      if (a.service) {
        doIt.push(`sc config ${a.service} start= ${svcSc[a.start]}`);
        if (a.default) undo.push(`sc config ${a.service} start= ${svcSc[a.default] ?? "demand"}`);
      } else if (a.power) {
        const [sub, set] = a.power.split("/");
        doIt.push(`powercfg /setacvalueindex SCHEME_CURRENT ${sub} ${set} ${a.value}`, `powercfg /setdcvalueindex SCHEME_CURRENT ${sub} ${set} ${a.value}`);
        if (a.default !== undefined && a.default !== null) undo.push(`powercfg /setacvalueindex SCHEME_CURRENT ${sub} ${set} ${a.default}`, `powercfg /setdcvalueindex SCHEME_CURRENT ${sub} ${set} ${a.default}`);
      } else if (a.reg.includes("{nic}")) {
        nic = true;
      } else {
        const type = a.dword !== undefined ? "REG_DWORD" : "REG_SZ";
        const val = a.dword !== undefined ? fmtDword(a.dword) : `"${a.sz}"`;
        doIt.push(`reg add "${a.reg}" /v "${a.name}" /t ${type} /d ${val} /f`);
        if (a.default === null || a.default === undefined) undo.push(`reg delete "${a.reg}" /v "${a.name}" /f`);
        else undo.push(`reg add "${a.reg}" /v "${a.name}" /t ${type} /d ${a.dword !== undefined ? fmtDword(a.default) : `"${a.default}"`} /f`);
      }
    }
    if (t.atoms.some((a) => a.power)) { doIt.push("powercfg /setactive SCHEME_CURRENT"); if (undo.length) undo.push("powercfg /setactive SCHEME_CURRENT"); }
    return { doIt, undo, nic };
  }
  const cap = (s) => s[0].toUpperCase() + s.slice(1);
  const firstOf = (s) => s.split(/(?<=\.)\s/)[0];

  const out = [];
  const twRoot = "../../";

  // ---- one page per tweak ----
  for (const t of T) {
    const c = catOf(t.category);
    const url = `${BASE}/tweaks/${t.id}/`;
    const title = `${t.name} (${label(t)}) on Windows 10 and 11 | Framecalm`;
    const description = `${firstOf(t.what)} What it changes in the registry, what it costs you, and how to undo it.`;
    const { doIt, undo, nic } = commands(t);
    const admin = needsAdmin(t);
    const same = T.filter((x) => x.category === t.category && x.id !== t.id);
    const faq = [
      { q: `What does "${t.name}" do?`, a: t.what },
      { q: "What's the downside?", a: t.tradeoff },
      { q: "How do I undo it?", a: `Set the values back to the Windows default listed on this page${undo.length ? ", or run the undo commands" : ""}. In Framecalm, switch the tweak off or undo it from History and the original value comes back.` },
      { q: "Is it safe?", a: tierText[t.tier] },
    ];
    const jsonld = {
      "@context": "https://schema.org",
      "@graph": [
        {
          "@type": "TechArticle", headline: `${t.name} on Windows 10 and 11`, description, url, mainEntityOfPage: url,
          datePublished: UPDATED, dateModified: UPDATED, image: `${BASE}/assets/og.png`, proficiencyLevel: t.tier === "safe" ? "Beginner" : "Expert",
          about: { "@type": "SoftwareApplication", name: "Microsoft Windows", operatingSystem: "Windows 10, Windows 11" },
          author: { "@type": "Organization", name: "krezio", url: "https://krezio.xyz" },
          publisher: { "@type": "Organization", name: "krezio", logo: { "@type": "ImageObject", url: `${BASE}/assets/icon-512.png` } },
        },
        { "@type": "FAQPage", mainEntity: faq.map((f) => ({ "@type": "Question", name: f.q, acceptedAnswer: { "@type": "Answer", text: f.a } })) },
        {
          "@type": "BreadcrumbList", itemListElement: [
            { "@type": "ListItem", position: 1, name: "Framecalm", item: `${BASE}/` },
            { "@type": "ListItem", position: 2, name: "Tweaks", item: `${BASE}/tweaks/` },
            { "@type": "ListItem", position: 3, name: c.name, item: `${BASE}/tweaks/${c.id}/` },
            { "@type": "ListItem", position: 4, name: t.name, item: url },
          ],
        },
      ],
    };
    let manual = "";
    if (nic) manual += `    <p>This one is set per network adapter. In <code>regedit</code>, open <code>HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces</code>, find the subkey (a long ID in braces) that shows your adapter's IP address, and add the values from the table there. Repeat for each adapter you play on.</p>\n`;
    if (doIt.length) manual += `    <p>Open <b>Command Prompt${admin ? " as administrator" : ""}</b> and run:</p>\n    <pre><code>${esc(doIt.join("\n"))}</code></pre>\n`;
    if (undo.length) manual += `    <p>To undo it:</p>\n    <pre><code>${esc(undo.join("\n"))}</code></pre>\n`;
    manual += `    <p>${t.restart ? "Restart Windows afterwards. " : ""}Make a restore point first if you're changing several things at once. Framecalm does that for you, keeps the old values, and checks each one after writing it.</p>\n`;

    write(`tweaks/${t.id}/index.html`, head({ title, description, url, root: twRoot, jsonld, type: "article" }) + `
  <main class="article" id="main">
    <p class="label"><a href="${twRoot}">Framecalm</a> › <a href="../">Tweaks</a> › <a href="../${c.id}/">${esc(c.name)}</a></p>
    <h1>${esc(t.name)}</h1>
    <p class="label tw-meta">${meter(t.tier)} ${cap(t.tier)} · ${impactText[t.impact]} · Windows 10 and 11</p>
    <div class="tldr"><p class="tldr-h">What it does</p><p>${esc(t.what)}</p></div>
    <table class="facts">
      <tr><th>Category</th><td><a href="../${c.id}/">${esc(c.name)}</a></td></tr>
      <tr><th>Tier</th><td>${esc(tierText[t.tier])}</td></tr>
      <tr><th>Expected difference</th><td>${impactText[t.impact]}</td></tr>
      <tr><th>Needs admin</th><td>${admin ? "Yes" : "No, it's a per-user setting"}</td></tr>
      <tr><th>Restart</th><td>${t.restart ? "Needed before it takes effect" : "Not needed"}</td></tr>
      <tr><th>Framecalm presets</th><td>${presetNames(t).join(", ") || "Not in a preset. Pick it yourself."}</td></tr>
    </table>
    <h2>The trade-off</h2>
    <p>${esc(t.tradeoff)}</p>
    <h2>Exactly what it changes</h2>
    <ul class="changes">
${rows(t)}
    </ul>
    <h2>Do it by hand</h2>
${manual}${cta(twRoot)}
    <h2>More ${esc(c.name.toLowerCase())} tweaks</h2>
    <ul class="guide-list">
${same.map((x) => `      <li><a href="../${x.id}/">${esc(x.name)}</a><p>${esc(firstOf(x.what))}</p></li>`).join("\n")}
    </ul>
  </main>
` + foot(twRoot));
    out.push({ loc: url, priority: "0.6" });
  }

  // ---- one page per category ----
  for (const c of CATALOG.categories) {
    const mine = T.filter((t) => t.category === c.id);
    const url = `${BASE}/tweaks/${c.id}/`;
    const title = `${mine.length} ${c.name} tweaks for Windows 10 and 11, explained | Framecalm`;
    const description = `${c.about} Every ${c.name.toLowerCase()} tweak in Framecalm, with what it does, what it costs and the exact registry value.`;
    const jsonld = {
      "@context": "https://schema.org",
      "@graph": [
        { "@type": "CollectionPage", name: title, url, description, hasPart: mine.map((t) => ({ "@type": "TechArticle", headline: t.name, url: `${BASE}/tweaks/${t.id}/` })) },
        {
          "@type": "BreadcrumbList", itemListElement: [
            { "@type": "ListItem", position: 1, name: "Framecalm", item: `${BASE}/` },
            { "@type": "ListItem", position: 2, name: "Tweaks", item: `${BASE}/tweaks/` },
            { "@type": "ListItem", position: 3, name: c.name, item: url },
          ],
        },
      ],
    };
    write(`tweaks/${c.id}/index.html`, head({ title, description, url, root: twRoot, jsonld }) + `
  <main class="article" id="main">
    <p class="label"><a href="${twRoot}">Framecalm</a> › <a href="../">Tweaks</a></p>
    <h1>${esc(c.name)} tweaks</h1>
    <p class="muted">${esc(c.about)} ${mine.length} tweaks: ${["safe", "advanced", "extreme"].map((tr) => `${mine.filter((t) => t.tier === tr).length} ${tr}`).join(", ")}.</p>
    <ul class="guide-list" style="margin-top:28px">
${mine.map((t) => `      <li><a href="../${t.id}/">${esc(t.name)}</a><p>${cap(t.tier)} · ${impactText[t.impact]}. ${esc(t.what)}</p></li>`).join("\n")}
    </ul>
${cta(twRoot)}
    <h2>Other categories</h2>
    <ul class="guide-list">
${CATALOG.categories.filter((o) => o.id !== c.id).map((o) => `      <li><a href="../${o.id}/">${esc(o.name)}</a><p>${esc(o.about)}</p></li>`).join("\n")}
    </ul>
  </main>
` + foot(twRoot));
    out.push({ loc: url, priority: "0.7" });
  }

  // ---- FAQ ----
  const homeFaq = [...readFileSync(join(TOOLS, "index.template.html"), "utf8").matchAll(/<details><summary>(.*?)<\/summary><p>(.*?)<\/p><\/details>/g)].map((m) => ({ q: m[1], a: m[2] }));
  const moreFaq = [
    { q: "Does it work on Windows 10?", a: "Yes. Framecalm runs on Windows 10 (version 2004 or newer) and Windows 11, 64-bit." },
    { q: "Does it work on laptops?", a: "Yes. The one-click plan checks whether it's running on a laptop and skips tweaks that cost battery life or add heat. It can also pin games to the dedicated graphics card when a laptop keeps using the integrated one." },
    { q: "What's the difference between Safe, Advanced and Extreme?", a: `${tierText.safe} ${tierText.advanced} ${tierText.extreme}` },
    { q: "Which games does it support?", a: "It finds games from Steam, Epic, Riot, Battle.net, the Xbox app, Minecraft and Roblox. It tunes the settings files of Minecraft Java, Fortnite and Rocket League for your hardware, and gives exact in-game steps for games with strict anti-cheat. The Windows tweaks help every game." },
    { q: "Windows SmartScreen warned me. Is it safe?", a: "SmartScreen warns about new programs that aren't code-signed yet or haven't been downloaded much. Framecalm is open source, so you can read the code or build it yourself. Click More info, then Run anyway." },
    { q: "Does it need the internet?", a: "No. It works offline. It can download game cover images and check for a newer version, and both can be switched off in Settings." },
    { q: "Where does it keep backups?", a: "In %LOCALAPPDATA%\\Framecalm on your PC: the original value of everything it changed, the history and its settings. Nothing is uploaded." },
    { q: "How do I uninstall it?", a: "First open History and choose Undo everything if you want Windows exactly as it was. Then uninstall Framecalm from Settings, Apps, Installed apps. The portable version is just a folder you can delete." },
    { q: "What if a tweak causes a problem?", a: "Open History and undo it: the exact old value is written back. If Windows won't start normally, use the restore point Framecalm made before the first change. Please also report it on GitHub so it can be fixed." },
  ];
  const allFaq = [...homeFaq, ...moreFaq];
  const strip = (s) => s.replace(/<[^>]+>/g, "");
  write("faq/index.html", head({
    title: "Framecalm FAQ: is it safe, will it raise FPS, how to undo | Framecalm",
    description: "Answers about Framecalm, the free Windows optimizer: what it changes, whether it's safe, admin rights, anti-cheat, laptops, SmartScreen, backups and how to undo or uninstall it.",
    url: `${BASE}/faq/`, root: "../",
    jsonld: { "@context": "https://schema.org", "@type": "FAQPage", mainEntity: allFaq.map((f) => ({ "@type": "Question", name: strip(f.q), acceptedAnswer: { "@type": "Answer", text: strip(f.a) } })) },
  }) + `
  <main class="article" id="main">
    <p class="label"><a href="../">Framecalm</a> › FAQ</p>
    <h1>Questions</h1>
${allFaq.map((f) => `    <h2>${f.q}</h2>\n    <p>${f.a}</p>`).join("\n")}
${cta("../")}
  </main>
` + foot("../"));

  // ---- download ----
  write("download/index.html", head({
    title: `Download Framecalm ${VERSION} for Windows 10 and 11 (free) | Framecalm`,
    description: `Download Framecalm ${VERSION}, the free and open source Windows optimizer. Installer or portable zip, no account, no admin needed to install. Windows 10 and 11, 64-bit.`,
    url: `${BASE}/download/`, root: "../",
    jsonld: {
      "@context": "https://schema.org", "@type": "SoftwareApplication", name: "Framecalm", softwareVersion: VERSION,
      operatingSystem: "Windows 10, Windows 11", applicationCategory: "UtilitiesApplication", downloadUrl: DOWNLOAD, installUrl: DOWNLOAD,
      license: "https://opensource.org/licenses/MIT", isAccessibleForFree: true, url: `${BASE}/download/`,
      offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
      publisher: { "@type": "Organization", name: "krezio", url: "https://krezio.xyz" },
    },
  }) + `
  <main class="article" id="main">
    <p class="label"><a href="../">Framecalm</a> › Download</p>
    <h1>Download Framecalm</h1>
    <p class="muted">Version ${VERSION}. Free and open source. No account, no ads, no telemetry.</p>
    <p style="margin-top:28px"><a class="btn btn-primary" href="${DOWNLOAD}">Download installer (.exe)</a> <a class="btn" href="${PORTABLE}">Portable zip</a></p>
    <h2>Which one?</h2>
    <ul>
      <li><b>Installer:</b> installs for your account only, so it doesn't need admin to install. Adds a Start menu entry and an uninstaller.</li>
      <li><b>Portable:</b> unzip anywhere and run <code>Framecalm.exe</code>. Nothing is installed. Delete the folder to remove it.</li>
    </ul>
    <h2>System requirements</h2>
    <table class="facts">
      <tr><th>Windows</th><td>Windows 10 (version 2004 or newer) or Windows 11, 64-bit</td></tr>
      <tr><th>Memory</th><td>4 GB or more</td></tr>
      <tr><th>Disk</th><td>About 200 MB</td></tr>
      <tr><th>Admin rights</th><td>Not needed to install. Windows asks once per batch when you apply machine-wide settings.</td></tr>
    </table>
    <h2>"Windows protected your PC"</h2>
    <p>Windows SmartScreen shows this for new programs that aren't code-signed yet or haven't been downloaded many times. Click <b>More info</b>, then <b>Run anyway</b>. If you'd rather check first, the <a href="https://github.com/krezio/framecalm">full source code is on GitHub</a> and you can build it yourself.</p>
    <h2>Code signing</h2>
    <p>Framecalm's Windows releases are built automatically by GitHub Actions from the public source code. We've applied to the <a href="https://signpath.org">SignPath Foundation</a>'s free code signing program for open source projects. Once approved, releases will be signed with a certificate issued by SignPath Foundation, so Windows shows a verified publisher instead of "Unknown publisher".</p>
    <h2>After installing</h2>
    <ol>
      <li>Open Framecalm. It looks at your PC first and changes nothing.</li>
      <li>Press <b>Optimize now</b> for the one-click plan, or open <b>Tweaks</b> and pick changes yourself.</li>
      <li>Everything you change is listed under <b>History</b>, where you can undo any of it.</li>
    </ol>
    <h2>Uninstalling</h2>
    <p>Open <b>History</b> and choose <b>Undo everything</b> if you want Windows exactly as it was, then remove Framecalm from <b>Settings › Apps › Installed apps</b>.</p>
    <p class="label" style="margin-top:28px">Release notes: <a href="../changelog/">changelog</a> · <a href="https://github.com/krezio/framecalm/releases">GitHub releases</a></p>
  </main>
` + foot("../"));

  // ---- changelog ----
  const changes = [
    { v: "0.5.3", d: "2026-11-17", items: ["Updates install from inside the app: Get it downloads the new version, checks it against GitHub's checksum, installs it and reopens Framecalm.", "The update notice at the top is easier to read."] },
    { v: "0.5.2", d: "2026-11-15", items: ["The update check and download links now point to the main open source repository.", "The Microsoft Store build leaves updates and starting with Windows to the Store."] },
    { v: "0.5.1", d: "2026-11-15", items: ["Framecalm is now open source under the MIT license. Settings links to the code."] },
    { v: "0.5.0", d: "2026-11-15", items: ["A complete redesign: top navigation, a one-click Home screen, and list-style Tweaks, PC fixes and Startup apps pages.", "The Onest typeface throughout."] },
    { v: "0.4.0", d: "2026-11-15", items: ["One-click Optimize builds a plan for your PC and applies it with a restore point first.", `${count} tweaks in nine categories, each marked Safe, Advanced or Extreme.`, "PC fixes, startup apps, game tuning, benchmarks, Game Session and full History with undo.", "Tray icon, optional start with Windows, and a notice when a new version is out."] },
  ];
  write("changelog/index.html", head({
    title: "Framecalm changelog: what's new in each version | Framecalm",
    description: "Every Framecalm release and what changed in it.",
    url: `${BASE}/changelog/`, root: "../",
  }) + `
  <main class="article" id="main">
    <p class="label"><a href="../">Framecalm</a> › Changelog</p>
    <h1>Changelog</h1>
${changes.map((r) => `    <h2>${r.v}</h2>\n    <p class="label">${r.d}</p>\n    <ul>${r.items.map((i) => `<li>${esc(i)}</li>`).join("")}</ul>`).join("\n")}
    <p style="margin-top:28px"><a class="btn btn-primary" href="../download/">Download the latest version</a></p>
  </main>
` + foot("../"));

  // ---- press kit ----
  const shots = [["dashboard", "Home: one button builds a plan for this PC"], ["auto", "The plan, with a reason on every line"], ["optimize", "Tweaks: each one shows the exact value it writes"], ["this-pc", "PC fixes Windows gets wrong on its own"], ["games", "Game settings tuned for the hardware"], ["benchmarks", "Before and after benchmarks"], ["history", "History: every change, with undo"]];
  write("press/index.html", head({
    title: "Framecalm press kit: facts, logos and screenshots | Framecalm",
    description: "Everything for writing about Framecalm, the free and open source Windows optimizer: a short description, key facts, logos and screenshots.",
    url: `${BASE}/press/`, root: "../",
  }) + `
  <main class="article" id="main">
    <p class="label"><a href="../">Framecalm</a> › Press</p>
    <h1>Press kit</h1>
    <div class="tldr"><p class="tldr-h">In one sentence</p><p>Framecalm is a free, open source Windows optimizer: one click builds a plan for your PC, or pick from ${count} tweaks, each explained and undoable.</p></div>
    <h2>Key facts</h2>
    <table class="facts">
      <tr><th>Made by</th><td>krezio, an independent developer</td></tr>
      <tr><th>Price</th><td>Free. No ads, no account, no telemetry.</td></tr>
      <tr><th>License</th><td>MIT. <a href="https://github.com/krezio/framecalm">Source on GitHub</a></td></tr>
      <tr><th>Platform</th><td>Windows 10 (2004 or newer) and Windows 11, 64-bit</td></tr>
      <tr><th>Current version</th><td>${VERSION}</td></tr>
      <tr><th>Tweaks</th><td>${count} in nine categories, each marked Safe, Advanced or Extreme</td></tr>
      <tr><th>Download</th><td><a href="../download/">framecalm.krezio.xyz/download</a></td></tr>
    </table>
    <h2>What makes it different</h2>
    <ul>
      <li><b>Shows its work.</b> Every tweak lists the exact registry value or power setting it writes, on the <a href="../tweaks/">public tweak list</a> and in the app.</li>
      <li><b>Everything can be undone.</b> A restore point first, every original value saved and read back, and a History screen that puts any change back.</li>
      <li><b>Leaves out the placebo.</b> No RAM cleaners, no "network packs", no turning off Defender. The site explains why.</li>
      <li><b>Hardware-aware.</b> The one-click plan checks laptop or desktop, GPU and what's already set. Laptops skip tweaks that cost battery and heat.</li>
      <li><b>Safe by design.</b> The main app never runs as admin. A small helper does the elevated writes and refuses any key that isn't in the published catalog.</li>
    </ul>
    <h2>Logos</h2>
    <p><a href="../assets/icon-512.png">App icon, 512 px PNG</a> · <a href="../assets/logo.svg">Logo, SVG</a> · <a href="../assets/og.png">Social image, 1200×630</a></p>
    <h2>Screenshots</h2>
    <p class="muted">Taken on Framecalm's demo mode, which simulates a gaming laptop. Free to use in coverage of Framecalm.</p>
    <ul class="guide-list">
${shots.map(([f, c]) => `      <li><a href="../assets/shots/${f}.webp">${c}</a></li>`).join("\n")}
    </ul>
    <h2>Contact</h2>
    <p>Questions, review builds or interviews: <a href="https://github.com/krezio/framecalm/issues">open an issue on GitHub</a>, or reach krezio at <a href="https://krezio.xyz">krezio.xyz</a>.</p>
  </main>
` + foot("../"));
  out.push({ loc: `${BASE}/press/`, priority: "0.4" });

  // ---- llms.txt and llms-full.txt: a plain summary for AI assistants and AI search ----
  writeFileSync(join(SITE, "llms.txt"), `# Framecalm

> Framecalm is a free, open source (MIT) Windows 10 and 11 optimizer for games and everyday speed. One click builds a tuning plan from the PC's hardware, or the user picks from ${count} tweaks. Every change is explained, saved first and can be undone. No account, no ads, no telemetry.

Made by krezio. Current version: ${VERSION}. Source code: https://github.com/krezio/framecalm

## Key facts
- Price: free. License: MIT.
- Platform: Windows 10 (2004+) and Windows 11, 64-bit.
- ${count} tweaks in nine categories (${CATALOG.categories.map((c) => c.name).join(", ")}), each marked Safe, Advanced or Extreme, each listing the exact registry value or power setting it writes.
- Safety: makes a System Restore point before the first admin change, saves every original value, reads each value back after writing, rolls back the whole batch on any mismatch, and lists every change in History for undo.
- The main app runs without admin; a small helper does elevated writes and refuses any registry key not in the published catalog.
- Never touches game files, game memory, launch options or anti-cheat.
- Deliberately left out: ${CATALOG.leftOut.map((l) => l.name).join(", ")}.
- Also: PC fixes (screen below its refresh rate, Wi-Fi power saving, Game DVR, power plans), startup apps, game detection (Steam, Epic, Riot, Battle.net, Xbox app, Minecraft, Roblox), settings tuning for Minecraft Java, Fortnite and Rocket League, PresentMon benchmarks, Game Session.

## Pages
- [Home](${BASE}/): what Framecalm does
- [Download](${BASE}/download/): installer and portable zip, requirements, SmartScreen
- [All tweaks](${BASE}/tweaks/): the full catalog
- [FAQ](${BASE}/faq/): safety, anti-cheat, laptops, undo, uninstall
- [Guides](${BASE}/guides/): plain-English PC gaming performance guides
- [Full catalog as text](${BASE}/llms-full.txt): every tweak with its registry values

## Tweak categories
${CATALOG.categories.map((c) => `- [${c.name}](${BASE}/tweaks/${c.id}/): ${c.about}`).join("\n")}

## Guides
${guides.map((g) => `- [${g.nav}](${BASE}/guides/${g.slug}/): ${g.description}`).join("\n")}
`);
  writeFileSync(join(SITE, "llms-full.txt"), `# Framecalm: every tweak

Framecalm ${VERSION}, free and open source (MIT) Windows optimizer. ${BASE}/
Each tweak below lists what it does, its trade-off, its tier and the exact values it writes.

${CATALOG.categories.map((c) => `## ${c.name}\n\n${c.about}\n\n${T.filter((t) => t.category === c.id).map((t) => `### ${t.name}\n\n- Page: ${BASE}/tweaks/${t.id}/\n- Tier: ${t.tier}. Expected difference: ${impactText[t.impact]}.\n- What it does: ${t.what}\n- Trade-off: ${t.tradeoff}\n- Writes:\n${keyLines(t).map((k, i) => `  - ${k.where} = ${k.value} (Windows default: ${defText(t.atoms[i])})`).join("\n")}`).join("\n\n")}`).join("\n\n")}

## Left out on purpose

${CATALOG.leftOut.map((l) => `- ${l.name}: ${l.why}`).join("\n")}
`);
  return out;
}
