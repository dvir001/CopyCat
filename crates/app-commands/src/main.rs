use twilight_model::application::command::CommandType;
use twilight_util::builder::command::{AttachmentBuilder, CommandBuilder, StringBuilder};

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let discord = twilight_http::Client::builder()
        .token(libpk::config.discord().bot_token.clone())
        .build();

    let interaction = discord.interaction(twilight_model::id::Id::new(
        libpk::config.discord().client_id.clone().get(),
    ));

    let commands = vec![
        // slash commands
        CommandBuilder::new("s", "Speak through CopyCat as yourself", CommandType::ChatInput)
            .option(StringBuilder::new("text", "Message text to send").required(true).build())
            .option(
                AttachmentBuilder::new("attachment", "Optional file to include")
                    .required(false)
                    .build(),
            )
            .option(
                StringBuilder::new("reply_to", "Optional message link to reply to")
                    .required(false)
                    .build(),
            )
            .build(),
        CommandBuilder::new("tts", "Generate a voice clip from text", CommandType::ChatInput)
            .option(StringBuilder::new("text", "Message text to speak").required(true).build())
            .option(
                StringBuilder::new("voice", "Voice preset")
                    .autocomplete(true)
                    .required(true)
                    .build(),
            )
            .option(
                AttachmentBuilder::new("attachment", "Optional file to include")
                    .required(false)
                    .build(),
            )
            .option(
                StringBuilder::new("reply_to", "Optional message link or id to reply to")
                    .required(false)
                    .build(),
            )
            .build(),

        // message commands
        // description must be empty string
        CommandBuilder::new("\u{274c} Delete message", "", CommandType::Message).build(),
        CommandBuilder::new("\u{1f4ac} Say as me", "", CommandType::Message).build(),
    ];

    interaction.set_global_commands(&commands).await?;

    Ok(())
}
