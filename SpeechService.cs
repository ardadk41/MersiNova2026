using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace MersiNova
{
    public class ElevenLabsVoiceItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public override string ToString() => $"{Name} — {Description}";
    }

    public class SpeechService : IDisposable
    {
        private readonly SpeechSynthesizer _synth;
        private readonly MediaPlayer _mediaPlayer;
        private readonly HttpClient _http;
        private readonly string _cacheDir;
        private readonly Queue<string> _audioQueue = new();
        private bool _isPlayingQueue = false;

        // Auto-selected optimal ElevenLabs voice: Rachel (Most natural, fluent, and warm Turkish in Multilingual v2)
        public const string OptimalVoiceId = "21m00Tcm4TlvDq8ikWAM";
        public string ElevenLabsVoiceId { get; set; } = OptimalVoiceId;

        public static readonly List<ElevenLabsVoiceItem> AvailableVoices = new()
        {
            new ElevenLabsVoiceItem { Id = "21m00Tcm4TlvDq8ikWAM", Name = "Rachel", Description = "Doğal, Akıcı & Samimi (Önerilen Varsayılan)" }
        };

        public bool IsPlayingQueue => _isPlayingQueue;
        public bool IsVoiceEnabled { get; set; } = true;
        public event Action<bool>? SpeakingStateChanged;

        public string? ElevenLabsApiKey { get; private set; }
        public bool IsElevenLabsConfigured => !string.IsNullOrWhiteSpace(ElevenLabsApiKey);

        public string ActiveVoiceName => IsElevenLabsConfigured
            ? "MersiNova Yapay Zeka Sesi (ElevenLabs Multilingual v2)"
            : "MersiNova Doğal Ses Motoru";

        public SpeechService()
        {
            _synth = new SpeechSynthesizer();
            _mediaPlayer = new MediaPlayer();
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.DefaultRequestHeaders.Add("User-Agent", "MersiNova-AgriAI/2.0");

            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MersiNova", "AudioCache");
            Directory.CreateDirectory(appData);
            _cacheDir = appData;

            // Automatically load ElevenLabs configuration from file or environment
            LoadElevenLabsConfig();

            // Configure local SAPI fallback
            try
            {
                var installedVoices = _synth.GetInstalledVoices();
                var female = installedVoices.FirstOrDefault(v => v.Enabled && v.VoiceInfo.Gender == VoiceGender.Female);
                if (female != null)
                {
                    _synth.SelectVoice(female.VoiceInfo.Name);
                }
                else
                {
                    _synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
                }
                _synth.Rate = 2; // Energetic, brisk and fluent speech
                _synth.Volume = 100;
            }
            catch { }

            _mediaPlayer.MediaOpened += (s, e) =>
            {
                try
                {
                    _mediaPlayer.SpeedRatio = 1.20; // 1.20x playback speed for brisk and fluent speech
                }
                catch { }
            };
            _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            _mediaPlayer.MediaFailed += (s, e) =>
            {
                _isPlayingQueue = false;
                SpeakingStateChanged?.Invoke(false);
            };
        }

        public void LoadElevenLabsConfig()
        {
            try
            {
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "elevenlabs_key.txt"),
                    Path.Combine(Directory.GetCurrentDirectory(), "elevenlabs_key.txt"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MersiNova", "elevenlabs_key.txt")
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        string content = File.ReadAllText(path).Trim();
                        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            string trimmed = line.Trim();
                            if (!trimmed.StartsWith("#") && !trimmed.StartsWith("//") && trimmed.Length > 10)
                            {
                                ElevenLabsApiKey = trimmed;
                                break;
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(ElevenLabsApiKey)) break;
                    }
                }

                if (string.IsNullOrWhiteSpace(ElevenLabsApiKey))
                {
                    string? envKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
                    if (!string.IsNullOrWhiteSpace(envKey))
                    {
                        ElevenLabsApiKey = envKey.Trim();
                    }
                }
            }
            catch { }
        }

        public void SaveElevenLabsConfig(string apiKey, string? voiceId = null)
        {
            ElevenLabsApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            if (!string.IsNullOrWhiteSpace(voiceId))
            {
                ElevenLabsVoiceId = voiceId.Trim();
            }

            try
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MersiNova");
                Directory.CreateDirectory(appDataDir);

                string keyPath = Path.Combine(appDataDir, "elevenlabs_key.txt");
                File.WriteAllText(keyPath, ElevenLabsApiKey ?? "");

                try
                {
                    string baseKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "elevenlabs_key.txt");
                    File.WriteAllText(baseKeyPath, ElevenLabsApiKey ?? "");
                }
                catch { }
            }
            catch { }
        }

        public async void SpeakAsync(string markdownText)
        {
            if (!IsVoiceEnabled) return;

            string clean = CleanMarkdownForSpeech(markdownText);
            if (string.IsNullOrWhiteSpace(clean)) return;

            Stop();

            // 1. ElevenLabs Multilingual V2 (Auto-selected, human nuance, energetic speed)
            if (IsElevenLabsConfigured)
            {
                bool elevenSuccess = await SpeakWithElevenLabsAsync(clean);
                if (elevenSuccess) return;
            }

            // 2. High-definition single-stream accelerated neural engine (Smooth & brisk speech)
            try
            {
                var sentences = SplitIntoChunks(clean, 175);
                var audioFiles = new List<string>();

                foreach (var chunk in sentences)
                {
                    string safeChunk = chunk.Trim();
                    if (string.IsNullOrEmpty(safeChunk)) continue;

                    string hash = Math.Abs(safeChunk.GetHashCode()).ToString();
                    string filePath = Path.Combine(_cacheDir, $"tr_fast_v2_chunk_{hash}.mp3");

                    if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                    {
                        string encoded = Uri.EscapeDataString(safeChunk);
                        string url = $"https://translate.google.com/translate_tts?ie=UTF-8&tl=tr&client=tw-ob&q={encoded}";
                        byte[] bytes = await _http.GetByteArrayAsync(url);
                        if (bytes.Length > 0)
                        {
                            await File.WriteAllBytesAsync(filePath, bytes);
                        }
                    }

                    if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
                    {
                        audioFiles.Add(filePath);
                    }
                }

                if (audioFiles.Count > 0)
                {
                    string combinedHash = Math.Abs(clean.GetHashCode()).ToString();
                    string combinedPath = Path.Combine(_cacheDir, $"tr_fast_v2_{combinedHash}.mp3");

                    if (!File.Exists(combinedPath) || new FileInfo(combinedPath).Length == 0)
                    {
                        var allBytes = new List<byte>();
                        foreach (var f in audioFiles)
                        {
                            allBytes.AddRange(await File.ReadAllBytesAsync(f));
                        }
                        await File.WriteAllBytesAsync(combinedPath, allBytes.ToArray());
                    }

                    PlayFileOnUI(combinedPath, 1.25);
                    return;
                }
            }
            catch
            {
                // Fallback to local SAPI SpeechSynthesizer
            }

            // 3. Fallback SAPI speech (Brisk rate = 2)
            _ = Task.Run(() =>
            {
                try
                {
                    SpeakingStateChanged?.Invoke(true);
                    _synth.Speak(clean);
                }
                catch { }
                finally
                {
                    SpeakingStateChanged?.Invoke(false);
                }
            });
        }

        private async Task<bool> SpeakWithElevenLabsAsync(string cleanText)
        {
            if (string.IsNullOrWhiteSpace(ElevenLabsApiKey)) return false;

            try
            {
                string hash = Math.Abs((cleanText + "_" + ElevenLabsVoiceId).GetHashCode()).ToString();
                string cachedFile = Path.Combine(_cacheDir, $"eleven_{ElevenLabsVoiceId}_{hash}.mp3");

                if (File.Exists(cachedFile) && new FileInfo(cachedFile).Length > 512)
                {
                    PlayFileOnUI(cachedFile, 1.25);
                    return true;
                }

                string url = $"https://api.elevenlabs.io/v1/text-to-speech/{ElevenLabsVoiceId}?output_format=mp3_44100_128";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("xi-api-key", ElevenLabsApiKey.Trim());
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("audio/mpeg"));

                // Optimal voice settings for maximum Turkish fluency, stability and lively pace
                var payload = new
                {
                    text = cleanText,
                    model_id = "eleven_multilingual_v2",
                    voice_settings = new
                    {
                        stability = 0.50,
                        similarity_boost = 0.80,
                        style = 0.0,
                        use_speaker_boost = true,
                        speed = 1.18
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    byte[] audioBytes = await response.Content.ReadAsByteArrayAsync();
                    if (audioBytes.Length > 512)
                    {
                        await File.WriteAllBytesAsync(cachedFile, audioBytes);
                        PlayFileOnUI(cachedFile, 1.25);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpeechService] ElevenLabs error: {ex.Message}");
            }

            return false;
        }

        private void PlayFileOnUI(string path, double speed = 1.25)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => PlaySeamlessAudio(path, speed));
            }
            else
            {
                PlaySeamlessAudio(path, speed);
            }
        }

        private void PlaySeamlessAudio(string path, double speed = 1.25)
        {
            try
            {
                StopInternal();
                _isPlayingQueue = true;

                _mediaPlayer.Open(new Uri(path, UriKind.Absolute));
                _mediaPlayer.Play();
                try { _mediaPlayer.SpeedRatio = speed; } catch { }

                SpeakingStateChanged?.Invoke(true);
            }
            catch
            {
                _isPlayingQueue = false;
                SpeakingStateChanged?.Invoke(false);
            }
        }

        private void MediaPlayer_MediaEnded(object? sender, EventArgs e)
        {
            _isPlayingQueue = false;
            SpeakingStateChanged?.Invoke(false);
        }

        public void Stop()
        {
            try
            {
                _audioQueue.Clear();
                _isPlayingQueue = false;
                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(StopInternal);
                }
                else
                {
                    StopInternal();
                }
                _synth.SpeakAsyncCancelAll();
                SpeakingStateChanged?.Invoke(false);
            }
            catch { }
        }

        private void StopInternal()
        {
            try
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Close();
            }
            catch { }
        }

        private static List<string> SplitIntoChunks(string text, int maxLen = 175)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            // Split ONLY by sentence terminals (. ! ? newline), NEVER by commas, preserving natural prosody
            var sentences = Regex.Split(text, @"(?<=[.!?\n])\s+");

            string current = "";
            foreach (var s in sentences)
            {
                string trimmed = s.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (current.Length + trimmed.Length + 1 <= maxLen)
                {
                    current = string.IsNullOrEmpty(current) ? trimmed : current + " " + trimmed;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        list.Add(current.Trim());
                    }

                    if (trimmed.Length > maxLen)
                    {
                        var commaParts = Regex.Split(trimmed, @"(?<=[,;])\s+");
                        string subCurrent = "";
                        foreach (var part in commaParts)
                        {
                            if (subCurrent.Length + part.Length + 1 <= maxLen)
                            {
                                subCurrent = string.IsNullOrEmpty(subCurrent) ? part : subCurrent + " " + part;
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(subCurrent)) list.Add(subCurrent.Trim());
                                subCurrent = part;
                            }
                        }
                        current = subCurrent;
                    }
                    else
                    {
                        current = trimmed;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                list.Add(current.Trim());
            }

            return list;
        }

        public static string CleanMarkdownForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string clean = text;

            // Expand abbreviations and units into natural fluent Turkish words
            clean = Regex.Replace(clean, @"(\d+)\s*°C", "$1 derece");
            clean = Regex.Replace(clean, @"(\d+)\s*km/s", "$1 kilometre");
            clean = Regex.Replace(clean, @"%(\d+)", "yüzde $1");
            clean = Regex.Replace(clean, @"(\d+)\s*mm", "$1 milimetre");
            clean = Regex.Replace(clean, @"(\d+)\s*kg", "$1 kilogram");
            clean = Regex.Replace(clean, @"(\d+)\s*da\b", "$1 dekar");
            clean = Regex.Replace(clean, @"(\d+)\s*ha\b", "$1 hektar");
            clean = Regex.Replace(clean, @"[₺]\s*(\d+)", "$1 lira");
            clean = Regex.Replace(clean, @"(\d+)\s*TL", "$1 lira");
            clean = Regex.Replace(clean, @"\bSQL\b", "veritabanı");
            clean = Regex.Replace(clean, @"\bYOLOv8\b", "Yolo ve sekiz");
            clean = Regex.Replace(clean, @"\bAI\b", "yapay zeka");

            // Strip Markdown and punctuation noise that causes stutter
            clean = Regex.Replace(clean, @"[*_~`#>]", " ");
            clean = Regex.Replace(clean, @"[\[\]\(\)\{\}]", " ");
            clean = Regex.Replace(clean, @"^[\s]*[•\-\*]\s+", "", RegexOptions.Multiline);
            clean = Regex.Replace(clean, @"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u27BF]", "");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            return clean;
        }

        public void Dispose()
        {
            try
            {
                Stop();
                _mediaPlayer.Close();
                _synth.Dispose();
                _http.Dispose();
            }
            catch { }
        }
    }
}
