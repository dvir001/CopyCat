using System.Diagnostics;
using System.Text.Json;

using Myriad.Rest.Types;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public sealed record GeneratedVoiceClip(MultipartFile File, string TempPath): IDisposable
{
    public void Dispose()
    {
        try
        {
            File.Data.Dispose();
        }
        catch
        {
            // ignore stream disposal errors
        }

        try
        {
            if (global::System.IO.File.Exists(TempPath))
                global::System.IO.File.Delete(TempPath);
        }
        catch
        {
            // ignore temp-file cleanup errors
        }
    }
}

// A voice the bot can speak with: its id (also the model filename stem) and the display name
// shown in the /tts autocomplete and the reply picker.
internal readonly record struct VoiceDefinition(string Id, string Name);

public class TtsVoiceService
{
    private readonly ILogger _logger;
    private HashSet<string> _availableVoiceIds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<VoiceDefinition> _catalog = Array.Empty<VoiceDefinition>();

    // The voices discovered on disk, in filesystem (id-sorted) order. Populated by LoadCatalog().
    internal IReadOnlyList<VoiceDefinition> Catalog => _catalog;

    // A background timer re-scans the voices directory on this cadence, so a new voice or an edited
    // voice-names.json takes effect without a restart. The scan is a cheap directory read.
    private static readonly TimeSpan CatalogRefreshInterval = TimeSpan.FromSeconds(5);
    private readonly Timer _refreshTimer;

    public TtsVoiceService(ILogger logger)
    {
        _logger = logger.ForContext<TtsVoiceService>();
        // Periodically re-scan the voices directory in the background so voices/name changes on
        // disk take effect without a restart. The initial load is done synchronously at startup
        // (LoadCatalog from Init); this just keeps it fresh afterward.
        _refreshTimer = new Timer(_ =>
        {
            try { LoadCatalog(); }
            catch (Exception e) { _logger.Warning(e, "TTS catalog refresh failed"); }
        }, null, CatalogRefreshInterval, CatalogRefreshInterval);
    }

