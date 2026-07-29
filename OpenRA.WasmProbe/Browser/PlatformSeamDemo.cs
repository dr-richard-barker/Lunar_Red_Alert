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
using OpenRA.Primitives;

namespace OpenRA.WasmProbe
{
	// Phase W3h milestone: the engine's own platform factory selects the
	// browser backend. Game.CreatePlatform("Browser") must resolve the
	// bundled OpenRA.Platforms.Browser assembly by name (no loose DLL in
	// wasm), find its single IPlatform implementation, and construct it —
	// the exact path Game.Initialize walks with Settings.Game.Platform.
	internal static class PlatformSeamDemo
	{
		public static void Run()
		{
			var platform = Game.CreatePlatform("Browser");
			var typeName = platform.GetType().FullName;
			if (typeName != "OpenRA.Platforms.Browser.BrowserPlatform")
				throw new InvalidOperationException($"CreatePlatform returned unexpected type {typeName}");

			using var window = platform.CreateWindow(
				new Size(256, 256), WindowMode.Windowed, 1f, 8192, 8192, 0, GLProfile.Embedded);

			Console.WriteLine($"[probe] W3h SUCCESS: Game.CreatePlatform(\"Browser\") -> {typeName}, window up ({window.Context.GLVersion})");
		}
	}
}
