#!/bin/sh
# Entry point for the .NET images. When COPYCAT_TTS_DOWNLOAD_VOICES=1 (set on the
# bot service, which mounts the piper volume), download any missing Piper
# voice models into the mounted folder before starting. For all other services the
# download is skipped so their ephemeral filesystem is not filled.
set -e

if [ "${COPYCAT_TTS_DOWNLOAD_VOICES:-0}" = "1" ]; then
    /app/ci/download-piper-voices.sh || echo "[entrypoint] voice download step failed, continuing" >&2
fi

exec dotnet "$@"
