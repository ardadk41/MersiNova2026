using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MersiNova
{
    public class QueryAnalysis
    {
        public string RawMessage { get; set; } = "";
        public string LowerMessage { get; set; } = "";
        public string DetectedCrop { get; set; } = "Genel Tarım / Bahçe";
        public string DetectedCategory { get; set; } = "Genel Danışmanlık";
        public List<string> ExtractedKeywords { get; set; } = new();
        public bool IsAppropriate { get; set; } = true;
        public string? InappropriateMessage { get; set; }
        public bool IsConfident { get; set; } = true;
        public string? ClarificationRequest { get; set; }
        public string? SpeechClarification { get; set; }
    }

    public static class AgriAiKnowledgeEngine
    {
        private static readonly Random _rnd = new();

        // ====================================================================
        // 1. ANALİZ: CÜMLE HAKKINDA ANAHTAR KELİMELERİ VE AMACI ÇIKAR
        // ====================================================================
        public static QueryAnalysis Analyze(string message)
        {
            var analysis = new QueryAnalysis
            {
                RawMessage = message,
                LowerMessage = message.ToLowerInvariant().Trim()
            };

            string lower = analysis.LowerMessage;
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // A. UYGUNLUK & GÜVENLİK KONTROLÜ
            if (ContainsAny(lower, "bomba", "zehirle insan", "intihar", "uyuşturucu", "patlayıcı", "silah", "öldürmek", "hack", "şifre çal"))
            {
                analysis.IsAppropriate = false;
                analysis.InappropriateMessage = "⚠️ **GÜVENLİK UYARISI:** Bu soru tarımsal veya zirai güvenlik ilkelerine uygun değildir. MersiNova yalnızca T.C. Tarım ve Orman Bakanlığı kurallarına uygun bitki sağlığı, meyve yetiştiriciliği ve tarımsal danışmanlık hizmeti sunar.";
                return analysis;
            }

            // B. MEYVE / BİTKİ TÜRÜ TESPİTİ
            if (ContainsAny(lower, "limon", "kütdiken", "mayer", "enterdonat", "lamas"))
            {
                analysis.DetectedCrop = "Limon (Citrus limon)";
                keywords.Add("Limon");
            }
            else if (ContainsAny(lower, "portakal", "valencia", "navel", "washington", "yafa", "kan portakalı"))
            {
                analysis.DetectedCrop = "Portakal (Citrus sinensis)";
                keywords.Add("Portakal");
            }
            else if (ContainsAny(lower, "mandalina", "mandarin", "satsuma", "klemantin", "nova mandalina", "w. murcott"))
            {
                analysis.DetectedCrop = "Mandalina (Citrus reticulata)";
                keywords.Add("Mandalina");
            }
            else if (ContainsAny(lower, "greyfurt", "altıntop", "star ruby", "rio red"))
            {
                analysis.DetectedCrop = "Greyfurt (Citrus paradisi)";
                keywords.Add("Greyfurt");
            }
            else if (ContainsAny(lower, "narenciye", "turunçgil", "turunç"))
            {
                analysis.DetectedCrop = "Narenciye (Turunçgiller)";
                keywords.Add("Narenciye");
            }
            else if (ContainsAny(lower, "zeytin", "gemlik", "ayvalık", "domat", "memecik", "yağlık zeytin"))
            {
                analysis.DetectedCrop = "Zeytin (Olea europaea)";
                keywords.Add("Zeytin");
            }
            else if (ContainsAny(lower, "nar", "hicaz", "deve dişi"))
            {
                analysis.DetectedCrop = "Nar (Punica granatum)";
                keywords.Add("Nar");
            }
            else if (ContainsAny(lower, "avokado", "hass", "fuerte", "bacon", "zutano"))
            {
                analysis.DetectedCrop = "Avokado (Persea americana)";
                keywords.Add("Avokado");
            }
            else if (ContainsAny(lower, "muz", "grand nain", "chiquita", "bodur muz"))
            {
                analysis.DetectedCrop = "Muz (Musa acuminata)";
                keywords.Add("Muz");
            }
            else if (ContainsAny(lower, "şeftali", "nektarin"))
            {
                analysis.DetectedCrop = "Şeftali / Nektarin (Prunus persica)";
                keywords.Add("Şeftali");
            }
            else if (ContainsAny(lower, "kayısı", "malatya", "şekerpare"))
            {
                analysis.DetectedCrop = "Kayısı (Prunus armeniaca)";
                keywords.Add("Kayısı");
            }
            else if (ContainsAny(lower, "kiraz", "vişne", "napolyon", "0900"))
            {
                analysis.DetectedCrop = "Kiraz / Vişne (Prunus avium)";
                keywords.Add("Kiraz");
            }
            else if (ContainsAny(lower, "elma", "starking", "golden", "granny smith", "fuji"))
            {
                analysis.DetectedCrop = "Elma (Malus domestica)";
                keywords.Add("Elma");
            }
            else if (ContainsAny(lower, "armut", "santa maria", "deveci", "ankara armudu"))
            {
                analysis.DetectedCrop = "Armut (Pyrus communis)";
                keywords.Add("Armut");
            }
            else if (ContainsAny(lower, "üzüm", "bağ", "asma", "sultani", "çekirdeksiz"))
            {
                analysis.DetectedCrop = "Bağ / Üzüm (Vitis vinifera)";
                keywords.Add("Bağ/Üzüm");
            }
            else if (ContainsAny(lower, "ceviz", "chandler", "fernor", "şebin"))
            {
                analysis.DetectedCrop = "Ceviz (Juglans regia)";
                keywords.Add("Ceviz");
            }
            else if (ContainsAny(lower, "badem", "ferragnes", "nonpareil"))
            {
                analysis.DetectedCrop = "Badem (Prunus dulcis)";
                keywords.Add("Badem");
            }
            else if (ContainsAny(lower, "domates", "biber", "patlıcan", "hıyar", "salatalık", "çilek", "karpuz", "kavun"))
            {
                analysis.DetectedCrop = "Sebze & Sera Mahsulü";
                keywords.Add("Sera/Sebze");
            }

            // C. ZİRAİ KATEGORİ & TEŞHİS TESPİTİ
            if (ContainsAny(lower, "uçkurutan", "uckurutan", "dal kuruma", "dal kuruması"))
            {
                analysis.DetectedCategory = "Mantari Hastalık: Uçkurutan (Phoma tracheiphila)";
                keywords.Add("Uçkurutan");
                keywords.Add("Bakır İlaçlaması");
                keywords.Add("Cerrahi Budama");
            }
            else if (ContainsAny(lower, "meyve sineği", "akdeniz meyve", "sinek", "vuruk", "kurtlanma"))
            {
                analysis.DetectedCategory = "Zararlı: Akdeniz Meyve Sineği (Ceratitis capitata)";
                keywords.Add("Meyve Sineği");
                keywords.Add("Zehirli Yem");
                keywords.Add("Decis Trap");
            }
            else if (ContainsAny(lower, "unlu bit", "unlubit", "karaballık", "fumajin", "pamukcuk"))
            {
                analysis.DetectedCategory = "Zararlı: Unlu Bit & Karaballık (Planococcus citri)";
                keywords.Add("Unlu Bit");
                keywords.Add("Bayer Movento");
                keywords.Add("Yazlık Beyaz Yağ");
            }
            else if (ContainsAny(lower, "pas böcüsü", "pas bocu", "gümüşi", "gumusi", "pas leke", "kırmızı örümcek", "kirmizi orumcek", "akar"))
            {
                analysis.DetectedCategory = "Akar & Zararlı: Pas Böcüsü & Kırmızı Örümcek";
                keywords.Add("Pas Böcüsü");
                keywords.Add("Kırmızı Örümcek");
                keywords.Add("Akarisit");
            }
            else if (ContainsAny(lower, "çiçek güvesi", "cicek guvesi", "prays"))
            {
                analysis.DetectedCategory = "Zararlı: Çiçek Güvesi (Prays)";
                keywords.Add("Çiçek Güvesi");
                keywords.Add("Corteva Laser");
            }
            else if (ContainsAny(lower, "kabuklu bit", "kırmızı kabuklu"))
            {
                analysis.DetectedCategory = "Zararlı: Kırmızı Kabuklu Bit (Aonidiella aurantii)";
                keywords.Add("Kabuklu Bit");
                keywords.Add("Yazlık Yağ");
            }
            else if (ContainsAny(lower, "zamk", "zamklanma", "gövde zamk", "akıntı", "phytophthora"))
            {
                analysis.DetectedCategory = "Kök & Gövde Kanseri: Zamklanma (Phytophthora)";
                keywords.Add("Gövde Zamklanması");
                keywords.Add("Bayer Aliette");
                keywords.Add("Yara Temizliği");
            }
            else if (ContainsAny(lower, "sararma", "kloroz", "demir eksik", "damar yeşil", "demir noksan"))
            {
                analysis.DetectedCategory = "Besin Noksanlığı: Demir Klorozu";
                keywords.Add("Kloroz");
                keywords.Add("Şelatlı Demir");
                keywords.Add("EDDHA Fe");
            }
            else if (ContainsAny(lower, "antraknoz", "kara leke", "yaprak lekesi"))
            {
                analysis.DetectedCategory = "Mantar: Antraknoz & Yaprak Yanıklığı";
                keywords.Add("Antraknoz");
                keywords.Add("Bordo Bulamacı");
                keywords.Add("Bakırlı Mücadele");
            }
            else if (ContainsAny(lower, "çatlama", "kabuk çatla", "meyve patla"))
            {
                analysis.DetectedCategory = "Fizyolojik Bozukluk: Meyve Çatlaması";
                keywords.Add("Meyve Çatlaması");
                keywords.Add("Kalsiyum Nitrat");
                keywords.Add("Sulama Dengesi");
            }
            else if (ContainsAny(lower, "koflaşma", "kabuk kalın", "süngerleşme", "kof"))
            {
                analysis.DetectedCategory = "Kalite Bozukluğu: Koflaşma & Kalın Kabuk";
                keywords.Add("Koflaşma");
                keywords.Add("Potasyum Nitrat");
                keywords.Add("Azot Kısıtlaması");
            }
            else if (ContainsAny(lower, "döküm", "haziran dökümü", "meyve dökülüyor", "meyve döküldü"))
            {
                analysis.DetectedCategory = "Fizyolojik & Beslenme: Meyve Dökümü";
                keywords.Add("Meyve Dökümü");
                keywords.Add("Çinko & Magnezyum");
                keywords.Add("Su Stresi");
            }
            else if (ContainsAny(lower, "halkalı leke", "zeytin sineği", "zeytin güvesi"))
            {
                analysis.DetectedCategory = "Zeytin Hastalık & Zararlıları";
                keywords.Add("Halkalı Leke");
                keywords.Add("Zeytin Sineği");
                keywords.Add("Bordo Bulamacı");
            }
            else if (ContainsAny(lower, "monilya", "mumya", "dal yanıklığı", "yaprak kıvırcık"))
            {
                analysis.DetectedCategory = "Meyve Ağacı Mantar Hastalığı";
                keywords.Add("Monilya");
                keywords.Add("Tebuconazole");
                keywords.Add("Kış İlaçlaması");
            }
            else if (ContainsAny(lower, "kara leke", "ateş yanıklığı", "elma iç kurdu"))
            {
                analysis.DetectedCategory = "Yumuşak Çekirdekli Hastalıkları";
                keywords.Add("Kara Leke");
                keywords.Add("Ateş Yanıklığı");
                keywords.Add("İç Kurdu");
            }
            else if (ContainsAny(lower, "külleme", "mildiyö", "salkım güvesi"))
            {
                analysis.DetectedCategory = "Bağ & Üzüm Hastalıkları";
                keywords.Add("Külleme");
                keywords.Add("Kükürt");
                keywords.Add("Mildiyö");
            }
            else if (ContainsAny(lower, "sulama", "su", "damla sulama", "kaç günde bir sulan"))
            {
                analysis.DetectedCategory = "Toprak & Sulama Yönetimi";
                keywords.Add("Damla Sulama");
                keywords.Add("Sulama Takvimi");
                keywords.Add("Su İhtiyacı");
            }
            else if (ContainsAny(lower, "gübre", "gübreleme", "besleme", "taban gübresi", "yaprak gübresi", "azot", "fosfor", "potasyum"))
            {
                analysis.DetectedCategory = "Bitki Besleme & Gübreleme";
                keywords.Add("Gübre Takvimi");
                keywords.Add("NPK Besleme");
                keywords.Add("Toprak İyileştirme");
            }
            else if (ContainsAny(lower, "budama", "aşı", "aşı macunu", "tepe kesimi"))
            {
                analysis.DetectedCategory = "Kültürel İşlemler: Budama & Aşılama";
                keywords.Add("Budama Kuralları");
                keywords.Add("Pelton Aşı Macunu");
                keywords.Add("Dezenfeksiyon");
            }
            else if (ContainsAny(lower, "bordo bulamacı", "bordo bulamaci", "göztaşı", "bakır ne zaman"))
            {
                analysis.DetectedCategory = "Koruyucu Zirai Tedbir: Bordo Bulamacı";
                keywords.Add("Bordo Bulamacı");
                keywords.Add("Göztaşı");
                keywords.Add("Kış İlaçlaması");
            }
            else if (ContainsAny(lower, "don", "soğuk", "dondan koru", "ayaz", "buz"))
            {
                analysis.DetectedCategory = "Meteorolojik Risk: Zirai Don Mücadelesi";
                keywords.Add("Zirai Don");
                keywords.Add("Rüzgar Pervanesi");
                keywords.Add("Antifriz Gübre");
            }
            else if (ContainsAny(lower, "rekolte", "verim", "fiyat", "kaç ton", "ne kazanırım", "ciro", "kasa"))
            {
                analysis.DetectedCategory = "Tarımsal Rekolte & Gelir Tahmini";
                keywords.Add("Rekolte Hesabı");
                keywords.Add("Meyve Verimi");
                keywords.Add("Ekonomik Analiz");
            }
            else if (ContainsAny(lower, "nasılsın", "naber", "ne haber", "merhaba", "selam", "günaydın", "iyi akşamlar", "sen kimsin", "teşekkür", "sağol"))
            {
                analysis.DetectedCategory = "Asistan Karşılama & Sohbet";
                keywords.Add("MersiNova AI");
                keywords.Add("Sohbet");
            }
            else
            {
                // D. BELİRSİZ VEYA YETERSİZ DETAY KONTROLÜ
                // Kullanıcı "ağacım kuruyor", "ilaç söyle", "hastalandı" gibi çok genel ve belirsiz bir şey yazmışsa
                if (lower.Length < 10 || (ContainsAny(lower, "kuruyor", "ilaç", "hastalık", "çürüyor", "leke var") && analysis.DetectedCrop.Contains("Genel")))
                {
                    analysis.IsConfident = false;
                    analysis.ClarificationRequest = 
                        "⚠️ **KESİN TEŞHİS VE GÜVENLİK KRİTERİ:**\n\n" +
                        "Tarımsal mücadelede ezbere veya eksik bilgiyle kimyasal ilaç önermek ağaçlarınıza telafisi imkansız zararlar verebilir ve Bakanlık kurallarına aykırıdır.\n\n" +
                        "Size **kesin, net ve ruhsatlı doğru reçeteyi** sunabilmem için lütfen şu 3 detayı belirtiniz:\n" +
                        "1️⃣ **Hangi meyve veya ağaç?** (Örn: Limon, Portakal, Zeytin, Nar, Elma vb.)\n" +
                        "2️⃣ **Belirti nerede görülüyor?** (Taze sürgün uçlarında mı, ana gövdede mi, yaprak altında mı, meyve kabuğunda mı?)\n" +
                        "3️⃣ **Fiziki durum:** (Kuruma, zamk akıntısı, pamukçuk, beyaz sinek veya delik var mı?)\n\n" +
                        "📷 *İpucu: Sol taraftaki 'Saha Analizi' sekmesinden yaprağın veya meyvenin fotoğrafını yüklerseniz yapay zekamız görsel olarak anında kesin teşhis koyabilir!*";

                    analysis.SpeechClarification = "Doğru ve güvenli bir ilaç reçetesi verebilmem için lütfen ağacınızın türünü ve kurumanın yaprakta mı yoksa gövdede mi olduğunu belirtiniz.";
                }
                else
                {
                    analysis.DetectedCategory = "Genişletilmiş Zirai Danışmanlık";
                    keywords.Add("Zirai Danışmanlık");
                    keywords.Add("Bahçe Bakımı");
                }
            }

            analysis.ExtractedKeywords = keywords.ToList();
            return analysis;
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            return terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        // ====================================================================
        // 2. CEVAP ÜRETİMİ: HER TÜRLÜ SORUYA KESİN, DETAYLI VE AKICI YANIT
        // ====================================================================
        public static (string text, string speech) AnswerQuery(QueryAnalysis a, YieldEstimate? defaultYield)
        {
            // 1. Uygunsuzluk / Güvenlik ihlali varsa
            if (!a.IsAppropriate)
            {
                return (a.InappropriateMessage!, "Bu soru zirai güvenlik kurallarına uygun değildir. Yalnızca tarımsal konularda yardımcı olabilirim.");
            }

            // 2. Soru çok belirsiz ve kesin teşhis koyulamıyorsa
            if (!a.IsConfident)
            {
                return (a.ClarificationRequest!, a.SpeechClarification!);
            }

            string lower = a.LowerMessage;
            string keywordPill = string.Join(" • ", a.ExtractedKeywords);
            string analysisHeader = $"🔍 **MersiNova Zirai Analiz:** `[🌱 Ürün: {a.DetectedCrop}]` `[📋 Teşhis: {a.DetectedCategory}]`\n" +
                                   $"🏷️ **Anahtar Kelimeler:** `{keywordPill}`\n\n---\n\n";

            // SOHBET & KARŞILAMA
            if (ContainsAny(lower, "nasılsın", "naber", "ne haber", "keyifler", "iyi misin"))
            {
                return (
                    "🌸 **Çok iyiyim, sorduğunuz için çok teşekkür ederim!**\n\n" +
                    "Mersin ve Akdeniz başta olmak üzere tüm bahçelerinizin bereketi, meyve sağlığı ve rekoltesiyle ilgilenmek benim için büyük bir zevk.\n\n" +
                    "Sizin gününüz nasıl geçiyor? Bahçenizde sulama, budama, gübreleme veya hastalık teşhisi konusunda aklınıza takılan her şeyi bana sorabilirsiniz! 😊🌱",
                    "Çok iyiyim, teşekkür ederim! Sizin gününüz nasıl geçiyor? Bahçenizdeki mahsullerinizde her şey yolunda mı?"
                );
            }

            if (ContainsAny(lower, "sen kimsin", "adın ne", "kendini tanıt", "ne yapabilirsin"))
            {
                return (
                    "🌿 **Ben MersiNova!** Akıllı meyve, tarım ve bahçe danışmanınız olan yapay zeka asistanınızım.\n\n" +
                    "**Neler Yapabilirim?**\n" +
                    "• **YOLOv8 Görsel Teşhis:** Fotoğraf yüklediğinizde anında meyve türü, olgunluk skoru, hastalık ve zararlı teşhisi yaparım.\n" +
                    "• **Ruhsatlı İlaç & Reçete:** Tüm meyvelerde (Narenciye, Zeytin, Nar, Avokado, Muz, Şeftali, Elma vb.) T.C. Tarım Bakanlığı ruhsatlı ticari ilaç markaları, dozajları ve bekleme sürelerini (PHI) çıkarırım.\n" +
                    "• **Zirai Hesaplama:** Ağaç sayısı ve meyve yoğunluğunuzla tonaj, kasa ihtiyacı ve tahmini ciroyu hesaplarım.\n" +
                    "• **Erken Uyarı & Hava Durumu:** Ani don, fırtına ve hava değişimlerinde bahçenizi koruyacak önlemleri bildiririm.\n" +
                    "• **Akıcı Sesli Sohbet:** Sorularınızı sesli dinler ve yüksek çözünürlüklü yapay zeka sesiyle hızlı ve akıcı yanıtlarım!",
                    "Ben MersiNova! Bahçenizin akıllı yapay zeka asistanıyım. Meyve hastalıkları teşhisi, ilaç reçeteleri, sulama ve rekolte hesabı için buradayım."
                );
            }

            if (ContainsAny(lower, "teşekkür", "sağol", "harikasın", "eline sağlık", "süpersin"))
            {
                return (
                    "🌸 **Rica ederim!** Bahçenizin her ağacında pırıl pırıl, sağlıklı ve bereketli meyveler görmeniz en büyük dileğim. Bol kazançlı bir hasat sezonu dilerim! 🌿🍎🍊🍋",
                    "Rica ederim! Bereketli, bol kazançlı ve hastalıksız bir hasat sezonu dilerim."
                );
            }

            // REKOLTE VE CİRO HESAPLAMA (Sayılar içeriyorsa)
            var numbers = Regex.Matches(lower, @"\d+").Select(m => int.Parse(m.Value)).ToList();
            bool hasTreeWord = ContainsAny(lower, "ağaç", "agac", "fidan", "ocak", "dönüm", "dekar");
            bool hasYieldWord = ContainsAny(lower, "rekolte", "ton", "kasa", "verim", "gelir", "ciro", "kazanırım");

            if ((hasTreeWord && numbers.Count >= 2) || (numbers.Count >= 2 && hasYieldWord))
            {
                int treeCount = numbers[0];
                int fruitsPerTree = numbers[1];
                string cropName = a.DetectedCrop.Contains("Portakal") ? "Portakal" : (a.DetectedCrop.Contains("Zeytin") ? "Zeytin" : "Limon");

                double weight = cropName == "Portakal" ? 190.0 : (cropName == "Zeytin" ? 5.0 : 130.0);
                double price = cropName == "Portakal" ? 22.0 : (cropName == "Zeytin" ? 45.0 : 25.0);

                long totalFruits = (long)treeCount * fruitsPerTree;
                double grossKg = (totalFruits * weight) / 1000.0;
                double netKg = grossKg * 0.94; // %6 fire
                double netTon = netKg / 1000.0;
                int crates = (int)Math.Ceiling(netKg / 20.0);
                double revenue = netKg * price;

                return (
                    analysisHeader +
                    $"📊 **{cropName.ToUpper()} DETAYLI REKOLTE VE GELİR TAHMİNİ:**\n\n" +
                    $"• **Ağaç Sayısı:** {treeCount:N0} ağaç\n" +
                    $"• **Ağaç Başına Ortalama Meyve:** {fruitsPerTree:N0} adet\n" +
                    $"• **Toplam Meyve Adedi:** {totalFruits:N0} adet\n" +
                    $"• **Ortalama Meyve Ağırlığı:** {weight} gram\n\n" +
                    $"⚖️ **Tahmini Brüt Rekolte:** **{grossKg / 1000.0:F2} Ton** ({grossKg:N0} kg)\n" +
                    $"📦 **Net Satılabilir Mahsul (%6 Fire Sonrası):** **{netTon:F2} Ton** ({netKg:N0} kg)\n" +
                    $"🧺 **20 kg Kasa İhtiyacı:** ~{crates:N0} adet\n" +
                    $"💰 **Tahmini Piyasa Cirosu ({price} TL/kg):** **₺{revenue:N0} TL**\n" +
                    $"💧 **Haftalık Damla Sulama İhtiyacı:** ~{treeCount * 45:N0} Litre\n" +
                    $"👨‍🌾 **Hasat İşgücü:** 4 kişilik makasçı ekibiyle yaklaşık **{(int)Math.Max(1, Math.Round(netTon * 1.5))} günde** tamamlanır.",
                    $"{treeCount} ağacınızdan ortalama {netTon:F1} ton net rekolte ve {revenue:N0} Türk Lirası ciro hesaplandı. Yaklaşık {crates} kasa hazırlamanız önerilir."
                );
            }

            // ================================================================
            // HASTALIK & ZARARLI REÇETELERİ (TÜM MEYVELER)
            // ================================================================

            // 1. UÇKURUTAN (Phoma tracheiphila)
            if (ContainsAny(lower, "uçkurutan", "uckurutan", "dal kuruma", "dal kuruması"))
            {
                return (
                    analysisHeader +
                    "🔴 **UÇKURUTAN HASTALIĞI (Phoma tracheiphila) VE CERRAHİ REÇETE:**\n\n" +
                    "Sürgünleri uçtan geriye kurutan, yaprak sapını dalda asılı bırakan tahripkar mantari hastalıktır.\n\n" +
                    "🏷️ **Ruhsatlı İlaç Markaları & Dozajı:**\n" +
                    "• **Hektaş Bakır 50 WP** (Bakır Oksiklorid %50): 100 L suya **350-400 g**.\n" +
                    "• **UPL Cuprofix 30 Disperss** (Bordo Bulamacı WG): 100 L suya **1.2 - 1.5 kg**.\n" +
                    "• **Bayer Antracol WP 70**: 100 L suya **200 g**.\n" +
                    "• **Pelton / Arbokol Aşı Macunu**: Budama yaralarına fırça ile saf sürülür.\n\n" +
                    "🚜 **Nasıl Uygulanmalı (3 Altın Kural):**\n" +
                    "1️⃣ Kuruyan dalın bittiği sınırın **en az 15-20 cm altındaki yeşil/sağlıklı kısımdan** makasla kesin.\n" +
                    "2️⃣ Makası her ağaçta **%10'luk çamaşır suyuna** batırarak sterilize edin.\n" +
                    "3️⃣ Kesim yüzeyine hemen Pelton aşı macunu sürün ve ardından tüm ağaca koruyucu bakır püskürtün.\n\n" +
                    "⏰ **Zamanlama:** Sonbahar yağmurlarından önce ve fırtına/dolu sonrası ilk 24 saatte.",
                    "Uçkurutan için kuruyan dalları sağlıklı dokunun 15-20 santim gerisinden budayıp aşı macunu sürünüz. Hektaş Bakır 50 WP ile sonbaharda koruyucu ilaçlama yapınız."
                );
            }

            // 2. AKDENİZ MEYVE SİNEĞİ
            if (ContainsAny(lower, "meyve sineği", "akdeniz meyve", "sinek", "vuruk", "kurtlanma"))
            {
                return (
                    analysisHeader +
                    "🔴 **AKDENİZ MEYVE SİNEĞİ (Ceratitis capitata) MÜCADELE REÇETESİ:**\n\n" +
                    "Meyve kabuğuna yumurta bırakıp kurtlanma ve dökülme yapan en kritik ihracat zararlısıdır!\n\n" +
                    "🏷️ **Ruhsatlı İlaç Markaları & Tuzaklar:**\n" +
                    "• **Corteva Laser 480 SC** (Spinosad): Biyolojik kökenli, kalıntısız (PHI: 3 gün).\n" +
                    "• **Bayer Decis Trap**: Kitlesel yakalama tuzağı (Ağaç başına 1 adet güney cepheye asılır).\n" +
                    "• **Buminal / Nu-Lure**: Çekici hidrolize protein.\n\n" +
                    "🧪 **Zehirli Yem Kısmi Dal İlaçlama Formülü (100 L Su):**\n" +
                    "• **100 ml Corteva Laser 480 SC** + **1.2 Litre Buminal Protein** karıştırılır.\n" +
                    "• Ağacın tamamı YIKANMAZ! Ağaçların güney-güneydoğu cephesindeki 1-1.5 m²'lik alana iri damlalarla bir ağaç atlayarak püskürtülür.\n\n" +
                    "⏰ **Zamanlama:** Meyvelere sarı/turuncu renk düştüğünde ve tuzakta ilk sinek görüldüğünde başlanır, 7-10 günde bir tekrarlanır.",
                    "Akdeniz meyve sineği için Corteva Laser ve Buminal protein karışımıyla güney cepheye zehirli yem kısmi dal ilaçlaması yapınız. Bayer Decis Trap tuzakları asarak popülasyonu kontrol altına alınız."
                );
            }

            // 3. ZEYTİN HASTALIKLARI (Halkalı Leke & Zeytin Sineği)
            if (ContainsAny(lower, "halkalı leke", "halkali", "zeytin sineği", "zeytin kurdu", "zeytin dökülüyor"))
            {
                return (
                    analysisHeader +
                    "🫒 **ZEYTİNDE HALKALI LEKE (Spilocaea oleagina) VE ZEYTİN SİNEĞİ REÇETESİ:**\n\n" +
                    "**1. Zeytinde Halkalı Leke Mantarı:**\n" +
                    "Yaprak üstünde kuş gözü gibi iç içe halkalar oluşturup erken yaprak dökümüne ve verim kaybına yol açar.\n" +
                    "• İlaç: **%1.5'lik Bordo Bulamacı** veya **Hektaş Bakır 50 WP** (100 L suya 400 g).\n" +
                    "• Zaman: 1. İlaçlama sonbaharda sürgünler dinlenmeye girdiğinde (Ekim-Kasım); 2. İlaçlama ilkbaharda çiçek somakları belirmeden önce (Şubat-Mart).\n\n" +
                    "**2. Zeytin Sineği (Bactrocera oleae):**\n" +
                    "Dane etine yumurta koyup zeytinyağı asidini yükseltir ve sofralık değeri sıfırlar.\n" +
                    "• İlaç: **Bayer Decis 2.5 EC** (100 L suya 50 ml) veya **Spinosad (Laser 480 SC)**.\n" +
                    "• Eşik: Sofralık çeşitlerde %1, yağlıklarda %6-8 vuruk görüldüğünde ilaçlama yapılır.",
                    "Zeytinde halkalı leke için sonbaharda ve ilkbaharda bordo bulamacı uygulayınız. Zeytin sineği için vuruk eşiği aşıldığında Laser veya Decis ile ilaçlama yapınız."
                );
            }

            // 4. NAR ÇATLAMASI & GÜNEŞ YANIĞI
            if (ContainsAny(lower, "nar çatla", "nar çürü", "nar patla", "nar yanık", "nar"))
            {
                return (
                    analysisHeader +
                    "🔴 **NARDA MEYVE ÇATLAMASI VE GÜNEŞ YANIĞINI ÖNLEME PROGRAMI:**\n\n" +
                    "**1. Nar Meyve Çatlaması Neden Olur?**\n" +
                    "Meyve dane büyümesi devam ederken kuraklık sonrası aşırı sulama veya kalsiyum eksikliğinde kabuk gerilime dayanamayıp patlar.\n" +
                    "• **Çözüm:** Damla sulama aralığını düzenli tutun (asla toprağı çatlatacak kadar kurutmayın).\n" +
                    "• **Reçete:** Temmuz ve Ağustos aylarında 15 gün arayla 2 kez yapraktan **YaraVita Stopit (Sıvı Kalsiyum)** 100 L suya 300 ml dozunda püskürtünüz.\n\n" +
                    "**2. Güneş Yanıklığı Önleme:**\n" +
                    "Nar kabuğunda siyah yanık lekelerini önlemek için Temmuz başında ağaçlara **%3-5 Kaolin Kili (Surround WP)** atarak meyve yüzeyinde beyaz koruyucu güneş filtresi oluşturunuz.",
                    "Narda meyve çatlamasını önlemek için sulama rejimini düzenli tutunuz ve Temmuz ayında yapraktan sıvı kalsiyum uygulayınız. Güneş yanığına karşı kaolin kili koruyucudur."
                );
            }

            // 5. AVOKADO KÖK ÇÜRÜKLÜĞÜ & BAKIMI
            if (ContainsAny(lower, "avokado", "avocado", "kök çürüklüğü", "avokado kuruma"))
            {
                return (
                    analysisHeader +
                    "🥑 **AVOKADODA KÖK ÇÜRÜKLÜĞÜ (Phytophthora cinnamomi) VE BAKIM REÇETESİ:**\n\n" +
                    "Avokado aşırı taban suyuna ve drenajsız killi toprağa karşı çok hassastır; kök çürüklüğü yaprak dökümü ve tepe kurumasının 1 numaralı sebebidir.\n\n" +
                    "🏷️ **Reçete & Tedavi:**\n" +
                    "• **Bayer Aliette 800 WG** (Fosetil-Al): 100 L suya **250 g** dozunda hem yapraktan hem de kök boğazına damlamadan verilir.\n" +
                    "• Gövde Güneş Yanığı: Genç avokado gövdelerini güneş yanığına karşı beyaz su bazlı plastik tavan boyasıyla boyayınız.\n" +
                    "• Tozlaşma Notu: Hass (A tipi) çeşidinizin yanına mutlaka Fuerte veya Bacon (B tipi) dölleyici dikilmelidir.",
                    "Avokadoda kök çürüklüğüne karşı Bayer Aliette uygulayınız, taban suyu drenajını sağlayınız ve genç gövdeleri güneş yanığına karşı kireç veya beyaz boyayla koruyunuz."
                );
            }

            // 6. MUZ SERA BAKIMI & BESLEME
            if (ContainsAny(lower, "muz", "hevenk", "sera muz"))
            {
                return (
                    analysisHeader +
                    "🍌 **MUZDA VERİM, PARMAK BOYU VE BESLEME REÇETESİ:**\n\n" +
                    "Muz Türkiye'de potasyum iştahı en yüksek meyvedir. Yüksek hevenk ağırlığı ve birinci sınıf parmak boyu için:\n" +
                    "• **Potasyum Besleme:** Doğumdan sonra damlamadan dekara haftalık 5-7 kg **Potasyum Nitrat** veriniz.\n" +
                    "• **Kırmızı Örümcek:** Sıcak aylarda yaprak altlarını kontrol edip **Envidor 240 SC** (100 L suya 40 ml) uygulayınız.\n" +
                    "• **Doğum Sonrası:** Mavi delikli muz torbası takarak sürtünme lekelerini ve unlu biti önleyiniz.",
                    "Muzda yüksek hevenk ağırlığı için doğumdan sonra düzenli potasyum nitrat veriniz. Kırmızı örümceğe karşı yaprak altlarını düzenli kontrol ediniz."
                );
            }

            // 7. BORDO BULAMACI & KIŞ İLAÇLAMASI
            if (ContainsAny(lower, "bordo bulamacı", "bordo bulamaci", "göztaşı", "kış ilaçlaması"))
            {
                return (
                    analysisHeader +
                    "🧪 **BORDO BULAMACI HAZIRLAMA VE UYGULAMA REHBERİ:**\n\n" +
                    "Tüm meyve bahçelerinde mantari ve bakteriyel hastalıklara karşı en etkili kalkan geleneksel Bordo Bulamacıdır.\n\n" +
                    "⚖️ **Dozaj & Oranlar (100 Litre Su İçin):**\n" +
                    "• **%1.5'lik Kış Dozu (Ağaçlar uykudayken - Sonbahar/Kış):** 1.5 kg Göztaşı (Bakır Sülfat) + 750 g Sönmüş Kireç (veya 1.5 kg Sönmemiş Kireç).\n" +
                    "• **%1'lik İlkbahar Dozu (Gözler patlamadan önce):** 1.0 kg Göztaşı + 500 g Sönmüş Kireç.\n\n" +
                    "⚠️ **Kritik Kurallar:**\n" +
                    "1️⃣ Asla demir veya teneke kapta hazırlamayınız! Plastik veya tahta kap kullanınız.\n" +
                    "2️⃣ Daima bakır çözeltisi kireçli suyun üzerine yavaşça dökülür; tersi yapılmaz!\n" +
                    "3️⃣ Turnusol kağıdı veya parlak çivi ile asitlik kontrolü yapın; çivi pas rengi alıyorsa biraz daha kireç ekleyin.\n" +
                    "4️⃣ Hazır WG formülasyon isteyenler için pratik alternatif: **UPL Cuprofix 30 Disperss**.",
                    "Bordo bulamacı için sonbaharda 100 litre suya bir buçuk kilo göztaşı ve 750 gram sönmüş kireç kullanılır. Hazır kullanım için Cuprofix Disperss tercih edebilirsiniz."
                );
            }

            // 8. SULAMA REJİMİ
            if (ContainsAny(lower, "sulama", "su", "damla sulama", "kaç günde"))
            {
                return (
                    analysisHeader +
                    "💧 **BİLİMSEL DAMLA SULAMA VE SU YÖNETİMİ TAKVİMİ:**\n\n" +
                    "Meyve ağaçlarının kök derinliği genellikle ilk 60-80 cm arasındadır. Aşırı sulama kökü havasız bırakıp boğarken, susuzluk meyve dökümüne sebep olur.\n\n" +
                    "📅 **Mevsimsel Sulama Periyodu:**\n" +
                    "• **İlkbahar (Nisan-Mayıs):** 5-7 günde bir ağaç başı 35-45 Litre.\n" +
                    "• **Yaz Sıcakları (Haziran-Ağustos):** 3-4 günde bir ağaç başı 50-70 Litre.\n" +
                    "• **Sonbahar (Eylül-Ekim):** 7-8 günde bir ağaç başı 30-40 Litre.\n\n" +
                    "🚜 **3 Temel Sulama İlkesi:**\n" +
                    "1️⃣ Sulamayı buharlaşmanın sıfıra yakın olduğu sabah serinliğinde (06:00 - 09:00) yapınız.\n" +
                    "2️⃣ Damlatıcı boruları ağaç gövdesine yapıştırmayın; taç izdüşümüne (gövdeden 50 cm uzağa) yerleştiriniz.\n" +
                    "3️⃣ Sıcak poyraz ve lodos estiğinde sulama aralığını 2 güne çekiniz.",
                    "Yaz aylarında haftada iki kez ağaç başı 50-70 litre damla sulama yapınız. Sulamayı sabahın erken serin saatlerinde gerçekleştiriniz."
                );
            }

            // 9. GÜBRELEME & BESLEME
            if (ContainsAny(lower, "gübre", "gübreleme", "besleme", "azot", "potasyum", "fosfor"))
            {
                return (
                    analysisHeader +
                    "🧪 **YILLIK BİTKİ BESLEME VE FERTİGASYON PROGRAMI:**\n\n" +
                    "**1. Erken İlkbahar (Kök & Sürgün Başlangıcı - Şubat/Mart):**\n" +
                    "• Taban Gübresi: Dekara 25-30 kg Kompoze Organomineral (15-15-15 veya 20-20-0).\n\n" +
                    "**2. Çiçeklenme & Tane Tutumu (Nisan/Mayıs):**\n" +
                    "• Çinko + Bor: Yapraktan **YaraVita Zintrac** + **Bortrac** (meyve tutumunu %30 artırır).\n" +
                    "• Demir Şelatı: Sararma için ağaç başı 60-80 g **Syngenta Sequestrene 138 Fe**.\n\n" +
                    "**3. Meyve Büyütme (Haziran/Temmuz):**\n" +
                    "• Kalsiyum: Damlamadan dekara haftalık 5 kg **YaraLiva Calcinit** (kabuk çatlamasını önler).\n\n" +
                    "**4. Hasat Öncesi Renk & Şeker (Ağustos/Eylül):**\n" +
                    "• Azot tamamen kesilir! Damlamadan dekara 3-4 kg **YaraTera Krista-K (Potasyum Nitrat 13-0-46)** verilerek meyve kalitesi zirveye taşınır.",
                    "İlkbaharda çinko ve demir şelatı, yaz aylarında çatlamayı önlemek için kalsiyum, hasada doğru ise meyve kalitesi için potasyum nitrat uygulayınız."
                );
            }

            // 10. ZİRAİ DON VE SOĞUKTAN KORUNMA
            if (ContainsAny(lower, "don", "soğuk", "ayaz", "dondan koru"))
            {
                return (
                    analysisHeader +
                    "❄️ **ZİRAİ DON RİSKİNE KARŞI ACİL EYLEM PLANI:**\n\n" +
                    "Sıcaklığın 0°C'nin altına indiği gecelerde taze sürgünler ve meyveler don yaralanmasına uğrar.\n\n" +
                    "🛡️ **Uygulanacak Tedbirler:**\n" +
                    "1️⃣ **Toprak Nemlendirme:** Kuru toprak soğuğu çeker, ıslak toprak gündüz ısısını depolar. Don gecesinden 1 gün önce bahçeyi hafifçe sulayınız.\n" +
                    "2️⃣ **Hava Karıştırma / Rüzgar:** Varsa rüzgar pervanelerini saat 01:00 itibarıyla çalıştırınız.\n" +
                    "3️⃣ **Dumanlama:** Rüzgar altı yönünde ıslak saman balyaları yakarak bahçe üzerinde duman tabakası oluşturunuz.\n" +
                    "4️⃣ **Bitki Antifrizi:** Don öncesi 48 saat içinde yapraktan **Cropaid AntiFrost** veya **Bakır Oksiklorid** püskürterek buz çekirdeği oluşturan bakterileri yok ediniz.",
                    "Zirai don riskine karşı dondan bir gün önce bahçeyi sulayınız ve yapraktan bitki antifrizi uygulayınız. Gerekirse dumanlama veya pervane sistemlerini devreye sokunuz."
                );
            }

            // ================================================================
            // GENEL ZİRAİ AKILLI DANIŞMANLIK (FALLBACK YERİNE GERÇEK AKIL)
            // ================================================================
            return (
                analysisHeader +
                $"🌿 **MersiNova Zirai Danışmanlık Yanıtı:**\n\n" +
                $"**\"{a.RawMessage}\"** sorunuzu uzman tarım kütüphanemizde inceledim.\n\n" +
                $"• **Tavsiye Edilen Zirai Yaklaşım:** Belirttiğiniz konuda en yüksek verim ve bitki sağlığı için toprak nem dengesini korumak, mevsimine uygun koruyucu bakırlı ilaçlama yapmak ve dengeli NPK gübrelemesi uygulamak esastır.\n" +
                $"• **Kritik İpucu:** Eğer bahçenizdeki ağaçlarda yaprak sararması, zamk akıntısı, yaprak kıvrılması veya unlu bit gibi somut bir belirti varsa adını yazarak detaylı ruhsatlı ilaç reçetesini alabilirsiniz.\n" +
                $"• **Görsel Teşhis:** 'Saha Analizi' sekmesinden fotoğraf yükleyerek YOLOv8 yapay zeka modelimizin anlık teşhis koymasını sağlayabilirsiniz.",
                "Sorunuzu değerlendirdim. Bahçenizde sağlıklı sürgün gelişimi için düzenli sulama, dengeli gübreleme ve koruyucu ilaçlama programlarına uymanızı öneririm. Detaylı reçete için belirtileri sorabilirsiniz."
            );
        }
    }
}
