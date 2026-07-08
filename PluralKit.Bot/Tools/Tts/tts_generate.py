#!/usr/bin/env python3
import argparse
import pathlib
import sys


def add_vendor_paths(script_dir: pathlib.Path) -> None:
    vendors = script_dir / "vendors"
    morshu_repo = vendors / "MorshuTalk"
    cabal_repo = vendors / "TiberianSunCABAL-Talk"

    if morshu_repo.exists():
        sys.path.insert(0, str(morshu_repo))
    if cabal_repo.exists():
        sys.path.insert(0, str(cabal_repo))


def generate_morshu(text: str, output_file: str) -> None:
    from morshutalk.morshu import Morshu

    morshu = Morshu()
    audio = morshu.load_text(text)
    if audio is False:
        raise RuntimeError("Morshu generator returned no audio")
    audio.export(output_file, format="wav")


def generate_cabal(text: str, output_file: str) -> None:
    from tibsuncabal_talk.tibsuncabal import TibSunCabal
    from pydub import AudioSegment
    from pydub.effects import normalize, low_pass_filter

    cabal = TibSunCabal()
    audio = cabal.load_text(text)
    if audio is False:
        raise RuntimeError("CABAL generator returned no audio")

    # Post-process to improve perceptual quality:
    # 1. Upsample to 44100 Hz — the source is 22050 Hz ADPCM; resampling lets
    #    the low-pass filter work in a more natural frequency range.
    audio = audio.set_frame_rate(44100).set_channels(1).set_sample_width(2)
    # 2. Low-pass at 6 kHz to reduce ADPCM compression harshness/buzz.
    audio = low_pass_filter(audio, 6000)
    # 3. Normalize to a consistent loudness.
    audio = normalize(audio, headroom=1.0)

    audio.export(output_file, format="wav")


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a TTS clip for CopyCat")
    parser.add_argument("--voice", required=True)
    parser.add_argument("--text", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    script_dir = pathlib.Path(__file__).resolve().parent
    add_vendor_paths(script_dir)

    out_path = pathlib.Path(args.out).resolve()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        voice = args.voice.strip()
        voice_lower = voice.lower()

        if voice_lower == "morshu":
            generate_morshu(args.text, str(out_path))
        elif voice_lower == "cabal":
            generate_cabal(args.text, str(out_path))
        else:
            raise RuntimeError(f"Unknown voice '{voice}'. Only 'morshu' and 'cabal' are handled by this script.")

        if not out_path.exists() or out_path.stat().st_size == 0:
            raise RuntimeError("generator did not create a valid output file")

        return 0
    except Exception as exc:
        sys.stderr.write(str(exc) + "\n")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())



def add_vendor_paths(script_dir: pathlib.Path) -> None:
    vendors = script_dir / "vendors"
    morshu_repo = vendors / "MorshuTalk"
    cabal_repo = vendors / "TiberianSunCABAL-Talk"

    if morshu_repo.exists():
        sys.path.insert(0, str(morshu_repo))
    if cabal_repo.exists():
        sys.path.insert(0, str(cabal_repo))


def generate_morshu(text: str, output_file: str) -> None:
    from morshutalk.morshu import Morshu

    morshu = Morshu()
    audio = morshu.load_text(text)
    if audio is False:
        raise RuntimeError("Morshu generator returned no audio")
    audio.export(output_file, format="wav")


def generate_cabal(text: str, output_file: str) -> None:
    from tibsuncabal_talk.tibsuncabal import TibSunCabal
    from pydub import AudioSegment
    from pydub.effects import normalize, low_pass_filter

    cabal = TibSunCabal()
    audio = cabal.load_text(text)
    if audio is False:
        raise RuntimeError("CABAL generator returned no audio")

    # Post-process to improve perceptual quality:
    # 1. Upsample to 44100 Hz — the source is 22050 Hz ADPCM; resampling lets
    #    the low-pass filter work in a more natural frequency range.
    audio = audio.set_frame_rate(44100).set_channels(1).set_sample_width(2)
    # 2. Low-pass at 6 kHz to reduce ADPCM compression harshness/buzz.
    audio = low_pass_filter(audio, 6000)
    # 3. Normalize to a consistent loudness.
    audio = normalize(audio, headroom=1.0)

    audio.export(output_file, format="wav")


def generate_remote(text: str, voice: str, output_file: str) -> None:
    base_url = os.environ.get("COPYCAT_TTS_API_URL", "").strip()
    if not base_url:
        raise RuntimeError(
            "Voice requires remote TTS provider. Set COPYCAT_TTS_API_URL for extended voices."
        )

    query = urllib.parse.urlencode({"text": text, "voice": voice})
    url = f"{base_url}?{query}" if "?" not in base_url else f"{base_url}&{query}"
    request = urllib.request.Request(url)

    token = os.environ.get("COPYCAT_TTS_API_TOKEN", "").strip()
    if token:
        request.add_header("Authorization", f"Bearer {token}")

    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            content_type = response.headers.get("Content-Type", "")
            payload = response.read()

            if "application/json" in content_type:
                data = json.loads(payload.decode("utf-8"))
                file_obj = data.get("file", {}) if isinstance(data, dict) else {}
                b64_value = file_obj.get("value")
                if not b64_value:
                    raise RuntimeError("Remote TTS returned JSON without file.value")
                audio = base64.b64decode(b64_value)
                with open(output_file, "wb") as handle:
                    handle.write(audio)
                return

            with open(output_file, "wb") as handle:
                handle.write(payload)
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="ignore")
        raise RuntimeError(f"Remote TTS HTTP {exc.code}: {body}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a TTS clip for CopyCat")
    parser.add_argument("--voice", required=True)
    parser.add_argument("--text", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    script_dir = pathlib.Path(__file__).resolve().parent
    add_vendor_paths(script_dir)

    out_path = pathlib.Path(args.out).resolve()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        voice = args.voice.strip()
        voice_lower = voice.lower()

        if voice_lower == "morshu":
            generate_morshu(args.text, str(out_path))
        elif voice_lower == "cabal":
            generate_cabal(args.text, str(out_path))
        else:
            generate_remote(args.text, voice, str(out_path))

        if not out_path.exists() or out_path.stat().st_size == 0:
            raise RuntimeError("generator did not create a valid output file")

        return 0
    except Exception as exc:
        sys.stderr.write(str(exc) + os.linesep)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
