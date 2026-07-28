// Phase W1 harness: execute the published browser-wasm bundle under Node.
// Run from inside the published wwwroot directory (where _framework/ lives):
//   node ../path/to/noderun.mjs
// The probe touches no browser APIs, so Node's wasm runtime is a faithful host
// for this stage. Browser-in-the-loop testing (Playwright) arrives with the
// rendering work in Phase W2+. See WASM-PORT-PLAN.md.
import { dotnet } from './_framework/dotnet.js';

try {
	await dotnet.run();
	console.log('[noderun] wasm runtime exited cleanly');
} catch (err) {
	console.error('[noderun] FAILED:', err);
	process.exit(1);
}
