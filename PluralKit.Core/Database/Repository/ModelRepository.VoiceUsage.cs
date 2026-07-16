using Dapper;

namespace PluralKit.Core;

public partial class ModelRepository
{
    // Half-life of the time-decayed usage score: a voice not used for this many days has its
    // accumulated score halved. Keeps "most-used" reflecting recent trends rather than lifetime
    // totals, so an abandoned old favourite fades and a recently-hammered voice climbs.
    private const double VoiceUsageHalfLifeDays = 14.0;
    private static readonly double VoiceUsageDecayLambda = Math.Log(2) / (VoiceUsageHalfLifeDays * 86400.0);

    // Record that a Discord user just sent a TTS message with a given voice. Bumps the raw count and
    // folds the decayed prior score plus 1 into the new score (exponential decay applied over the
    // gap since last use). Raw SQL because the update references the target row's own columns.
    public Task IncrementVoiceUsage(ulong userId, string voiceId)
        => _db.Execute(conn => conn.ExecuteAsync(
            "insert into tts_voice_usage (uid, voice_id, count, score, last_used) "
          + "values (@uid, @voiceId, 1, 1, now()) "
          + "on conflict (uid, voice_id) do update set "
          + "count = tts_voice_usage.count + 1, "
          + "score = tts_voice_usage.score "
          + "        * exp(-@lambda * extract(epoch from (now() - tts_voice_usage.last_used))) + 1, "
          + "last_used = now()",
            new { uid = userId, voiceId, lambda = VoiceUsageDecayLambda }));

    // The voice ids a user leans on most right now, highest first. Orders by the score decayed to
    // the present moment (score * e^(-lambda * age)) so voices go stale even without a fresh use.
    public Task<IEnumerable<string>> GetMostUsedVoices(ulong userId, int limit)
        => _db.Execute(conn => conn.QueryAsync<string>(
            "select voice_id from tts_voice_usage where uid = @uid "
          + "order by score * exp(-@lambda * extract(epoch from (now() - last_used))) desc, last_used desc "
          + "limit @limit",
            new { uid = userId, lambda = VoiceUsageDecayLambda, limit }));
}