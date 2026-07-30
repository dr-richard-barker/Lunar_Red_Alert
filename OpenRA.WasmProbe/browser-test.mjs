// Phase W2 CI driver: load the published probe in headless Chromium, collect
// console output, require the W2 SUCCESS line, and save a screenshot artifact.
// Usage: node OpenRA.WasmProbe/browser-test.mjs http://127.0.0.1:8123/
import { chromium } from 'playwright';

const url = process.argv[2] || 'http://127.0.0.1:8123/';
const timeoutMs = 120000;

// Hard backstop: the boot-hang bug this guards against froze the renderer in
// a synchronous spin-loop, which can make Chromium's DevTools protocol itself
// stop responding -- at that point page.screenshot()/browser.close() below
// can hang right along with it, and the poll loop's own deadline never gets a
// chance to matter. Race every await against this timer so a frozen page
// fails the CI step in seconds instead of running out the job's multi-hour
// default timeout.
const watchdogMs = timeoutMs + 30000;
const raceWatchdog = (promise, label) => Promise.race([
	promise,
	new Promise((_, reject) => setTimeout(() => reject(new Error(`[driver] watchdog: ${label} did not settle within ${watchdogMs}ms — renderer likely hung`)), watchdogMs)),
]);

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 640, height: 480 } });

const lines = [];
page.on('console', m => { lines.push(m.text()); console.log('[console]', m.text()); });
page.on('pageerror', e => { lines.push('PAGEERROR: ' + e.message); console.error('[pageerror]', e.message); });

console.log('[driver] loading', url);
await raceWatchdog(page.goto(url, { waitUntil: 'domcontentloaded' }), 'page.goto');

// NOTE: W4a (the full Game.InitializeMod content boot) and W4c (the live
// rAF-driven game loop) are deliberately NOT required here — see Program.cs's
// comment above the MenuDemo call site. That full boot takes minutes under
// CI's software-GL Chromium, long enough that Chromium's own CDP console
// delivery falls behind and this poll loop times out even on a genuine
// success. This probe path only ever exercises up through the real load
// screen (W3i-b); live-loop coverage for the actual deployed page lives in
// the separate playwasm-smoke-test step against /play-wasm/ instead.
const required = ['W2 SUCCESS', 'W3a SUCCESS', 'W3b SUCCESS', 'W3c SUCCESS', 'W3d SUCCESS', 'W3e SUCCESS', 'W3f SUCCESS', 'W3g SUCCESS', 'W3h SUCCESS', 'W3i SUCCESS', 'W3i-b SUCCESS', 'W4b SUCCESS'];
const deadline = Date.now() + timeoutMs;
let ok = false, failed = false;
while (Date.now() < deadline && !ok && !failed) {
	ok = required.every(tag => lines.some(l => l.includes(tag)));
	failed = lines.some(l => l.includes('[probe] FAILED') || l.includes('PAGEERROR'));
	if (!ok && !failed)
		await new Promise(r => setTimeout(r, 500));
}

try {
	await raceWatchdog(page.screenshot({ path: 'wasm-screenshot.png', timeout: 15000 }), 'page.screenshot');
} catch (e) {
	console.error('[driver] screenshot unavailable:', e.message);
}

try {
	await raceWatchdog(browser.close(), 'browser.close');
} catch (e) {
	console.error('[driver] browser.close did not settle cleanly:', e.message);
	process.exit(1);
}

if (ok) {
	console.log('[driver] all required SUCCESS tags observed; screenshot saved');
	process.exit(0);
}
const missing = required.filter(tag => !lines.some(l => l.includes(tag)));
console.error(failed ? '[driver] probe reported failure' : `[driver] timed out; missing tags: ${missing.join(', ')}`);
process.exit(1);
