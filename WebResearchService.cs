using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MersiNova
{
    public class WebSourceItem
    {
        public string Title { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
    }

    public class WebResearchResult
    {
        public bool Found { get; set; }
        public string Query { get; set; } = "";
        public string CleanQuery { get; set; } = "";
        public List<WebSourceItem> Sources { get; set; } = new();
        public List<string> GoogleRelatedTopics { get; set; } = new();
        public string FullMarkdown { get; set; } = "";
        public string SpokenSummary { get; set; } = "";
    }

    public static class WebResearchService
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        static WebResearchService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        }

        // ====================================================================
        // ANA ARAŞTIRMA METODU: GOOGLE VE WEB VERİ TABANLARINI TARA
        // ====================================================================
        public static async Task<WebResearchResult> ResearchAsync(string userMessage, QueryAnalysis? analysis = null)
        {
            var result = new WebResearchResult
            {
                Query = userMessage,
                CleanQuery = CleanSearchQuery(userMessage)
            };

            // Eğer sorgu temizlendikten sonra boş veya çok kısaysa
            if (string.IsNullOrWhiteSpace(result.CleanQuery) || result.CleanQuery.Length < 2)
            {
                result.Found = false;
                result.FullMarkdown =
                    "🔍 **MersiNova Araştırma & Veri Tabanı Taraması:**\n\n" +
                    "Google ve web veri tabanlarında araştırma yapabilmek için lütfen araştırılacak konuyu veya soruyu açıkça yazınız.\n\n" +
                    "❌ **Bu konuda bilgim yok.**";
                result.SpokenSummary = "Arama yapabilmek için lütfen araştırmak istediğiniz konuyu açıkça belirtiniz. Bu konuda bilgim yok.";
                return result;
            }

            // 1. Eşzamanlı arama görevleri: DuckDuckGo Web + Türkçe Wikipedia + Google Suggest
            var ddgTask = SearchDuckDuckGoLiteAsync(result.CleanQuery);
            var wikiTask = SearchWikipediaAsync(result.CleanQuery);
            var googleSuggestTask = SearchGoogleSuggestionsAsync(result.CleanQuery);

            try
            {
                await Task.WhenAll(ddgTask, wikiTask, googleSuggestTask);
            }
            catch
            {
                // Ağ hatası veya zaman aşımı durumunda kısmi sonuçlarla devam edilir
            }

            var ddgResults = await ddgTask;
            var wikiResults = await wikiTask;
            result.GoogleRelatedTopics = await googleSuggestTask;

            // 2. Kaynakları birleştir ve tekilleştir
            var combinedSources = new List<WebSourceItem>();

            // Önce varsa doğrudan Wikipedia ansiklopedik kaynağı ekle
            if (wikiResults != null && wikiResults.Count > 0)
            {
                combinedSources.AddRange(wikiResults);
            }

            // Sonra DuckDuckGo web sonuçlarını ekle
            if (ddgResults != null)
            {
                foreach (var item in ddgResults)
                {
                    if (!combinedSources.Any(s => s.Domain.Equals(item.Domain, StringComparison.OrdinalIgnoreCase) ||
                                                 s.Title.Equals(item.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        combinedSources.Add(item);
                    }
                }
            }

            // 3. İLGİLİLİK FİLTRESİ (RELEVANCE VALIDATION)
            // Kullanıcının sorgusundaki anahtar kelimeleri kaynaklarda ara.
            // Eğer sorgunun hiçbir kelimesi kaynaklarda geçmiyorsa (uydurma kelimeler, rastgele harfler),
            // veya arama motoru hiçbir sonuç döndürmediyse: "Bu konuda bilgim yok" denir.
            var searchTokens = TokenizeQuery(result.CleanQuery);
            var validSources = new List<WebSourceItem>();

            foreach (var src in combinedSources)
            {
                string combinedText = (src.Title + " " + src.Snippet).ToLowerInvariant();
                bool matches = searchTokens.Any(token => combinedText.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (matches)
                {
                    validSources.Add(src);
                }
            }

            result.Sources = validSources.Take(4).ToList();

            // 4. EĞER HİÇBİR KAYNAK BULUNAMADIYSA VEYA DOĞRULANAMADIYSA
            if (result.Sources.Count == 0)
            {
                result.Found = false;
                result.FullMarkdown =
                    "🔍 **MersiNova Araştırma & Veri Tabanı Taraması:**\n\n" +
                    $"Google ve çevrimiçi web veri tabanlarında **\"{result.Query}\"** konusu incelendi.\n\n" +
                    "❌ **Bu konuda bilgim yok.**\n\n" +
                    "• **Sebep:** Yapılan internet ve kaynak taramasında bu konuyla ilgili doğrulanmış, güvenilir bir literatür veya kayıt bulunamadı.\n" +
                    "• **Öneri:** Lütfen sorunuzu farklı anahtar kelimelerle, bitki/hastalık adını belirterek veya detaylandırarak tekrar sorunuz.";

                result.SpokenSummary = $"Google ve veri tabanı araştırmasında \"{result.CleanQuery}\" hakkında doğrulanmış bir kaynak bulunamadı. Bu konuda bilgim yok.";
                return result;
            }

            // 5. KAYNAKLAR BULUNDU: ÖZETİ VE DETAYLI YANITI SENTEZLE
            result.Found = true;
            BuildSynthesizedResponse(result, analysis);

            return result;
        }

        // ====================================================================
        // YANIT METNİ OLUŞTURMA (KAYNAKLAR, BİLGİLER VE ZİRAİ REÇETE)
        // ====================================================================
        private static void BuildSynthesizedResponse(WebResearchResult r, QueryAnalysis? analysis)
        {
            // A. Başlık & Analiz Özeti
            string keywordsStr = analysis != null && analysis.ExtractedKeywords.Count > 0
                ? string.Join(", ", analysis.ExtractedKeywords)
                : r.CleanQuery;

            string header =
                $"🔍 **MersiNova Veri Tabanı & Google Web Araştırması:**\n" +
                $"📌 **Konu / Sorgu:** \"{r.CleanQuery}\" | 🏷️ **Anahtar Kelimeler:** {keywordsStr}\n\n";

            // B. İncelenen Kaynaklar Listesi
            string sourcesSection = "🌐 **İncelenen ve Doğrulanan Kaynaklar:**\n";
            int idx = 1;
            foreach (var src in r.Sources)
            {
                sourcesSection += $"• **[{idx}] {src.Title}** — *{src.Domain}*\n";
                if (!string.IsNullOrWhiteSpace(src.Url) && src.Url.StartsWith("http"))
                {
                    sourcesSection += $"  🔗 [Kaynağı İncele]({src.Url})\n";
                }
                idx++;
            }
            sourcesSection += "\n";

            // C. Çıkan Bilgiler ve Sentez
            string findingsSection = "📚 **Konu Hakkında Çıkan Bilgiler ve Analiz:**\n\n";

            // Kaynak snippet'lerinden en bilgilendirici ve temiz paragrafları derle
            var paragraphs = new List<string>();
            foreach (var src in r.Sources)
            {
                if (!string.IsNullOrWhiteSpace(src.Snippet) && src.Snippet.Length > 25)
                {
                    string cleanSnippet = src.Snippet.Trim();
                    if (!paragraphs.Any(p => p.Contains(cleanSnippet.Substring(0, Math.Min(30, cleanSnippet.Length)))))
                    {
                        paragraphs.Add($"• **{src.Domain} Verisi:** {cleanSnippet}");
                    }
                }
            }

            if (paragraphs.Count > 0)
            {
                findingsSection += string.Join("\n\n", paragraphs) + "\n\n";
            }
            else
            {
                findingsSection += $"Araştırılan \"{r.CleanQuery}\" konusu doğrulanmış kaynaklarda incelenmiş olup güncel literatür verileri yukarıdaki kaynaklarda mevcuttur.\n\n";
            }

            // D. Google'dan İlgili / Benzer Konular (Varsa)
            if (r.GoogleRelatedTopics.Count > 0)
            {
                findingsSection += "🔎 **Google Veri Tabanında En Çok Aranan İlgili Konular:**\n" +
                                   string.Join(" • ", r.GoogleRelatedTopics.Take(4).Select(t => $"*{t}*")) + "\n\n";
            }

            // E. Eğer Zirai veya Meyve Konusuysa: MersiNova Uzman Zirai Bilgisini Ekle
            string agriAdviceSection = "";
            if (analysis != null && analysis.DetectedCategory != "Genel Danışmanlık" && analysis.DetectedCategory != "Asistan Karşılama & Sohbet")
            {
                // AgriAiKnowledgeEngine'deki doğrulanmış ruhsatlı ilaç/gübre ve dozaj bilgisini al
                var (agriText, _) = AgriAiKnowledgeEngine.AnswerQuery(analysis, null);

                // Analiz başlığını ayıklayıp sadece tavsiye gövdesini al
                string adviceBody = agriText;
                if (adviceBody.Contains("\n\n"))
                {
                    int firstDoubleBreak = adviceBody.IndexOf("\n\n");
                    adviceBody = adviceBody.Substring(firstDoubleBreak + 2);
                }

                agriAdviceSection =
                    "🌱 **MersiNova Zirai & Pratik Uygulama Tavsiyesi:**\n\n" +
                    adviceBody;
            }
            else
            {
                agriAdviceSection =
                    "💡 **MersiNova Değerlendirmesi:**\n" +
                    "Yukarıdaki kaynak verileri güncel web taramasından elde edilmiştir. Bahçenizde somut bir hastalık, kuruma veya yaprak dökümü belirtisi varsa bitki adıyla birlikte belirtirseniz ruhsatlı ilaç ve dozaj reçetesini detaylıca sunabilirim.";
            }

            r.FullMarkdown = header + sourcesSection + findingsSection + agriAdviceSection;

            // Spoken seslendirme metni: Kısa, akıcı, net Türkçe
            string firstSnippet = r.Sources.FirstOrDefault()?.Snippet ?? "";
            if (firstSnippet.Length > 160)
            {
                firstSnippet = firstSnippet.Substring(0, 160) + "...";
            }

            r.SpokenSummary = $"Google ve web veritabanında yaptığım araştırmaya göre, {r.CleanQuery} konusu incelenmiştir. Kaynaklarda çıkan özet bilgileri ve önerilen uygulamaları ekranda detaylıca listeledim.";
        }

        // ====================================================================
        // 1. DUCKDUCKGO LITE SEARCH (WEB & GÜNCEL KAYNAKLAR)
        // ====================================================================
        private static async Task<List<WebSourceItem>> SearchDuckDuckGoLiteAsync(string query)
        {
            var list = new List<WebSourceItem>();
            try
            {
                using var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("q", query),
                    new KeyValuePair<string, string>("kl", "tr-tr")
                });

                var response = await _http.PostAsync("https://lite.duckduckgo.com/lite/", form);
                if (!response.IsSuccessStatusCode) return list;

                string html = await response.Content.ReadAsStringAsync();

                // Regex ile arama başlıkları, linkler ve snippet'leri yakala
                var titleMatches = Regex.Matches(html, @"<a rel=""nofollow"" href=""(?<url>[^""]+)"" class='result-link'>(?<title>.*?)</a>", RegexOptions.Singleline);
                var snippetMatches = Regex.Matches(html, @"<td class='result-snippet'>(?<snippet>.*?)</td>", RegexOptions.Singleline);

                int count = Math.Min(titleMatches.Count, snippetMatches.Count);
                for (int i = 0; i < count && list.Count < 5; i++)
                {
                    string rawUrl = titleMatches[i].Groups["url"].Value;
                    string rawTitle = titleMatches[i].Groups["title"].Value;
                    string rawSnippet = snippetMatches[i].Groups["snippet"].Value;

                    string cleanTitle = CleanHtml(rawTitle);
                    string cleanSnippet = CleanHtml(rawSnippet);

                    // URL ayıklama
                    string cleanUrl = rawUrl;
                    if (cleanUrl.Contains("uddg="))
                    {
                        var match = Regex.Match(cleanUrl, @"uddg=([^&]+)");
                        if (match.Success)
                        {
                            cleanUrl = WebUtility.UrlDecode(match.Groups[1].Value);
                        }
                    }

                    string domain = "Web Kaynağı";
                    if (Uri.TryCreate(cleanUrl, UriKind.Absolute, out var uri))
                    {
                        domain = uri.Host.Replace("www.", "");
                    }

                    // Genel anlamsız izleme linklerini veya kargo sitelerini atla
                    if (domain.Contains("17track") || domain.Contains("track.global") || domain.Contains("ups.com"))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(cleanTitle) && !string.IsNullOrWhiteSpace(cleanSnippet))
                    {
                        list.Add(new WebSourceItem
                        {
                            Title = cleanTitle,
                            Url = cleanUrl,
                            Domain = domain,
                            Snippet = cleanSnippet
                        });
                    }
                }
            }
            catch
            {
                // Hata durumunda boş liste döner
            }

            return list;
        }

        // ====================================================================
        // 2. TÜRKÇE WIKIPEDIA API (ANSİKLOPEDİK VE BİLİMSEL VERİTABANI)
        // ====================================================================
        private static async Task<List<WebSourceItem>> SearchWikipediaAsync(string query)
        {
            var list = new List<WebSourceItem>();
            try
            {
                string searchUrl = $"https://tr.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&utf8=1";
                var response = await _http.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode) return list;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("query", out var queryElem) ||
                    !queryElem.TryGetProperty("search", out var searchElem))
                {
                    return list;
                }

                foreach (var item in searchElem.EnumerateArray().Take(2))
                {
                    string title = item.GetProperty("title").GetString() ?? "";
                    string snippetHtml = item.GetProperty("snippet").GetString() ?? "";
                    string cleanSnippet = CleanHtml(snippetHtml);

                    if (string.IsNullOrWhiteSpace(title)) continue;

                    // Wikipedia sayfa özetini REST API'den çek (daha detaylı ve temiz metin)
                    string articleUrl = $"https://tr.wikipedia.org/wiki/{Uri.EscapeDataString(title)}";
                    string detailedExtract = await FetchWikipediaExtractAsync(title);

                    list.Add(new WebSourceItem
                    {
                        Title = $"{title} (Vikipedi Ansiklopedi)",
                        Domain = "tr.wikipedia.org",
                        Url = articleUrl,
                        Snippet = !string.IsNullOrWhiteSpace(detailedExtract) ? detailedExtract : cleanSnippet
                    });
                }
            }
            catch
            {
                // Hata durumunda sessizce devam
            }

            return list;
        }

        private static async Task<string> FetchWikipediaExtractAsync(string title)
        {
            try
            {
                string summaryUrl = $"https://tr.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}";
                var res = await _http.GetAsync(summaryUrl);
                if (!res.IsSuccessStatusCode) return "";

                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("extract", out var extractElem))
                {
                    return extractElem.GetString() ?? "";
                }
            }
            catch
            {
            }
            return "";
        }

        // ====================================================================
        // 3. GOOGLE SUGGEST API (GOOGLE VERİTABANI İLGİLİ ARAMA BAŞLIKLARI)
        // ====================================================================
        private static async Task<List<string>> SearchGoogleSuggestionsAsync(string query)
        {
            var suggestions = new List<string>();
            try
            {
                string url = $"https://suggestqueries.google.com/complete/search?client=chrome&q={Uri.EscapeDataString(query)}";
                var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode) return suggestions;

                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                // Google suggest cevabı: ["query", ["sug1", "sug2", ...], ...]
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() >= 2)
                {
                    var sugArray = doc.RootElement[1];
                    if (sugArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var sug in sugArray.EnumerateArray().Take(5))
                        {
                            string text = sug.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(text) && !text.Equals(query, StringComparison.OrdinalIgnoreCase))
                            {
                                suggestions.Add(text);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return suggestions;
        }

        // ====================================================================
        // YARDIMCI METOTLAR: TEMİZLEME VE METİN İŞLEME
        // ====================================================================
        private static string CleanSearchQuery(string raw)
        {
            string clean = raw.Trim();

            // Sık kullanılan sohbet veya arama ön eklerini/arka eklerini kaldır
            string[] patterns =
            {
                @"^(lütfen|bana|acaba)\s+",
                @"\s+(hakkında bilgi ver|hakkında bilgi|nedir|nelerdir|ne demek|nasıl yapılır|araştır|araştırır mısın|araştırırmısın|söyler misin|anlatır mısın|google'dan bak|internetten bak)$",
                @"^(google'da|internette|web'de)\s+araştır\s+",
                @"\?+$"
            };

            foreach (var pat in patterns)
            {
                clean = Regex.Replace(clean, pat, "", RegexOptions.IgnoreCase).Trim();
            }

            return clean;
        }

        private static List<string> TokenizeQuery(string query)
        {
            string lower = query.ToLowerInvariant();
            var matches = Regex.Matches(lower, @"\b[\p{L}\p{Nd}]{3,}\b");

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "için", "olan", "nasıl", "nedir", "zaman", "kadar", "hakkında", "bana", "anlat",
                "araştır", "google", "bilgi", "gibi", "veya", "daha", "bunu", "şunu", "diye"
            };

            var tokens = new List<string>();
            foreach (Match m in matches)
            {
                if (!stopWords.Contains(m.Value))
                {
                    tokens.Add(m.Value);
                }
            }

            if (tokens.Count == 0 && matches.Count > 0)
            {
                tokens.Add(matches[0].Value);
            }

            return tokens;
        }

        private static string CleanHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            string stripped = Regex.Replace(html, @"<[^>]+>", " ");
            string decoded = WebUtility.HtmlDecode(stripped);
            return Regex.Replace(decoded, @"\s+", " ").Trim();
        }
    }
}
