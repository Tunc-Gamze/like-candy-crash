using UnityEngine;

namespace Birdsong
{
    public enum SoundCue { Select, Swap, Match, Invalid, Cascade, Win, Lose, Button }
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudio : MonoBehaviour
    {
        [Tooltip("Optional clips. Missing clips are intentionally silent.")]
        public AudioClip select, swap, match, invalid, cascade, win, lose, button;
        [Range(0, 1)] public float volume = 0.6f;
        bool soundEnabled = true;
        public bool SoundEnabled
        {
            get => soundEnabled;
            set
            {
                soundEnabled = value;
                if (source == null) return;
                source.mute = !value;
                if (!value) source.Stop();
            }
        }
        AudioSource source;
        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.mute = !soundEnabled;
        }
        public void Play(SoundCue cue)
        {
            if (!SoundEnabled) return;
            AudioClip clip = null;
            switch (cue)
            {
                case SoundCue.Select: clip = select; break;
                case SoundCue.Swap: clip = swap; break;
                case SoundCue.Match: clip = match; break;
                case SoundCue.Invalid: clip = invalid; break;
                case SoundCue.Cascade: clip = cascade; break;
                case SoundCue.Win: clip = win; break;
                case SoundCue.Lose: clip = lose; break;
                case SoundCue.Button: clip = button; break;
            }
            if (clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
