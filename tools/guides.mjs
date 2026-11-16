// Guide content for framecalm.krezio.xyz/guides. Bodies are plain HTML.
// Run `node tools/build-site.mjs` after editing.

export const guides = [
  {
    slug: "minecraft-settings-for-more-fps",
    title: "Minecraft Java settings for more FPS (what each one actually costs)",
    nav: "Minecraft Java: settings for more FPS",
    description: "Which Minecraft Java video settings really change your FPS, which barely matter, and the order to lower them in. Works for vanilla 1.20 and 1.21.",
    minutes: 6,
    tldr: "Lower <b>Render Distance</b> first, then <b>Simulation Distance</b>, set <b>Graphics</b> to Fast, and make sure <b>Max Framerate</b> isn't below your monitor's refresh rate. Clouds, particles and biome blend are smaller wins. For a much bigger jump, a performance mod like Sodium beats any setting.",
    body: `
<p>Minecraft is unusual: a lot of its cost is on the CPU, building chunks and ticking the world, not just drawing pixels. That's why some settings that look heavy barely matter, and some that look harmless cost a lot. Press <kbd>F3</kbd> in game to see your FPS in the top-left corner while you try these, and test in the same spot each time.</p>

<h2>1. Render Distance (biggest lever)</h2>
<p>Every extra chunk of distance adds a ring of chunks to build and draw, so the cost grows fast. Going from 16 to 10 chunks is often the single largest FPS gain you can get. 8 to 12 is a sensible range on laptops; fog just starts closer.</p>

<h2>2. Simulation Distance</h2>
<p>How far from you the world keeps ticking: mobs, crops, redstone. This is pure CPU work. If your FPS drops near farms or in busy villages, lowering this from 12 to 8 helps more than any graphics setting. The trade-off is that far-away farms pause until you get closer.</p>

<h2>3. Graphics: Fast</h2>
<p><b>Fancy</b> and <b>Fabulous!</b> draw see-through leaves and sort transparent objects, which costs a lot on integrated and laptop GPUs. <b>Fast</b> makes leaves solid. Most people stop noticing within minutes.</p>

<h2>4. Max Framerate and VSync</h2>
<p>Check <b>Max Framerate</b> under Video Settings. If it's 60 and your screen is 144 Hz, you're throwing away frames your monitor could show. Set it to your refresh rate, or Unlimited. <b>VSync</b> also caps you at the refresh rate and adds input delay; turn it off if you don't mind occasional tearing, or keep it on if you have a G-Sync/FreeSync screen and prefer smoothness.</p>

<h2>5. Smaller wins</h2>
<ul>
  <li><b>Clouds:</b> Fast or Off. Fancy 3D clouds are drawn every frame.</li>
  <li><b>Particles:</b> Decreased. Helps during explosions, rain and potion effects.</li>
  <li><b>Biome Blend:</b> 0 or 1. Faster chunk building, sharper colour edges between biomes.</li>
  <li><b>Entity Shadows:</b> Off. Tiny cost each, but there are a lot of mobs.</li>
  <li><b>Entity Distance:</b> 75% or 50% in crowded farms.</li>
</ul>

<h2>Things that don't help much</h2>
<ul>
  <li><b>Giving Minecraft tons of RAM.</b> Vanilla rarely needs more than 4 GB. Allocating 12 GB+ can make garbage-collection pauses longer, which feels like stutter.</li>
  <li><b>Mipmap levels.</b> Small effect on modern GPUs; turning them off makes distant blocks shimmer.</li>
  <li><b>FOV.</b> A wider FOV shows a bit more, but the cost is minor compared to render distance.</li>
</ul>

<h2>The biggest upgrade: a performance mod</h2>
<p>If you're fine with mods, <b>Sodium</b> (on Fabric) rewrites Minecraft's renderer and often gives far more FPS than every setting above combined. Get it only from its official Modrinth page.</p>

<h2>Check it's using the right GPU</h2>
<p>On laptops, Minecraft runs through Java, and Windows sometimes sends it to the integrated GPU. See <a href="../wrong-gpu-laptop/">is my game using the wrong GPU?</a></p>
`,
  },
  {
    slug: "fortnite-best-settings-low-end-pc",
    title: "Fortnite best settings for a low-end PC or laptop",
    nav: "Fortnite: best settings for low-end PCs",
    description: "The Fortnite settings that buy the most FPS on weak GPUs and laptops, in order, plus Performance mode and the frame-rate limit most people get wrong.",
    minutes: 6,
    tldr: "Switch <b>Rendering Mode</b> to Performance if your GPU is weak. Otherwise turn <b>Shadows</b> off, set <b>Global Illumination</b> and <b>Reflections</b> to Low, keep <b>View Distance</b> at Medium or Far, and set <b>Frame Rate Limit</b> to your monitor's refresh rate.",
    body: `
<p>Fortnite runs on Unreal Engine 5, and its expensive features (Lumen lighting, Nanite, virtual shadow maps) are aimed at strong desktop GPUs. On a laptop or older card, a few settings decide almost everything.</p>

<h2>1. Rendering Mode: Performance (weak GPUs)</h2>
<p>Under <b>Settings → Video → Rendering Mode</b>, the <b>Performance</b> mode is built for low-end hardware and integrated graphics. It looks simpler but is by far the biggest jump on weak machines. It needs a game restart.</p>

<h2>2. If you stay on DirectX 11/12: the heavy hitters</h2>
<ul>
  <li><b>Shadows: Off.</b> The most expensive setting on almost every GPU. Many competitive players run it off anyway.</li>
  <li><b>Global Illumination: Low.</b> Turns off Lumen's bounced lighting, a huge cost on mid-range cards.</li>
  <li><b>Reflections: Low.</b> Same story for Lumen reflections.</li>
  <li><b>Effects: Low</b> and <b>Post Processing: Low.</b> Helps in busy build fights.</li>
  <li><b>Foliage: Low.</b> Less grass to draw, and players are easier to spot.</li>
</ul>

<h2>3. Settings to be careful with</h2>
<ul>
  <li><b>View Distance:</b> keep it at Medium or Far. Lower can hide players and builds in the distance.</li>
  <li><b>3D Resolution:</b> lowering it gives a lot of FPS but makes everything blurry. Try other settings first, and don't go below about 75%.</li>
  <li><b>Textures:</b> mostly about video memory, not FPS. Lower them if you have 4 GB of VRAM or less and see stutter.</li>
</ul>

<h2>4. Frame Rate Limit and V-Sync</h2>
<p>Set <b>Frame Rate Limit</b> to your monitor's refresh rate (for example 144). A limit below your screen wastes frames; a much higher one just heats up a laptop. Turn <b>V-Sync</b> off unless tearing bothers you. On NVIDIA cards, turn on <b>NVIDIA Reflex Low Latency</b>.</p>

<h2>Why your settings sometimes come back</h2>
<p>Fortnite keeps a copy of your settings in Epic's cloud and can restore it, so a change made outside the game can be overwritten. Changing settings in the in-game menu is always safe. Framecalm edits Fortnite's settings file and then checks whether the change stuck; if Fortnite put the old values back, it tells you and shows the in-game steps instead.</p>

<h2>Measure it</h2>
<p>Turn on the built-in FPS counter (<b>Settings → Game UI → Performance Stats</b>) and compare the same drop spot or creative map before and after. See <a href="../how-to-measure-fps/">how to measure FPS properly</a>.</p>
`,
  },
  {
    slug: "wrong-gpu-laptop",
    title: "Is my game using the wrong GPU? How to check and fix it on Windows",
    nav: "Is my game using the wrong GPU?",
    description: "Gaming laptops have two graphics chips, and games sometimes start on the slow one. Here's how to check which GPU a game uses and pin it to the fast one.",
    minutes: 5,
    tldr: "Open <b>Task Manager</b>, add the <b>GPU engine</b> column and look at your game: GPU 0 is usually the integrated chip. To fix it, go to <b>Settings → System → Display → Graphics</b>, add the game and choose <b>High performance</b>.",
    body: `
<p>Most gaming laptops have two GPUs: a small integrated one inside the processor (Intel Iris/UHD or AMD Radeon) that saves battery, and a dedicated one (NVIDIA GeForce, AMD Radeon RX) that's several times faster. Windows and the graphics driver decide which one each app uses. Usually they get games right. Sometimes they don't, and the game runs at a fraction of the frame rate it should.</p>

<h2>Signs you're on the wrong GPU</h2>
<ul>
  <li>FPS is far lower than other people report with the same laptop.</li>
  <li>Lowering settings barely changes anything.</li>
  <li>The laptop stays quiet and cool while gaming.</li>
  <li>It's a game launched through Java (Minecraft), an emulator, or a launcher that starts another exe.</li>
</ul>

<h2>How to check</h2>
<ol>
  <li>Start the game, then press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Esc</kbd> to open Task Manager.</li>
  <li>On the <b>Processes</b> tab, right-click a column header and turn on <b>GPU engine</b>.</li>
  <li>Find the game. If it says <b>GPU 0</b>, check the <b>Performance</b> tab to see which chip GPU 0 is. On most laptops GPU 0 is the integrated one.</li>
</ol>

<h2>How to fix it in Windows</h2>
<ol>
  <li>Open <b>Settings → System → Display → Graphics</b>.</li>
  <li>Find the game in the list, or click <b>Browse</b> and pick the game's .exe (for Minecraft, that's the <code>javaw.exe</code> the launcher uses).</li>
  <li>Choose <b>High performance</b> and save. Restart the game.</li>
</ol>
<p>This is a per-app setting stored for your user account, and you can set it back to <b>Let Windows decide</b> any time.</p>

<h2>Other things that force the slow GPU</h2>
<ul>
  <li><b>Battery power.</b> Many laptops limit the dedicated GPU heavily on battery. Plug in.</li>
  <li><b>MUX switch / Advanced Optimus.</b> If your laptop has a "dGPU only" or "Ultimate" mode in its control app, using it removes the integrated chip from the path entirely, usually for a few extra percent.</li>
  <li><b>External monitor port.</b> On some laptops only certain ports are wired to the dedicated GPU (often the HDMI or a specific USB-C port).</li>
</ul>
<p>Framecalm checks every game you own for this and pins them in one click, using the same Windows setting.</p>
`,
  },
  {
    slug: "cpu-vs-gpu-bottleneck",
    title: "CPU vs GPU bottleneck, explained (and which settings matter for each)",
    nav: "CPU vs GPU bottleneck explained",
    description: "Every frame waits on either your processor or your graphics card. How to tell which one limits your game, and why the answer decides which settings are worth lowering.",
    minutes: 6,
    tldr: "If your GPU sits near 95-99% usage, you're <b>GPU-limited</b>: lower shadows, effects, anti-aliasing and resolution. If GPU usage is well below that and FPS doesn't rise when you lower resolution, you're <b>CPU-limited</b>: lower view distance, simulation and crowd settings instead.",
    body: `
<p>To make a frame, the CPU first works out what's happening (physics, AI, what's visible) and hands a list of drawing work to the GPU, which then draws it. Whichever side takes longer sets your frame rate. The other side waits.</p>

<h2>Why it matters</h2>
<p>Lowering a GPU-heavy setting only helps if the GPU is the one holding you back. If you're CPU-limited, turning shadows from Ultra to Low can change almost nothing, because the GPU was already waiting. That's the most common reason "I lowered everything and nothing changed".</p>

<h2>How to tell which one you are</h2>
<ol>
  <li><b>Watch GPU usage.</b> In Task Manager's Performance tab, or an overlay like the one in your GPU driver. Consistently 95-99% while playing means GPU-limited. Clearly lower (say 50-75%) usually means the CPU is the limit, or a frame cap is.</li>
  <li><b>The resolution test.</b> Drop the game's resolution or render scale a lot. If FPS jumps, you were GPU-limited. If it barely moves, you're CPU-limited.</li>
  <li><b>Check for a cap.</b> If FPS sits exactly at 60, 120 or 144 and the GPU isn't busy, a frame limit or V-Sync is holding you, not your hardware.</li>
</ol>
<p>Measuring tools like PresentMon report "GPU busy" time for every frame, which is the most precise version of this: if the GPU is busy for only 58% of each frame, the CPU (or a cap) is setting the pace. Framecalm uses exactly this.</p>

<h2>Settings that help when you're GPU-limited</h2>
<ul>
  <li>Shadows, global illumination, reflections, volumetric fog</li>
  <li>Anti-aliasing and post-processing</li>
  <li>Resolution / render scale, or upscalers like DLSS, FSR and XeSS</li>
</ul>

<h2>Settings that help when you're CPU-limited</h2>
<ul>
  <li>View distance, render distance, simulation distance</li>
  <li>Crowd, NPC and physics detail</li>
  <li>In some games, turning on multithreaded rendering</li>
  <li>Closing heavy background apps (browsers with many tabs, launchers, RGB software)</li>
</ul>

<h2>It can change mid-game</h2>
<p>A busy city or a 50-player fight can be CPU-limited while an empty field is GPU-limited. That's why measuring a repeatable, typical scene beats looking at a single number.</p>
`,
  },
  {
    slug: "does-game-mode-help",
    title: "Does Windows Game Mode help FPS? What it really does",
    nav: "Does Game Mode help?",
    description: "What Windows 11 and 10 Game Mode actually changes, whether it raises FPS, and whether you should leave it on.",
    minutes: 4,
    tldr: "Leave it <b>on</b>. Game Mode mostly stops Windows Update from installing drivers and showing restart prompts while you play, and gives the game a little priority. It rarely adds measurable FPS, but it can prevent stutter from background work.",
    body: `
<p>Game Mode is a switch in <b>Settings → Gaming → Game Mode</b>. It's on by default in Windows 11. Its reputation swings between "free FPS" and "makes things worse", and neither is quite right today.</p>

<h2>What it does</h2>
<ul>
  <li>Stops Windows Update from installing drivers and showing restart notifications while a game is in focus.</li>
  <li>Gives the game a bit more priority for CPU and GPU resources over background apps.</li>
  <li>Works automatically when Windows recognises a game; there's nothing to turn on per game.</li>
</ul>

<h2>Does it raise FPS?</h2>
<p>On a reasonably clean PC, the difference in average FPS is usually inside the normal run-to-run variation. Where it helps is consistency: if something heavy starts in the background (an update, an indexer), Game Mode makes it less likely to steal time from your game and cause a hitch.</p>

<h2>What about the old stutter reports?</h2>
<p>Early versions of Game Mode in 2017-2018 caused problems in some games. Those were fixed long ago. On current Windows 10 and 11 there's no good reason to turn it off.</p>

<h2>What helps more</h2>
<ul>
  <li>Making sure the game runs on the right GPU (<a href="../wrong-gpu-laptop/">how to check</a>).</li>
  <li>A frame cap that matches your monitor (<a href="../fix-60-fps-cap/">stuck at 60 FPS?</a>).</li>
  <li>The game's own graphics settings, chosen for whether you're <a href="../cpu-vs-gpu-bottleneck/">CPU- or GPU-limited</a>.</li>
  <li>Turning off background recording (<b>Settings → Gaming → Captures</b>) if you never use it.</li>
</ul>
`,
  },
  {
    slug: "how-to-measure-fps",
    title: "How to measure FPS properly (so you know a change actually helped)",
    nav: "How to measure FPS properly",
    description: "Why one glance at an FPS counter isn't proof, and a simple method for before-and-after tests: repeatable scenes, three runs, 1% lows and frame times.",
    minutes: 6,
    tldr: "Use a frame-time tool (PresentMon, CapFrameX, or a game's built-in benchmark), test the <b>same scene</b> for the same length, do <b>three runs</b> before and after, and compare average FPS <b>and</b> 1% lows. If the difference is smaller than how much your runs vary, it's noise.",
    body: `
<p>Most "this tweak gave me +30 FPS" claims come from glancing at a counter once before and once after. FPS swings a lot depending on where you look and what's happening, so a single glance can show almost any number. Here's how to test properly.</p>

<h2>1. Use a tool that records every frame</h2>
<ul>
  <li><b>PresentMon</b> (free, by Intel) records the time of every frame from Windows itself. Framecalm uses it.</li>
  <li><b>CapFrameX</b> is a free front-end for the same data, with charts.</li>
  <li><b>Built-in benchmarks</b> in games that have them are the most repeatable option of all.</li>
</ul>
<p>An in-game FPS counter is fine for a quick look, but it only shows an average over the last second.</p>

<h2>2. Pick a repeatable scene</h2>
<p>Same map, same spot, same camera direction, same thing happening, same length (30-60 seconds). Standing still in a busy area is fine. Different scenes can easily differ by 30% on their own.</p>

<h2>3. Do three runs, not one</h2>
<p>Run the same test three times. If the averages are 98, 101 and 96, your noise is a few percent. A change that moves you from about 98 to about 100 isn't proven; one that moves you to 130 is.</p>

<h2>4. Look at more than the average</h2>
<ul>
  <li><b>Average FPS</b> is total frames divided by total time.</li>
  <li><b>1% low</b> (or 99th-percentile frame time) shows your worst moments. A setting that leaves the average alone but raises the 1% low makes the game feel smoother.</li>
  <li><b>Frame time</b> in milliseconds: 16.7 ms is 60 FPS, 6.9 ms is 144 FPS. A frame-time graph shows stutters that averages hide.</li>
</ul>

<h2>5. Keep conditions the same</h2>
<ul>
  <li>Plugged in (laptops), same power mode.</li>
  <li>Let the game warm up a minute; the first run after launch often stutters while shaders compile.</li>
  <li>Same background apps open.</li>
  <li>Restart the game after changing settings that need it.</li>
</ul>
<p>Framecalm does all of this for you: three matched runs before and after, average, median and P95/P99 frame times, and a plain "within noise" verdict when nothing really changed.</p>
`,
  },
  {
    slug: "pc-optimization-myths",
    title: "PC gaming optimization myths: tweaks that don't raise FPS",
    nav: "PC optimization myths",
    description: "RAM cleaners, registry packs, HPET and timer tweaks, disabling services, core parking: what popular FPS tweaks really do, and what to do instead.",
    minutes: 7,
    tldr: "Most \"FPS booster\" tweaks are placebo on modern Windows, and some cause stutter or security problems. What reliably helps: the game's own settings, the right GPU, a correct frame cap, enough free disk space, cooling and up-to-date drivers.",
    body: `
<p>Search for "how to boost FPS" and you'll find the same list of tweaks copied between videos and tools for a decade. Many date from Windows 7. Here's what they actually do on Windows 10 and 11.</p>

<h2>RAM cleaners and "free up memory" buttons</h2>
<p><b>Myth.</b> Windows deliberately uses spare memory as cache, so it's supposed to look full. Forcing it empty makes Windows and your game reload things from disk, which can cause stutter. Unused RAM is wasted RAM.</p>

<h2>Registry "network throttling" and gaming packs</h2>
<p><b>Mostly myth.</b> Values like <code>NetworkThrottlingIndex</code> or <code>SystemResponsiveness</code> were relevant to old multimedia scheduling. Tests on current systems show no consistent FPS change, and random registry packs are hard to undo.</p>

<h2>Disabling HPET or changing timers with bcdedit</h2>
<p><b>Risky myth.</b> Windows chooses timers itself. Forcing settings with <code>bcdedit</code> can raise or lower results by a little depending on the PC, and occasionally causes stutter or boot problems. Not worth it.</p>

<h2>Disabling lots of Windows services</h2>
<p><b>Myth with side effects.</b> Idle services use almost no CPU. Disabling them in bulk can break updates, audio, Bluetooth, printing or the Microsoft Store, and saves nothing you'd measure in a game.</p>

<h2>Core parking and "unpark CPU" tools</h2>
<p><b>Outdated.</b> Modern CPUs and Windows schedulers handle this well. Any effect is within noise for games.</p>

<h2>Turning off Defender or Memory Integrity</h2>
<p><b>Bad trade.</b> The FPS effect of real-time protection during gaming is small, and you give up protection against exactly the kind of "free cheats" and cracked tools gamers get targeted with. Some anti-cheats also require these features.</p>

<h2>Setting the game to Realtime priority</h2>
<p><b>Harmful.</b> Realtime can starve the parts of Windows that handle input and audio. High priority is the most that makes sense, and usually gains little.</p>

<h2>What actually works</h2>
<ul>
  <li>The game's own graphics settings, chosen for your <a href="../cpu-vs-gpu-bottleneck/">bottleneck</a>.</li>
  <li>Making sure the game uses the <a href="../wrong-gpu-laptop/">right GPU</a>.</li>
  <li>A frame cap that matches your monitor (<a href="../fix-60-fps-cap/">stuck at 60?</a>).</li>
  <li>Good cooling, especially on laptops (<a href="../laptop-gaming-performance-checklist/">checklist</a>).</li>
  <li>Current graphics drivers from NVIDIA, AMD or Intel.</li>
  <li>Measuring before and after, so you <a href="../how-to-measure-fps/">know</a> what helped.</li>
</ul>
`,
  },
  {
    slug: "rocket-league-fps-settings",
    title: "Rocket League settings for more FPS and smoother gameplay",
    nav: "Rocket League: FPS settings",
    description: "Which Rocket League video settings cost the most FPS, which ones pros turn off for clarity, and how to set the frame rate cap correctly.",
    minutes: 4,
    tldr: "Turn off <b>Dynamic Shadows</b>, <b>Light Shafts</b>, <b>Depth of Field</b> and <b>Motion Blur</b>, set <b>World Detail</b> to Performance or Quality, and set the frame rate to Uncapped or your monitor's refresh rate with V-Sync off.",
    body: `
<p>Rocket League is light by modern standards, but it's also a game where smoothness and input delay matter a lot, and integrated GPUs struggle with its heavier effects. These are under <b>Settings → Video</b>.</p>

<h2>The ones that cost the most</h2>
<ul>
  <li><b>Dynamic Shadows: off.</b> The heaviest effect in the game. You keep the ball's ground indicator.</li>
  <li><b>World Detail: Performance or Quality.</b> Removes stadium clutter and crowd detail.</li>
  <li><b>Ambient Occlusion: off.</b> Expensive on laptop and integrated GPUs.</li>
  <li><b>Light Shafts and Bloom: off.</b> Pure visual effects.</li>
</ul>

<h2>Off for clarity as well as FPS</h2>
<ul>
  <li><b>Depth of Field</b> blurs the background, which makes far plays harder to read.</li>
  <li><b>Motion Blur</b> smears fast movement.</li>
</ul>

<h2>Frame rate and V-Sync</h2>
<p>Set <b>Max FPS</b> to Uncapped or at least your monitor's refresh rate, and turn <b>V-Sync</b> off for the lowest input delay. If you have a G-Sync or FreeSync screen and see tearing, cap the frame rate a few FPS below your refresh rate instead.</p>

<h2>Render quality</h2>
<p><b>Render Quality</b> changes the internal resolution. Lowering it gives a big FPS boost on weak hardware but makes the image blurry; keep it at High Quality unless nothing else is enough.</p>

<p>Framecalm can make the file-level changes for you (from Rocket League's <code>TASystemSettings.ini</code>), backed up and undoable, and measure the result.</p>
`,
  },
  {
    slug: "fix-60-fps-cap",
    title: "Stuck at 60 FPS on a 144 Hz monitor? 6 things to check",
    nav: "Stuck at 60 FPS? 6 things to check",
    description: "If your games never go above 60 FPS on a high refresh rate screen, one of these six settings is usually the cause. How to find it and fix it.",
    minutes: 5,
    tldr: "Check the refresh rate in <b>Settings → System → Display → Advanced display</b>, then the game's frame cap and V-Sync, your GPU driver's frame limit, battery saver, and your cable or port.",
    body: `
<p>FPS that sits exactly at 60, no matter what you change, almost never means your hardware is too slow. It means something is capping it. Work through these in order.</p>

<h2>1. Windows is running your screen at 60 Hz</h2>
<p>Go to <b>Settings → System → Display → Advanced display</b> and look at <b>Choose a refresh rate</b>. A new 144 Hz monitor often starts at 60 Hz. Pick the highest option.</p>

<h2>2. The game's frame cap</h2>
<p>Many games ship with a frame limit of 60, or set one when you first start them. Look for <b>Max Framerate</b>, <b>Frame Rate Limit</b> or <b>Max FPS</b> in video settings, and set it to your refresh rate or Unlimited.</p>

<h2>3. V-Sync</h2>
<p>V-Sync locks the game to the screen's refresh rate. If Windows thinks the screen is 60 Hz (step 1), V-Sync locks you to 60. Turn it off in the game, or fix the refresh rate first.</p>

<h2>4. A frame limit in your GPU driver</h2>
<p>NVIDIA Control Panel (<b>Manage 3D settings → Max Frame Rate</b>), the NVIDIA app, AMD Software (<b>Radeon Chill</b> or <b>Frame Rate Target Control</b>) can all cap games globally. Check they're off or set above your refresh rate.</p>

<h2>5. Battery saver and power mode</h2>
<p>On laptops, battery saver and some vendor "silent" modes cap the frame rate or GPU power. Plug in and set <b>Settings → System → Power &amp; battery → Power mode</b> to Best performance.</p>

<h2>6. The cable or port</h2>
<p>An old HDMI cable or port can only carry 1080p at 60 Hz. Use DisplayPort, or an HDMI 2.0 or newer cable and port, to get 144 Hz. On laptops, make sure you're plugged into a port wired to the dedicated GPU (<a href="../wrong-gpu-laptop/">more on that</a>).</p>

<p>Framecalm checks game frame caps against your screen's refresh rate automatically and fixes them where it supports the game.</p>
`,
  },
  {
    slug: "laptop-gaming-performance-checklist",
    title: "Gaming laptop running slow? A 10-step performance checklist",
    nav: "Gaming laptop checklist",
    description: "Gaming laptops lose a lot of performance to power limits, heat and the wrong GPU. A practical 10-step checklist to get the FPS your laptop should have.",
    minutes: 6,
    tldr: "Plug in, set Power mode to Best performance, make sure games use the dedicated GPU, keep the vents clear and the back raised, update the GPU driver, close heavy background apps, and leave 15% of your drive free.",
    body: `
<p>A gaming laptop can lose half its performance to things that have nothing to do with the game. Go through this list once and you'll usually find at least one.</p>

<ol>
  <li><b>Plug in.</b> On battery, most gaming laptops cut GPU power drastically.</li>
  <li><b>Power mode: Best performance.</b> <b>Settings → System → Power &amp; battery</b>. Also check your laptop maker's app (Armoury Crate, Legion Space, Omen Gaming Hub, etc.) for a Performance or Turbo mode.</li>
  <li><b>Use the dedicated GPU.</b> Check each game with Task Manager's GPU engine column and pin it to High performance. <a href="../wrong-gpu-laptop/">Step by step</a>.</li>
  <li><b>Consider the MUX switch.</b> If your laptop has a "dGPU only" or Advanced Optimus mode, it can add a few percent and lower latency.</li>
  <li><b>Keep it cool.</b> Don't game on a bed or your lap. Raise the back an inch so the intake vents breathe. Heat makes the CPU and GPU lower their clocks, which shows up as FPS that's good for a few minutes and then drops.</li>
  <li><b>Clean the vents.</b> Dust builds up within a year or two. Compressed air through the vents (with the laptop off) often fixes throttling.</li>
  <li><b>Update the graphics driver</b> from NVIDIA, AMD or Intel directly. New games often get fixes and performance updates.</li>
  <li><b>Close heavy background apps.</b> Browsers with many tabs, launchers you aren't using and RGB utilities all take CPU time. Check Task Manager while playing.</li>
  <li><b>Leave free space.</b> Keep at least 15% of your system drive free so Windows and shader caches have room. <a href="https://roomy.krezio.xyz">Roomy</a> (free, same maker) can help.</li>
  <li><b>Match the frame cap to your screen</b> and tune game settings for your <a href="../cpu-vs-gpu-bottleneck/">bottleneck</a>.</li>
</ol>

<h2>How to know if heat is the problem</h2>
<p>Play for 10 minutes and watch FPS. If it starts high and drops steadily while the fans get loud, that's thermal throttling. Framecalm's benchmarks watch clock speeds during a capture and tell you when they sag.</p>
`,
  },
  {
    slug: "best-power-plan-for-gaming",
    title: "Best Windows power plan for gaming (Balanced vs High performance vs Ultimate)",
    nav: "Best power plan for gaming",
    description: "Which Windows 10 and 11 power plan to use for games on a desktop or a laptop, what High performance and Ultimate Performance actually change, and how to switch.",
    minutes: 5,
    tldr: "On a <b>desktop</b>, High performance (or Ultimate Performance) keeps the CPU ready and avoids small hitches when it ramps up. On a <b>laptop</b>, plug in and set the Windows power mode to <b>Best performance</b>; forcing High performance on battery mostly adds heat. The biggest wins are usually elsewhere: the right GPU, the right refresh rate, and no power saving on USB and Wi-Fi.",
    body: `
<p>A power plan is a bundle of settings that decide how eagerly Windows lets the processor speed up, how quickly it parks cores, and what it puts to sleep. The differences between plans are real but smaller than most "FPS boost" videos suggest. Here's what each one does and when it matters.</p>

<h2>The plans, in plain English</h2>
<table>
  <tr><th>Plan</th><th>What it does</th><th>Good for</th></tr>
  <tr><td><b>Balanced</b></td><td>Lets the CPU drop to low clocks when idle and boost quickly under load. On modern CPUs it boosts almost as fast as High performance.</td><td>Laptops, and most desktops if you don't notice hitches</td></tr>
  <tr><td><b>High performance</b></td><td>Keeps the minimum processor state high and parks fewer cores, so the CPU is already awake when a game asks for work.</td><td>Desktops, competitive games, CPU-heavy games</td></tr>
  <tr><td><b>Ultimate Performance</b></td><td>High performance plus a few extra timers set to "never". Meant for workstations. Uses more power at idle.</td><td>Desktops where every millisecond matters</td></tr>
</table>

<h2>How to switch</h2>
<ol>
  <li>Press <kbd>Win</kbd> + <kbd>R</kbd>, type <code>powercfg.cpl</code> and press Enter.</li>
  <li>Pick a plan. If you only see Balanced, click <b>Show additional plans</b>.</li>
  <li>On Windows 11 laptops, also open <b>Settings › System › Power &amp; battery</b> and set <b>Power mode</b> to <b>Best performance</b> while plugged in.</li>
</ol>
<p>Ultimate Performance is hidden by default. To add it, open Command Prompt as administrator and run:</p>
<pre><code>powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61</code></pre>
<p>It then shows up in the list. To see which plans you have, run <code>powercfg /list</code>.</p>

<h2>Laptops: be careful</h2>
<p>Laptops are limited by heat, not by how eagerly the CPU wakes up. Forcing the CPU to stay at high clocks makes the fans louder and can make the GPU throttle sooner, because they share the same cooler. Plug in, use Best performance, and spend the effort on the things that really cost laptops frames: running games on the integrated GPU and heat.</p>

<h2>The settings inside the plan that matter more</h2>
<ul>
  <li><a href="../../tweaks/usb-suspend-off/">USB selective suspend</a>: turning it off stops mice and headsets dropping out after being idle.</li>
  <li><a href="../../tweaks/cpu-min-100/">Minimum processor state</a>: 100% keeps the CPU ready, which is most of what High performance does.</li>
  <li><a href="../../tweaks/core-parking-off/">Core parking</a>: stops Windows putting cores to sleep mid-game.</li>
  <li><a href="../../tweaks/power-throttling-off/">Power throttling</a>: stops Windows slowing down apps it thinks are in the background, like voice chat or a capture app.</li>
</ul>

<h2>How to know if it helped</h2>
<p>Average FPS rarely moves much. Look at <b>1% lows</b> and frame-time spikes instead: that's where a sleepy CPU shows up as stutter. Measure the same scene three times before and three times after. <a href="../how-to-measure-fps/">Here's how to measure FPS properly.</a></p>
`,
  },
  {
    slug: "hardware-accelerated-gpu-scheduling",
    title: "Hardware-accelerated GPU scheduling: should you turn it on for gaming?",
    nav: "Hardware-accelerated GPU scheduling: on or off?",
    description: "What hardware-accelerated GPU scheduling (HAGS) does in Windows 10 and 11, when it helps, when to leave it off, and how to switch it.",
    minutes: 4,
    tldr: "Turn it <b>on</b> if you use DLSS Frame Generation, which needs it. For other games the difference is small and can go either way, so test it: if you see new stutter or a capture tool misbehaves, turn it back off. It needs a restart either way.",
    body: `
<p>Normally Windows decides when each program gets time on the graphics card. Hardware-accelerated GPU scheduling (HAGS) hands that job to a scheduler on the GPU itself. The idea is a little less overhead and latency between the game and the screen.</p>

<h2>When it helps</h2>
<ul>
  <li><b>DLSS Frame Generation</b> on NVIDIA RTX 40-series and newer requires it. The option is greyed out in games without it.</li>
  <li>Some games show slightly better frame pacing, especially when the CPU is the bottleneck.</li>
</ul>

<h2>When to leave it off</h2>
<ul>
  <li>Older drivers, or a GPU too old to support it (the switch won't appear at all).</li>
  <li>If a capture or streaming tool, or a VR setup, starts stuttering after you turn it on.</li>
  <li>If your own before-and-after test shows worse 1% lows. It happens on some systems.</li>
</ul>

<h2>How to switch it</h2>
<ol>
  <li>Open <b>Settings › System › Display › Graphics</b>.</li>
  <li>On Windows 11, click <b>Change default graphics settings</b> (or <b>Advanced graphics settings</b>). On Windows 10 it's <b>Graphics settings</b> under Display.</li>
  <li>Switch <b>Hardware-accelerated GPU scheduling</b> on or off, then restart.</li>
</ol>
<p>If the switch isn't there, your GPU or driver doesn't support it. Update the graphics driver from NVIDIA, AMD or Intel first. Under the hood it's one registry value: <a href="../../tweaks/hags-on/">here's the exact key</a>.</p>

<h2>Test it, don't trust it</h2>
<p>Play the same scene with it off and on, after a restart each time, and compare 1% lows rather than average FPS. If you can't tell the difference, leave it on: it's the default on new Windows 11 installs with supported GPUs and it's required for frame generation.</p>
`,
  },
  {
    slug: "memory-integrity-vbs-gaming",
    title: "Memory Integrity and VBS: do they slow down games, and should you turn them off?",
    nav: "Memory Integrity (VBS) and gaming",
    description: "What virtualization-based security and Memory Integrity do in Windows 11, how much they can cost in games, how to check if they're on, and the real trade-off of turning them off.",
    minutes: 6,
    tldr: "Virtualization-based security (VBS) and Memory Integrity can cost some CPUs around 5 to 15% in games, and Microsoft itself has said gamers can turn them off. But they protect Windows from a whole class of kernel-level malware, and some work devices and anti-cheats require them. Check whether they're on first, and only turn them off on a personal gaming PC if you accept that trade.",
    body: `
<p>Windows 11 runs part of itself inside a small virtual machine to keep the most sensitive parts of the system out of reach of malware. That's <b>virtualization-based security (VBS)</b>. <b>Memory Integrity</b> (also called HVCI) uses it to check every driver before it's allowed to run. Both are on by default on many new PCs.</p>

<h2>Is it on?</h2>
<ol>
  <li>Press <kbd>Win</kbd> + <kbd>R</kbd>, type <code>msinfo32</code> and press Enter.</li>
  <li>On the System Summary page, look for <b>Virtualization-based security</b>. "Running" means it's on.</li>
  <li>For Memory Integrity specifically: <b>Windows Security › Device security › Core isolation details</b>.</li>
</ol>

<h2>How much does it cost?</h2>
<p>It depends a lot on the CPU. Older processors without fast support for the checks it does lose the most; very recent ones lose little. Tests on affected systems have shown roughly 5 to 15% lower FPS in some games. The only way to know for your PC is to measure the same scene with it on and off.</p>

<h2>What you give up</h2>
<ul>
  <li>Protection against malware that tries to load a malicious or vulnerable driver into the Windows kernel. That kind of attack is rare on home PCs, but it's serious when it happens.</li>
  <li>Some <b>work or school devices</b> require it by policy. Don't change it on a PC you don't own.</li>
  <li>A few <b>anti-cheat systems</b> and security tools expect it on. If a game complains after you turn it off, turn it back on.</li>
</ul>

<h2>How to turn Memory Integrity off</h2>
<ol>
  <li>Open <b>Windows Security › Device security › Core isolation details</b>.</li>
  <li>Switch <b>Memory integrity</b> off and restart.</li>
</ol>
<p>That leaves VBS itself running on some PCs. Turning off both parts takes two registry values, listed exactly on the <a href="../../tweaks/vbs-off/">VBS off tweak page</a> with the commands to undo it.</p>

<h2>A sensible way to decide</h2>
<ul>
  <li><b>Personal gaming PC, CPU a few years old, you keep Windows and drivers updated:</b> worth testing with it off.</li>
  <li><b>Work laptop, shared family PC, or you install a lot of random software:</b> leave it on.</li>
  <li><b>Very recent CPU:</b> measure first. The gain may be too small to be worth it.</li>
</ul>
<p>Framecalm puts this in its <b>Extreme</b> tier: it asks you to confirm, says what you lose, and can switch it back from History.</p>
`,
  },
  {
    slug: "debloat-windows-11-safely",
    title: "How to debloat Windows 11 safely (what to remove, and what to leave alone)",
    nav: "Debloat Windows 11 safely",
    description: "A careful Windows 11 debloat: which apps, startup items and background features are safe to remove or switch off, and which 'debloat scripts' break things you need later.",
    minutes: 7,
    tldr: "Uninstall apps you don't use from Settings, switch off startup apps you don't need, and turn off background features like widgets, tips and ad tracking. Leave Microsoft Store, Edge WebView2, Windows Update, Windows Security and the Xbox services you use alone. Avoid scripts that delete system components: you can't easily undo them, and updates can break.",
    body: `
<p>"Debloating" means removing the extra apps and background features Windows ships with. Done carefully it frees memory, startup time and a bit of background CPU. Done with an aggressive script, it can break the Store, Windows Update or games that rely on Xbox services, and leave no easy way back.</p>

<h2>1. Uninstall apps you don't use</h2>
<p>Open <b>Settings › Apps › Installed apps</b>, sort by name, and remove what you don't use: preinstalled games, trial apps, news and weather apps, and anything your PC maker added. This is the safest debloat there is, because you can reinstall any of it from the Store.</p>

<h2>2. Turn off startup apps</h2>
<p><b>Settings › Apps › Startup</b> (or Task Manager › Startup apps). Switch off launchers, updaters and chat apps you don't need the moment Windows starts. Keep your audio driver panel, touchpad driver and security software on.</p>

<h2>3. Switch off background features</h2>
<p>These cost a little memory and CPU each, and switching them off is fully reversible:</p>
<ul>
  <li><a href="../../tweaks/widgets-off/">Widgets</a> and the news feed</li>
  <li><a href="../../tweaks/background-apps-off/">Background apps</a> that run when you didn't open them</li>
  <li><a href="../../tweaks/suggestions-off/">Tips and suggestions</a> in Start and Settings</li>
  <li><a href="../../tweaks/ad-id-off/">The advertising ID</a></li>
  <li><a href="../../tweaks/web-search-off/">Web results in Start search</a></li>
  <li><a href="../../tweaks/edge-background-off/">Edge running in the background</a> after you close it</li>
  <li><a href="../../tweaks/copilot-off/">Copilot</a> and <a href="../../tweaks/recall-off/">Recall</a>, if you don't use them</li>
</ul>

<h2>What to leave alone</h2>
<table>
  <tr><th>Keep</th><th>Why</th></tr>
  <tr><td>Microsoft Store</td><td>Updates many built-in apps, and you'll want it to reinstall anything you removed.</td></tr>
  <tr><td>Edge WebView2 Runtime</td><td>Many apps and game launchers use it to draw their windows, even if you never open Edge.</td></tr>
  <tr><td>Windows Update services</td><td>Security fixes and drivers. Turning them off leaves you exposed and causes trouble later.</td></tr>
  <tr><td>Windows Security (Defender)</td><td>Tamper Protection turns it back on anyway, and you'd have no protection in between.</td></tr>
  <tr><td>Xbox app and Xbox services</td><td>Game Pass and many PC games use them for sign-in and saves. Only remove them if you never use either.</td></tr>
</table>

<h2>Why not just run a debloat script?</h2>
<p>Many popular scripts remove system packages, delete services or block update servers in one go. Some of that can't be undone without reinstalling Windows, and the next big Windows update may fail or put things back half-working. If you use one, read exactly what it does first, and make a restore point.</p>
<p>Framecalm's approach is the opposite: every change is a setting, listed with the exact value it writes, and every one can be switched back. Services are set to Disabled, never deleted.</p>
`,
  },
];
