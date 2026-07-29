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
using System.Runtime.InteropServices.JavaScript;

namespace OpenRA.WasmProbe
{
	// Phase W3: the game-loop inversion primitive. The browser owns the frame,
	// so instead of OpenRA's blocking Game.Run loop, JS requestAnimationFrame
	// calls INTO managed code each frame via this [JSExport]. Proving this
	// tick-from-rAF path is the structural prerequisite for running the real
	// engine loop in a browser (see WASM-PORT-PLAN.md, "Game-loop inversion").
	public static partial class FrameLoop
	{
		const int TargetFrames = 60;
		static int frames;
		static double firstTimestamp = -1;
		static double lastTimestamp = -1;

		// Called by main.js from requestAnimationFrame. Returns false to stop.
		[JSExport]
		public static bool OnFrame(double timestamp)
		{
			if (lastTimestamp >= 0 && timestamp <= lastTimestamp)
				throw new InvalidOperationException($"rAF timestamp not monotonic: {timestamp} after {lastTimestamp}");

			if (firstTimestamp < 0)
				firstTimestamp = timestamp;

			lastTimestamp = timestamp;
			frames++;

			if (frames < TargetFrames)
				return true;

			var avgDt = (lastTimestamp - firstTimestamp) / (TargetFrames - 1);
			Console.WriteLine($"[probe] W3b SUCCESS: {TargetFrames} rAF-driven managed frames, avg dt {avgDt:F1} ms");
			return false;
		}
	}
}
