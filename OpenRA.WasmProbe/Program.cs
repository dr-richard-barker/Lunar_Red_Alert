#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;

namespace OpenRA.WasmProbe
{
	// Phase W0 probe: does OpenRA.Game load inside the .NET browser-wasm runtime at all?
	// This intentionally exercises only pure-managed types (fixed-point world geometry,
	// MiniYAML) — no platform, no rendering, no filesystem. Success = the assembly
	// resolves and executes math in the browser. See WASM-PORT-PLAN.md.
	public static class Program
	{
		public static void Main()
		{
			var a = new WPos(1024, 2048, 0);
			var b = new WPos(4096, 0, 512);
			var d = (b - a).Length;
			Console.WriteLine($"[probe] OpenRA.Game assembly: {typeof(WPos).Assembly.GetName().Name}");
			Console.WriteLine($"[probe] WPos distance math OK: {d}");
			Console.WriteLine($"[probe] WAngle from facing 128: {WAngle.FromFacing(128)}");
			Console.WriteLine("[probe] SUCCESS: OpenRA.Game core executes under browser-wasm");
		}
	}
}