    /// <summary>
    /// Builds the voice catalog by scanning the filesystem — every Piper model
    /// (<c>{id}.onnx</c>) present in the voices directory, plus the special python-bridge
    /// voices whose vendor libraries are installed — and caches it along with availability.
    /// Display names resolve in order: (1) an optional drop-in <c>voice-names.json</c> override
    /// file, else (2) the model's <c>{id}.onnx.json</c> metadata, else (3) a label derived from
    /// the id. Call once at bot startup; adding a voice is then just dropping its files on disk
    /// (a restart re-scans) — no recompile.
    /// </summary>
    public void LoadCatalog()
    {
        var overrides = LoadNameOverrides();

        var voices = new List<VoiceDefinition>();
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Piper voices: one entry per {id}.onnx model file on disk.
        foreach (var dir in PiperVoiceDirectories())
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.onnx"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(id) || !available.Add(id))
                    continue;
                voices.Add(new VoiceDefinition(id, ResolveVoiceName(id, overrides, file)));
            }
        }

        // Special python-bridge voices (morshu / cabal): only when their vendor library is present.
        var scriptPath = TryResolveScriptPath();
        var scriptDir = scriptPath != null ? Path.GetDirectoryName(scriptPath) : null;
        if (scriptDir != null)
            foreach (var (id, vendor) in SpecialVoices)
                if (Directory.Exists(Path.Combine(scriptDir, "vendors", vendor)) && available.Add(id))
                    voices.Add(new VoiceDefinition(id, ResolveVoiceName(id, overrides, null)));

        // Present in a stable filesystem order — Directory enumeration order isn't guaranteed,
        // so sort by id (the order `ls` shows).
        voices.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        var previousCount = _catalog.Count;
        _catalog = voices;
        _availableVoiceIds = available;

        // Log at Information only when the set actually changes (startup, or a voice added/removed);
        // the periodic refresh is otherwise quiet so it doesn't spam the log every few seconds.
        if (voices.Count != previousCount)
            _logger.Information("TTS catalog: {Count} voice(s) on disk ({Overrides} name override(s))",
                voices.Count, overrides.Count);
        else
            _logger.Debug("TTS catalog refreshed: {Count} voice(s)", voices.Count);

        if (voices.Count == 0)
            _logger.Warning("No TTS voices found — is the piper directory populated?");
    }

    public bool IsVoiceAvailable(string voiceId) =>
        _availableVoiceIds.Contains(voiceId);

    // The non-Piper voices, generated via the python bridge; each is listed only if its vendor
    // library directory (Tools/Tts/vendors/<Vendor>) exists.
    private static readonly (string Id, string Vendor)[] SpecialVoices =
    {
        ("morshu", "MorshuTalk"),
        ("cabal", "TiberianSunCABAL-Talk"),
    };

    private static IEnumerable<string> PiperVoiceDirectories()
    {
        var candidates = new[]
        {
            "/app/piper",
            Path.Combine(AppContext.BaseDirectory, "piper"),
        };
        return candidates.Where(Directory.Exists).Distinct();
    }

    // Display-name resolution: a drop-in override file wins, then the model's sidecar metadata,
    // then a label derived from the id. Quality (medium/high/low) is intentionally omitted — it
    // isn't useful to end users.
    private static string ResolveVoiceName(string id, IReadOnlyDictionary<string, string> overrides, string? onnxPath)
    {
        if (overrides.TryGetValue(id, out var overridden) && !string.IsNullOrWhiteSpace(overridden))
            return overridden;

        var fromMeta = onnxPath != null ? NameFromOnnxMetadata(onnxPath) : null;
        if (!string.IsNullOrWhiteSpace(fromMeta))
            return fromMeta!;

        return NameFromId(id);
    }

    // Piper ids look like "<locale>-<name>-<quality>", where <name> may contain hyphens, e.g.
    // "en_US-alex-jones-medium" -> "Alex Jones (en_US)". Ids without that shape -> just titleized.
    private static string NameFromId(string id)
    {
        var parts = id.Split('-');
        if (parts.Length < 2)
            return Titleize(id);
        if (parts.Length == 2)
            return $"{Titleize(parts[1])} ({parts[0]})";

        var locale = parts[0];
        var name = string.Join('-', parts[1..^1]); // everything between locale and the quality suffix
        return $"{Titleize(name)} ({locale})";
    }

    // Builds a name from the Piper sidecar {id}.onnx.json when it carries metadata:
    // "<Titleized dataset> (<language>)". Returns null if the file is missing, unreadable, or has
    // no dataset field (as with our own exported voices, whose configs are stripped).
    private static string? NameFromOnnxMetadata(string onnxPath)
    {
        try
        {
            var jsonPath = onnxPath + ".json";
            if (!File.Exists(jsonPath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("dataset", out var datasetEl)
                || datasetEl.ValueKind != JsonValueKind.String)
                return null;
            var dataset = datasetEl.GetString();
            if (string.IsNullOrWhiteSpace(dataset))
                return null;

            string? language = null;
            if (root.TryGetProperty("language", out var langEl) && langEl.ValueKind == JsonValueKind.Object
                && langEl.TryGetProperty("name_english", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                language = nameEl.GetString();

            return string.IsNullOrWhiteSpace(language)
                ? Titleize(dataset!)
                : $"{Titleize(dataset!)} ({language})";
        }
        catch
        {
            return null; // metadata is best-effort; fall back to the id
        }
    }

    // Loads the optional drop-in name override file (a JSON object of id -> display name). Looked
    // up via COPYCAT_TTS_VOICE_NAMES, then voice-names.json in the voices directory or app dir.
    // A missing or malformed file yields no overrides (best effort — never breaks voice loading).
    private static IReadOnlyDictionary<string, string> LoadNameOverrides()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var path = FindNameOverridesFile();
            if (path == null)
                return result;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var name = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        result[prop.Name] = name!;
                }
        }
        catch
        {
            // best effort — a malformed override file must never break voice loading
        }
        return result;
    }

    private static string? FindNameOverridesFile()
    {
        var candidates = new List<string>();

        var env = Environment.GetEnvironmentVariable("COPYCAT_TTS_VOICE_NAMES");
        if (!string.IsNullOrWhiteSpace(env))
            candidates.Add(env);

        foreach (var dir in PiperVoiceDirectories())
            candidates.Add(Path.Combine(dir, "voice-names.json"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "voice-names.json"));

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string Titleize(string s)
    {
        var words = s.Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    public async Task<GeneratedVoiceClip> GenerateClip(string voiceId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new PKError("Text cannot be empty.");
        if (string.IsNullOrWhiteSpace(voiceId))
            throw new PKError("Voice cannot be empty.");

        var voice = voiceId.Trim();
        var voiceLower = voice.ToLowerInvariant();

        // morshu and cabal are generated by the Python bridge using their vendor libraries.
        // All other voices are handled directly by the Piper TTS binary (no Python needed).
        if (voiceLower is "morshu" or "cabal")
            return await GeneratePythonClip(voice, text);

        return await GeneratePiperClip(voice, text);
    }

    // ── Piper TTS (neural voices, called directly from C#) ───────────────────

    private async Task<GeneratedVoiceClip> GeneratePiperClip(string voiceId, string text)
    {
        var modelPath = FindPiperModel(voiceId)
            ?? throw new PKError($"Voice model '{voiceId}' not found.");

        var outputPath = Path.Combine(Path.GetTempPath(), $"copycat-tts-{Guid.NewGuid():N}.wav");

        // piper-tts is installed as a Python package; invoke it via `python3 -m piper`.
        var pythonOverride = Environment.GetEnvironmentVariable("COPYCAT_TTS_PYTHON");
        var python = string.IsNullOrWhiteSpace(pythonOverride) ? "python3" : pythonOverride;

        var psi = new ProcessStartInfo
        {
            FileName = python,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("piper");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(modelPath);
        psi.ArgumentList.Add("--output_file");
        psi.ArgumentList.Add(outputPath);

        using var process = Process.Start(psi)
            ?? throw new PKError("Could not start piper-tts process.");

        // Piper can clip the final phoneme on short/plain text; add a terminal
        // pause marker so synthesis has room to finish naturally.
        await process.StandardInput.WriteAsync(PreparePiperInput(text));
        process.StandardInput.Close();

        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.Warning("piper-tts exited with code {ExitCode}: {Stderr}", process.ExitCode, stderr);
            throw new PKError($"Voice generation failed (piper exit {process.ExitCode}).");
        }

        if (!global::System.IO.File.Exists(outputPath))
            throw new PKError("piper-tts did not create an output file.");

        // Add a tiny silence tail so Discord playback doesn't feel abruptly cut.
        TryAppendWaveSilence(outputPath, 120);

        // Compress WAV -> Ogg/Opus before upload (~10x smaller than raw PCM, no perceptible
        // quality loss for speech; Opus is Discord's native voice codec and plays inline).
        outputPath = CompressToOggOpus(outputPath);

        var stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var safeVoice = string.Concat(voiceId.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_')).ToLowerInvariant();
        var multipart = new MultipartFile($"tts-{safeVoice}{Path.GetExtension(outputPath)}", stream, "Generated voice clip", null, null, false);
        return new GeneratedVoiceClip(multipart, outputPath);
    }

    // Re-encode a generated WAV to Ogg/Opus (32 kbps) via ffmpeg before it's uploaded to Discord.
    // Cuts a typical clip ~10x with no perceptible loss for speech. Best-effort: if ffmpeg is
    // missing or the conversion fails for any reason, the original WAV path is returned unchanged.
    private string CompressToOggOpus(string wavPath)
    {
        try
        {
            var oggPath = Path.ChangeExtension(wavPath, ".ogg");
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[] { "-nostdin", "-y", "-i", wavPath, "-c:a", "libopus", "-b:a", "32k", oggPath })
                psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi);
            if (proc == null)
                return wavPath;

            _ = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 0 && global::System.IO.File.Exists(oggPath) && new FileInfo(oggPath).Length > 0)
            {
                try { global::System.IO.File.Delete(wavPath); } catch { /* leave temp file for GC */ }
                return oggPath;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Opus compression failed; falling back to raw WAV");
        }

        return wavPath;
    }

    private static string PreparePiperInput(string text)
    {
        var normalized = text.TrimEnd();
        if (normalized.Length == 0)
            return " .";

        if (normalized.EndsWith('.'))
            return normalized;

        return normalized + " .";
    }

    private static void TryAppendWaveSilence(string path, int silenceMs)
    {
        if (silenceMs <= 0)
            return;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            using var br = new BinaryReader(fs);

            if (fs.Length < 44)
                return;

            fs.Seek(22, SeekOrigin.Begin);
            var channels = br.ReadInt16();
            var sampleRate = br.ReadInt32();
            fs.Seek(34, SeekOrigin.Begin);
            var bitsPerSample = br.ReadInt16();

            if (channels <= 0 || sampleRate <= 0 || bitsPerSample <= 0)
                return;

            var bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample <= 0)
                return;

            var silenceBytes = sampleRate * channels * bytesPerSample * silenceMs / 1000;
            if (silenceBytes <= 0)
                return;

            fs.Seek(0, SeekOrigin.End);
            fs.Write(new byte[silenceBytes]);

            var newFileSize = checked((uint)fs.Length - 8);
            var newDataSize = checked((uint)fs.Length - 44);

            fs.Seek(4, SeekOrigin.Begin);
            fs.Write(BitConverter.GetBytes(newFileSize));
            fs.Seek(40, SeekOrigin.Begin);
            fs.Write(BitConverter.GetBytes(newDataSize));
        }
        catch
        {
            // Best effort only: if header parsing fails, keep the synthesized file.
        }
    }

    private static string? FindPiperBinary() => null; // kept for compatibility; piper is now invoked via python -m piper

    private static string? FindPiperModel(string voiceId)
    {
        var candidates = new[]
        {
            $"/app/piper/{voiceId}.onnx",
            Path.Combine(AppContext.BaseDirectory, "piper", $"{voiceId}.onnx"),
        };
        return candidates.FirstOrDefault(global::System.IO.File.Exists);
    }

    // ── Python bridge (morshu / cabal) ───────────────────────────────────────

    private async Task<GeneratedVoiceClip> GeneratePythonClip(string voiceId, string text)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"copycat-tts-{Guid.NewGuid():N}.wav");
        var scriptPath = ResolveScriptPath();

        var pythonOverride = Environment.GetEnvironmentVariable("COPYCAT_TTS_PYTHON");
        var pythonCandidates = string.IsNullOrWhiteSpace(pythonOverride)
            ? new[] { "python", "python3" }
            : new[] { pythonOverride };

        var lastError = string.Empty;
        foreach (var python in pythonCandidates)
        {
            var result = await RunPythonGenerator(python, scriptPath, voiceId, text, outputPath);
            if (result.Success)
            {
                if (!global::System.IO.File.Exists(outputPath))
                    throw new PKError("TTS generation completed but no output file was created.");

                var finalPath = CompressToOggOpus(outputPath);
                var stream = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var safeVoice = string.Concat(voiceId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')).ToLowerInvariant();
                var multipart = new MultipartFile($"tts-{safeVoice}{Path.GetExtension(finalPath)}", stream, "Generated voice clip", null, null, false);
                return new GeneratedVoiceClip(multipart, finalPath);
            }
            lastError = result.Error;
        }

        throw new PKError($"TTS generation failed. {lastError}");
    }

    private async Task<(bool Success, string Error)> RunPythonGenerator(string pythonCommand, string scriptPath,
                                                                         string voice, string text, string outputPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonCommand,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? AppContext.BaseDirectory
            };
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("--voice");
            psi.ArgumentList.Add(voice);
            psi.ArgumentList.Add("--text");
            psi.ArgumentList.Add(text);
            psi.ArgumentList.Add("--out");
            psi.ArgumentList.Add(outputPath);

            using var process = Process.Start(psi);
            if (process == null)
                return (false, $"Could not start {pythonCommand}.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0)
                return (true, string.Empty);

            _logger.Warning("TTS generator exited with code {ExitCode}. stdout={Stdout} stderr={Stderr}",
                process.ExitCode, stdout, stderr);
            var trimmed = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (trimmed.Length > 400) trimmed = trimmed[..400] + "...";
            return (false, $"Generator ({pythonCommand}) exited with code {process.ExitCode}. {trimmed}.");
        }
        catch (Exception e)
        {
            return (false, $"Failed to launch generator ({pythonCommand}): {e.Message}");
        }
    }

    private static string ResolveScriptPath()
    {
        var path = TryResolveScriptPath();
        if (path != null)
            return path;

        throw new PKError("Missing TTS generator script at Tools/Tts/tts_generate.py.");
    }

    private static string? TryResolveScriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "Tts", "tts_generate.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "PluralKit.Bot", "Tools", "Tts", "tts_generate.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "Tools", "Tts", "tts_generate.py")
        };

        return candidates.FirstOrDefault(global::System.IO.File.Exists);
    }
}