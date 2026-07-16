# CopyCat development

CopyCat is implemented in C#. `PluralKit.Core` provides shared data and service code, `Myriad` provides the Discord gateway and REST client, and `PluralKit.Bot` contains the Discord bot. The bot process connects directly to Discord. The same executable also provides one-shot database migration and application-command registration modes.

PostgreSQL stores persistent data and Redis stores transient state. Service configuration uses environment variables; see [dotnet.md](./dotnet.md) for the available C# settings.

## Docker development

Create a `.env` file in the repository root:

```env
CLIENT_ID=your_client_id_here
BOT_TOKEN=your_bot_token_here
```

Build and run the local stack from source:

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

Register Discord application commands after changing their schema:

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm register-commands
```

## Local .NET development

Install the .NET SDK version targeted by the projects, then provide at least these settings:

```env
PluralKit__Bot__Token=your_bot_token_here
PluralKit__Bot__ClientId=your_client_id_here
PluralKit__Database=Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=pluralkit
PluralKit__RedisAddr=localhost:6379
```

Run migrations and start the bot:

```sh
COPYCAT_MIGRATE_ONLY=true dotnet run --project PluralKit.Bot
dotnet run --project PluralKit.Bot
```

## Legacy database upgrades

If upgrading from the legacy Python bot, see [LEGACYMIGRATE.md](./LEGACYMIGRATE.md).
