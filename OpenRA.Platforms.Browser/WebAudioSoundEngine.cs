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

using System.IO;

namespace OpenRA.Platforms.Browser
{
	// Phase W4b: OpenRA's ISoundEngine over the Web Audio API. Positional
	// audio is approximated in 2D: horizontal offset from the listener maps
	// to stereo pan, distance to attenuation — adequate for an RTS camera.
	sealed class WebAudioSoundEngine : ISoundEngine
	{
		const int PanRange = 8192;          // world units to full pan
		const int AttenuationRange = 16384; // world units to half volume

		WPos listener;
		float volume = 1f;

		public SoundDevice[] AvailableDevices()
		{
			return [new SoundDevice(null, "Web Audio")];
		}

		public bool Dummy => false;

		public float Volume
		{
			get => volume;
			set
			{
				volume = value;
				WebAudio.MasterVolume(value);
			}
		}

		public ISoundSource AddSoundSourceFromMemory(byte[] data, int channels, int sampleBits, int sampleRate)
		{
			return new WebAudioSource(WebAudio.CreateBuffer(channels, sampleBits, sampleRate, data));
		}

		public ISound Play2D(ISoundSource sound, bool loop, bool relative, WPos pos, float volume, bool attenuateVolume)
		{
			if (sound is not WebAudioSource source)
				return null;

			var (pan, gain) = Spatialize(pos, relative, attenuateVolume);
			return new WebAudioSound(WebAudio.Play(source.Buffer, loop, volume * gain, pan));
		}

		public ISound Play2DStream(Stream stream, int channels, int sampleBits, int sampleRate, bool loop, bool relative, WPos pos, float volume)
		{
			// Music/speech streams: buffer fully and play as a regular source.
			// Progressive streaming is a future refinement; menu/game music
			// tracks decode in one shot acceptably.
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			var buffer = WebAudio.CreateBuffer(channels, sampleBits, sampleRate, memory.ToArray());
			var (pan, gain) = Spatialize(pos, relative, false);
			return new WebAudioSound(WebAudio.Play(buffer, loop, volume * gain, pan));
		}

		(double Pan, float Gain) Spatialize(WPos pos, bool relative, bool attenuate)
		{
			if (relative)
				return (0, 1f);

			var offset = pos - listener;
			var pan = System.Math.Clamp(offset.X / (double)PanRange, -1, 1);
			var gain = 1f;
			if (attenuate)
			{
				var distance = offset.HorizontalLength;
				gain = AttenuationRange / (float)(AttenuationRange + distance);
			}

			return (pan, gain);
		}

		public void PauseSound(ISound sound, bool paused)
		{
			if (sound is WebAudioSound s)
				WebAudio.Pause(s.Source, paused);
		}

		public void StopSound(ISound sound)
		{
			if (sound is WebAudioSound s)
				WebAudio.Stop(s.Source);
		}

		public void SetAllSoundsPaused(bool paused) { }
		public void StopAllSounds() { }

		public void SetListenerPosition(WPos position)
		{
			listener = position;
		}

		public void SetSoundVolume(float volume, ISound music, ISound video)
		{
			if (music is WebAudioSound m)
				WebAudio.SetVolume(m.Source, volume);
			if (video is WebAudioSound v)
				WebAudio.SetVolume(v.Source, volume);
		}

		public void SetSoundLooping(bool looping, ISound sound) { }

		public void SetSoundPosition(ISound sound, WPos position)
		{
			if (sound is not WebAudioSound s)
				return;

			var (pan, _) = Spatialize(position, false, false);
			WebAudio.SetPan(s.Source, pan);
		}

		public void Dispose() { }
	}

	sealed class WebAudioSource : ISoundSource
	{
		internal readonly int Buffer;

		public WebAudioSource(int buffer)
		{
			Buffer = buffer;
		}

		public void Dispose() { }
	}

	sealed class WebAudioSound : ISound
	{
		internal readonly int Source;

		public WebAudioSound(int source)
		{
			Source = source;
		}

		public float Volume
		{
			get => 1f;
			set => WebAudio.SetVolume(Source, value);
		}

		public float SeekPosition => (float)WebAudio.SeekSeconds(Source);
		public bool Complete => WebAudio.Complete(Source);
		public void SetPosition(WPos pos) { }
	}
}
