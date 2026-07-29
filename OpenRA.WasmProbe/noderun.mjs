// Phase W1 harness: execute the published browser-wasm bundle under Node.
// Run from inside the published wwwroot directory (where _framework/ lives):
//   node noderun.mjs
// Registers a stub 'webgl.js' module reporting no DOM, so the probe runs its
// W1 checks and skips the W2 WebGL path (which needs a real browser — that
// runs under Playwright/Chromium in CI). See WASM-PORT-PLAN.md.
import { readFile } from 'node:fs/promises';
import { dotnet } from './_framework/dotnet.js';

try {
	const { setModuleImports } = await dotnet.create();
	setModuleImports('webgl.js', {
		hasDocument: () => false,
		// W3a under Node: same contract as the browser's fetch(), served from disk
		// (cwd is the published wwwroot, where CI stages probe-data/).
		fetchText: async url => readFile(url, 'utf8'),
	});
	await dotnet.run();
	console.log('[noderun] wasm runtime exited cleanly');
} catch (err) {
	console.error('[noderun] FAILED:', err);
	process.exit(1);
}
