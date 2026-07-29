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

using System.Runtime.InteropServices.JavaScript;

namespace OpenRA.Platforms.Browser
{
	// Phase W4b: Web Audio interop. Handles are ints into a JS-side table,
	// matching the GL binding design. JS half lives in wwwroot/main.js.
	// Note: browsers gate audio on a user gesture — until the first
	// click/keypress the AudioContext stays 'suspended': node graphs build
	// and schedule correctly but are inaudible, which is also what makes the
	// engine structurally testable in headless CI.
	internal static partial class WebAudio
	{
		[JSImport("audioInit", "webgl.js")]
		internal static partial int Init();

		[JSImport("audioState", "webgl.js")]
		internal static partial string State();

		[JSImport("audioCreateBuffer", "webgl.js")]
		internal static partial int CreateBuffer(int channels, int sampleBits, int sampleRate, byte[] pcm);

		[JSImport("audioPlay", "webgl.js")]
		internal static partial int Play(int buffer, bool loop, double volume, double pan);

		[JSImport("audioSetVolume", "webgl.js")]
		internal static partial void SetVolume(int source, double volume);

		[JSImport("audioSetPan", "webgl.js")]
		internal static partial void SetPan(int source, double pan);

		[JSImport("audioPause", "webgl.js")]
		internal static partial void Pause(int source, bool paused);

		[JSImport("audioStop", "webgl.js")]
		internal static partial void Stop(int source);

		[JSImport("audioComplete", "webgl.js")]
		internal static partial bool Complete(int source);

		[JSImport("audioSeekSeconds", "webgl.js")]
		internal static partial double SeekSeconds(int source);

		[JSImport("audioMasterVolume", "webgl.js")]
		internal static partial void MasterVolume(double volume);
	}
}
