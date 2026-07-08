-- database version 54
-- per-user TTS voice usage, powering recency-weighted most-used ordering (e.g. /tts autocomplete)

create table if not exists tts_voice_usage (
    uid       bigint not null,
    voice_id  text   not null,
    count     int    not null default 0,           -- raw lifetime uses (analytics / tiebreak)
    score     double precision not null default 0, -- time-decayed usage score, drives ordering
    last_used timestamptz not null default now(),
    primary key (uid, voice_id)
);

-- The table is tiny per user (<= one row per voice used), so ordering by the decayed score
-- expression is a cheap in-memory sort; a plain uid index covers the lookup.
create index if not exists tts_voice_usage_uid_idx on tts_voice_usage (uid);

update info set schema_version = 54;
