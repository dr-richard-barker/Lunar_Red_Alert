// Live-page smoke test: hits the ACTUAL deployed /play-wasm/?mode=play page
// (not the CI-only gated probe path in browser-test.mjs) and requires the
// real PlayMode boot to finish. This is the coverage gap that let the
// original boot-hang bug ship silently -- PlayMode.Run() (OpenRA.WasmProbe/
// Browser/PlayMode.cs) has no gates and no CI timeout of its own, so a
// regression there was only ever discoverable by a human loading the page.
//
// Usage: node OpenRA.WasmProbe/playwasm-smoke-test.mjs https://example.github.io/repo/play-wasm/ [terrain]
//
// Optional 3rd arg selects a reskinned map via the same ?terrain= query
// param the in-page toggle button uses ('lunar' or 'mars'; omit for the
// default Earth map). Screenshot filename is suffixed with the terrain name
// so a lunar/mars run doesn't clobber the default Earth screenshot when both
// run in the same CI job.
import { chromium } from 'playwright';

const url = process.argv[2];
if (!url) {
	console.error('[smoke] usage: node playwasm-smoke-test.mjs <play-wasm base url> [terrain]');
	process.exit(1);
}
const terrain = process.argv[3] || '';
const base = url.endsWith('/') ? url : `${url}/`;
const playUrl = terrain ? `${base}?mode=play&terrain=${terrain}` : `${base}?mode=play`;
const screenshotPath = terrain ? `playwasm-smoke-screenshot-${terrain}.png` : 'playwasm-smoke-screenshot.png';

// PlayMode's boot (MEMFS stage -> settings -> mods -> platform -> renderer ->
// sound -> Game.InitializeMod) took well under a minute in manual testing;
// double that for a cold CDN cache plus CI's slower, software-rendered
// Chromium, and add a hard watchdog on top so a genuinely hung renderer
// fails this step in around two minutes instead of running out the job's
// full timeout.
const timeoutMs = 90000;
const watchdogMs = timeoutMs + 30000;
const raceWatchdog = (promise, label) => Promise.race([
	promise,
	new Promise((_, reject) => setTimeout(() => reject(new Error(`[smoke] watchdog: ${label} did not settle within ${watchdogMs}ms — renderer likely hung`)), watchdogMs)),
]);

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });

const lines = [];
page.on('console', m => { lines.push(m.text()); console.log('[console]', m.text()); });
page.on('pageerror', e => { lines.push('PAGEERROR: ' + e.message); console.error('[pageerror]', e.message); });

console.log('[smoke] loading', playUrl);
await raceWatchdog(page.goto(playUrl, { waitUntil: 'domcontentloaded' }), 'page.goto');

const deadline = Date.now() + timeoutMs;
let ok = false, failed = false;
while (Date.now() < deadline && !ok && !failed) {
	ok = lines.some(l => l.includes('[play] boot complete'));
	failed = lines.some(l => l.includes('PAGEERROR') || l.includes('threw:'));
	if (!ok && !failed)
		await new Promise(r => setTimeout(r, 500));
}

// The page must also still be responsive after boot -- a frozen renderer
// stops posting new console lines but doesn't necessarily stop having
// printed "boot complete" moments before hanging, so a trivial live eval is
// the actual proof the tab isn't stuck.
let responsive = false;
if (ok) {
	try {
		const evalResult = await raceWatchdog(page.evaluate(() => 1 + 1), 'page.evaluate liveness check');
		responsive = evalResult === 2;
	} catch (e) {
		console.error('[smoke] liveness check failed:', e.message);
	}
}

try {
	await raceWatchdog(page.screenshot({ path: screenshotPath, timeout: 15000 }), 'page.screenshot');
} catch (e) {
	console.error('[smoke] screenshot unavailable:', e.message);
}

try {
	await raceWatchdog(browser.close(), 'browser.close');
} catch (e) {
	console.error('[smoke] browser.close did not settle cleanly:', e.message);
	process.exit(1);
}

if (ok && responsive) {
	console.log('[smoke] SUCCESS: live page reached "boot complete" and stayed responsive');
	process.exit(0);
}

if (failed)
	console.error('[smoke] FAILURE: page reported an error during boot');
else if (!ok)
	console.error(`[smoke] FAILURE: timed out waiting for "[play] boot complete" after ${timeoutMs}ms`);
else
	console.error('[smoke] FAILURE: boot completed but the page was unresponsive afterward');
process.exit(1);
