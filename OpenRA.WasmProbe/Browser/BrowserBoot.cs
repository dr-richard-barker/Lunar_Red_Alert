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

namespace OpenRA.WasmProbe
{
	// Shared browser boot defaults, applied right after InitializeSettings:
	// - Register the log channels desktop's Game.Initialize registers, with
	//   null sinks (engine code logs to "perf"/"debug"/... unconditionally;
	//   an unregistered channel throws). File logs are pointless in MEMFS.
	// - Force Windowed mode: the default PseudoFullscreen derives its size
	//   from display enumeration, which the browser platform reports as 0x0.
	internal static class BrowserBoot
	{
		public static void ApplyDefaults()
		{
			foreach (var channel in new[] { "perf", "debug", "server", "sound", "graphics", "geoip", "nat", "client" })
				Log.AddChannel(channel, null);

			Game.Settings.Graphics.Mode = WindowMode.Windowed;
		}
	}
}
