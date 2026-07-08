# TTS Integration

This folder hosts the runtime bridge for the /tts command.

## Voices

- `Morshu`: clone https://github.com/n0spaces/MorshuTalk into `vendors/MorshuTalk`
- `CABAL`: clone https://github.com/ASp2004/TiberianSunCABAL-Talk into `vendors/TiberianSunCABAL-Talk`
- Extended NotSoBot-style voices (eg `TIKTOK_EN_US_MALE_01`, `SAPI_MICROSOFT_EN_MALE_SAM`) are provider-backed.

## Setup

1. Install Python and dependencies required by each voice repo.
2. Ensure these folders exist:
   - `PluralKit.Bot/Tools/Tts/vendors/MorshuTalk`
   - `PluralKit.Bot/Tools/Tts/vendors/TiberianSunCABAL-Talk`
3. If Python is not available as `python` or `python3`, set env var `COPYCAT_TTS_PYTHON` to the Python executable path.
4. For extended voices, set `COPYCAT_TTS_API_URL` to a compatible TTS endpoint.
5. If required by your provider, set `COPYCAT_TTS_API_TOKEN`.

## Notes

- CABAL voice also requires its own audio asset setup/config as documented in that repository.
- The /tts command generates a temporary wav file and uploads it with the webhook message.
- The Python bridge expects provider responses as raw audio bytes, or JSON with `file.value` base64 (NotSoBot-style).
