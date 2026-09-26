# Birdsong sound effects

Eight original, synthesized effects: select, button, swap, invalid, match,
cascade, win and lose. No recordings or third-party samples are used.

Reproduce them with `python tools/generate_sfx.py` from the repository root.
Format: 44.1 kHz, mono, 16-bit PCM WAV. Gentle attack/release envelopes avoid
clicks; output stays below full scale. Unity imports these short effects as PCM
with Decompress On Load to keep playback immediate.

`V1ProjectSetup.Configure` assigns them to MainScene's Birdsong / GameAudio.
The master On/Off preference mutes and stops active effects immediately.
