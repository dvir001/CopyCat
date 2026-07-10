# CopyCat

CopyCat is a Discord bot that lets you send messages and voice clips as yourself — using your own name and avatar — via Discord's webhook system.

## Commands

| Command | Description |
|---------|-------------|
| `/s` | Send a message (with optional attachment) as yourself through a webhook. Supports replying to other messages. |
| `/tts` | Generate a text-to-speech voice clip and post it as yourself. Choose from dozens of neural voices (Piper TTS) or novelty voices like Morshu and CABAL. |

### TTS voices

Voices are split into two backends:

- **Piper TTS** — neural, offline voices covering many languages. Models are downloaded to a mounted volume at container startup (`/app/piper-voices`). Only voices whose `.onnx` model file is present on disk appear in autocomplete.
- **Python bridge** — Morshu (MorshuTalk) and CABAL (TiberianSunCABAL-Talk). Require their vendor repos to be cloned into `Tools/Tts/vendors/` inside the container. Only appear in autocomplete when their vendor directory is found.

## Self-hosting

### Requirements

- Docker and Docker Compose.
- A Discord application with a bot token and client ID.

### Setup

1. Create a `.env` file in the project root:

```env
CLIENT_ID=your_client_id_here
BOT_TOKEN=your_bot_token_here
```

To enable `pk;admin` commands (raising member limits, etc.), also set:

```env
ADMIN_ROLE=your_admin_role_id_here
```

2. Build and start all services:

```sh
docker compose build
docker compose up -d
```

3. Register slash commands (run once, or after command changes):

```sh
docker compose run --rm register-commands
```

4. View logs:

```sh
docker compose logs -f bot
```

### Piper voice models

Set `COPYCAT_TTS_DOWNLOAD_VOICES=1` on the `bot` service (it is set by default in `docker-compose.yml`) to automatically download Piper voice models into `/data/piper` on the host at container startup. Only voices with a downloaded model file will appear in the `/tts` autocomplete list.

To add a custom voice model, place the `.onnx` and `.onnx.json` files in `/data/piper` on the host (mounted into the container at `/app/piper-voices`).

### CABAL audio assets

The CABAL voice requires copyright-encumbered game audio files (`.aud`) that are **not** included in the repository. Supply them by placing the files in `/data/cabal` on the host before starting the bot:

```
/data/cabal/
  01-n400.aud
  nod-02.aud
  nod-07b.aud
  ... (other .aud files from the cabal/ directory)
```

The directory is bind-mounted into the container at `/app/cabal_audio`. Without these files the CABAL voice will fail at runtime; all other voices are unaffected.

### Data storage

- **PostgreSQL** — all persistent data, stored in `/data/db` on the host.
- **Redis** — internal state and transient data.
- **Piper voices** — model files in `/data/piper` on the host (mounted into the container at `/app/piper-voices`).
- **CABAL audio** — `.aud` game audio files in `/data/cabal` on the host (mounted into the container at `/app/cabal_audio`). Must be supplied manually (see above).

## Architecture

| Service | Language | Role |
|---------|----------|------|
| `bot` | C# (.NET) | Discord bot: handles slash commands, proxying, TTS |
| `gateway` | Rust | Connects to Discord gateway and forwards events to the bot |
| `migrate` | Rust | Runs database migrations on startup |
| `register-commands` | Rust | Registers slash commands with Discord |
| `db` | PostgreSQL 17 | Persistent data store |
| `redis` | Redis | Internal state and caching |

## Development

See [dev-docs/](./dev-docs/README.md) for build instructions, local development setup, and service-specific configuration.

## License

With the exception of the Myriad library, this project is licensed under the GNU Affero General Public License, Version 3. See the [COPYING](./COPYING) file for the full license text.

Licensing information for the Myriad library can be found in [Myriad/COPYING](./Myriad/COPYING).
