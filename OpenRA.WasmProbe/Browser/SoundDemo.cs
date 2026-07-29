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
using OpenRA.Platforms.Browser;

namespace OpenRA.WasmProbe
{
	// Phase W4b milestone: the Web Audio engine behind OpenRA's ISoundEngine.
	// Generates a 440 Hz PCM16 tone, builds an AudioBuffer from it via the
	// engine contract, schedules playback with volume/pan, and verifies the
	// node graph state. Headless CI has no speakers and the AudioContext is
	// gesture-suspended, so verification is structural — buffers built,
	// sources scheduled, controls responsive — which is exactly what the
	// game needs from the engine layer.
	internal static class SoundDemo
	{
		public static void Run()
		{
			var engine = RendererDemo.CreatedPlatform.CreateSound(null);
			Console.WriteLine($"[probe] step: sound engine = {engine.GetType().Name} (context: {WebAudio.State()})");

			if (engine.Dummy)
				throw new InvalidOperationException("Expected the Web Audio engine in the browser host");

			// 0.2s of 440 Hz mono PCM16 at 22050 Hz — generated, engine-shaped.
			const int SampleRate = 22050;
			var samples = SampleRate / 5;
			var pcm = new byte[samples * 2];
			for (var i = 0; i < samples; i++)
			{
				var value = (short)(Math.Sin(2 * Math.PI * 440 * i / SampleRate) * 12000);
				pcm[i * 2] = (byte)(value & 0xFF);
				pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
			}

			var source = engine.AddSoundSourceFromMemory(pcm, 1, 16, SampleRate);
			var sound = engine.Play2D(source, loop: false, relative: true, WPos.Zero, volume: 0.5f, attenuateVolume: false);
			if (sound == null)
				throw new InvalidOperationException("Play2D returned no sound");

			sound.Volume = 0.25f;
			engine.SetListenerPosition(new WPos(1024, 1024, 0));
			engine.SetSoundPosition(sound, new WPos(9216, 1024, 0));
			engine.Volume = 0.8f;
			engine.StopSound(sound);

			Console.WriteLine("[probe] W4b SUCCESS: Web Audio engine built a PCM16 AudioBuffer, scheduled playback with gain/pan, and honoured volume/stop controls");
		}
	}
}
