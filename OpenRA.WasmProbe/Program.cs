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
using System.Linq;

namespace OpenRA.WasmProbe
{
	// Phase W1 probe: does OpenRA.Game EXECUTE inside the .NET wasm runtime?
	// (W0 proved it publishes; this runs under Node in CI — see wasm-port.yml.)
	// Exercises the pure-managed data layer the real engine boots through:
	// fixed-point world geometry and the MiniYAML rules parser. No platform,
	// no rendering, no filesystem. See WASM-PORT-PLAN.md.
	public static class Program
	{
		const string Rules =
			"E1:\n" +
			"\tOxygen:\n" +
			"\t\tCapacity: 5000\n" +
			"\t\tDrainRate: 3\n" +
			"\tDamagedByVacuum:\n" +
			"\t\tDamage: 350\n" +
			"DOME:\n" +
			"\tProximityExternalCondition@LIFESUPPORT:\n" +
			"\t\tCondition: pressurised\n" +
			"\t\tRange: 8c0\n";

		public static void Main()
		{
			// 1. Fixed-point world math (the sim's foundation).
			var a = new WPos(1024, 2048, 0);
			var b = new WPos(4096, 0, 512);
			Console.WriteLine($"[probe] OpenRA.Game assembly: {typeof(WPos).Assembly.GetName().Name}");
			Console.WriteLine($"[probe] WPos distance math OK: {(b - a).Length}");
			Console.WriteLine($"[probe] WAngle from facing 128: {WAngle.FromFacing(128)}");

			// 2. MiniYAML: parse a SpaceAge-flavoured rules snippet and walk it.
			var nodes = MiniYaml.FromString(Rules, "probe-rules").ToList();
			var e1 = nodes.First(n => n.Key == "E1");
			var oxygen = e1.Value.Nodes.First(n => n.Key == "Oxygen");
			var capacity = oxygen.Value.Nodes.First(n => n.Key == "Capacity").Value.Value;
			var dome = nodes.First(n => n.Key == "DOME");
			var lifeSupport = dome.Value.Nodes.First(n => n.Key.StartsWith("ProximityExternalCondition", StringComparison.Ordinal));
			Console.WriteLine($"[probe] MiniYAML parsed {nodes.Count} actors; E1.Oxygen.Capacity={capacity}");
			Console.WriteLine($"[probe] MiniYAML trait key with suffix: {lifeSupport.Key}");

			if (capacity != "5000")
				throw new InvalidOperationException("MiniYAML round-trip mismatch");

			Console.WriteLine("[probe] SUCCESS: OpenRA.Game core executes under the .NET wasm runtime");
		}
	}
}
