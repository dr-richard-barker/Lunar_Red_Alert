import { chromium } from 'playwright';

const url = process.argv[2];
if (!url) {
	console.error('[ai-test] usage: node ai-test.mjs <play-wasm base url>');
	process.exit(1);
}
const playUrl = url.endsWith('/') ? `${url}?mode=play&autoplay=1` : `${url}/?mode=play&autoplay=1`;

// We need a longer timeout for a full game to play out with AI. 5 minutes?
const timeoutMs = 5 * 60 * 1000;
const watchdogMs = timeoutMs + 30000;
const raceWatchdog = (promise, label) => Promise.race([
	promise,
	new Promise((_, reject) => setTimeout(() => reject(new Error(`[ai-test] watchdog: ${label} did not settle within ${watchdogMs}ms`)), watchdogMs)),
]);

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });

const lines = [];
page.on('console', m => { lines.push(m.text()); console.log('[console]', m.text()); });
page.on('pageerror', e => { lines.push('PAGEERROR: ' + e.message); console.error('[pageerror]', e.message); });

console.log('[ai-test] loading', playUrl);
await raceWatchdog(page.goto(playUrl, { waitUntil: 'domcontentloaded' }), 'page.goto');

// Wait for boot complete
console.log('[ai-test] Waiting for boot complete...');
const bootDeadline = Date.now() + 90000;
let booted = false;
let failed = false;
while (Date.now() < bootDeadline && !booted && !failed) {
	booted = lines.some(l => l.includes('[play] boot complete'));
	failed = lines.some(l => l.includes('PAGEERROR') || l.includes('threw:'));
	if (!booted && !failed)
		await new Promise(r => setTimeout(r, 500));
}

if (failed || !booted) {
    console.error('[ai-test] FAILURE: Did not boot successfully.');
    await browser.close();
    process.exit(1);
}

console.log('[ai-test] Boot complete. AI Autoplay is handling the game. Waiting for League Table to appear (game end)...');

const gameDeadline = Date.now() + timeoutMs;
let gameEnded = false;
while (Date.now() < gameDeadline && !gameEnded) {
    // Check if the league table is visible
    const displayStyle = await page.evaluate(() => document.getElementById('league-table')?.style?.display);
    if (displayStyle === 'flex') {
        gameEnded = true;
    } else {
        await new Promise(r => setTimeout(r, 2000));
    }
}

if (gameEnded) {
    console.log('[ai-test] GAME ENDED! Extracting League Table data...');
    const tableData = await page.evaluate(() => {
        const rows = document.querySelectorAll('#league-stats tbody tr');
        return Array.from(rows).map(row => {
            const cells = row.querySelectorAll('td');
            return {
                result: cells[0].innerText,
                score: cells[1].innerText,
                unitsKilled: cells[2].innerText,
                unitsLost: cells[3].innerText,
                buildingsDestroyed: cells[4].innerText,
                buildingsLost: cells[5].innerText
            };
        });
    });

    console.log('[ai-test] League Table Contents:');
    console.table(tableData);
    
    // Also grab localstorage
    const ls = await page.evaluate(() => localStorage.getItem('lunar_red_alert_league'));
    console.log('[ai-test] Raw localStorage data:', ls);

    console.log('[ai-test] SUCCESS: Game played out, AI controlled, League populated.');
    await browser.close();
    process.exit(0);
} else {
    console.error('[ai-test] FAILURE: Game did not end before timeout.');
    await page.screenshot({ path: 'ai-test-timeout.png' });
    await browser.close();
    process.exit(1);
}
