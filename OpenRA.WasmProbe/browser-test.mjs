// Phase W2 CI driver: load the published probe in headless Chromium, collect
// console output, require the W2 SUCCESS line, and save a screenshot artifact.
// Usage: node OpenRA.WasmProbe/browser-test.mjs http://127.0.0.1:8123/
import { chromium } from 'playwright';

const url = process.argv[2] || 'http://127.0.0.1:8123/';
const timeoutMs = 180000;

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 640, height: 480 } });

const lines = [];
page.on('console', m => { lines.push(m.text()); console.log('[console]', m.text()); });
page.on('pageerror', e => { lines.push('PAGEERROR: ' + e.message); console.error('[pageerror]', e.message); });

console.log('[driver] loading', url);
await page.goto(url, { waitUntil: 'domcontentloaded' });

const required = ['W2 SUCCESS', 'W3a SUCCESS', 'W3b SUCCESS', 'W3c SUCCESS', 'W3d SUCCESS', 'W3e SUCCESS', 'W3f SUCCESS', 'W3g SUCCESS', 'W3h SUCCESS', 'W3i SUCCESS', 'W3i-b SUCCESS', 'W3i-c SUCCESS', 'W4a SUCCESS', 'W4b SUCCESS', 'W4c SUCCESS', 'W4d SUCCESS'];
const deadline = Date.now() + timeoutMs;
let ok = false, failed = false;
while (Date.now() < deadline && !ok && !failed) {
	ok = required.every(tag => lines.some(l => l.includes(tag)));
	failed = lines.some(l => l.includes('[probe] FAILED') || l.includes('PAGEERROR'));
	if (!ok && !failed)
		await new Promise(r => setTimeout(r, 500));
}

try {
	await page.screenshot({ path: 'wasm-screenshot.png', timeout: 15000 });
} catch {
	console.error('[driver] screenshot unavailable (page busy)');
}
await browser.close();

if (ok) {
	console.log('[driver] W2 SUCCESS observed; screenshot saved');
	process.exit(0);
}
console.error(failed ? '[driver] probe reported failure' : '[driver] timed out waiting for W2 SUCCESS');
process.exit(1);
