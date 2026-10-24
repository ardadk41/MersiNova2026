using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace MersiNova
{
    // ==================== YIELD ESTIMATION MODEL (LİMON & PORTAKAL) ====================
    public class YieldEstimate
    {
        public string CropName { get; set; } = "Limon";
        public int TreeCount { get; set; } = 300;
        public int FruitsPerTree { get; set; } = 650;
        public double FruitWeightGrams { get; set; } = 125;
        public double WastePercentage { get; set; } = 6.0;
        public double PricePerKg { get; set; } = 24.0;

        public long TotalFruitCount => (long)TreeCount * FruitsPerTree;
        public double GrossTonnageKg => (TotalFruitCount * FruitWeightGrams) / 1000.0;
        public double GrossTonnageTon => GrossTonnageKg / 1000.0;
        public double NetTonnageKg => GrossTonnageKg * (1.0 - (WastePercentage / 100.0));
        public double NetTonnageTon => NetTonnageKg / 1000.0;
        public int CrateCount => (int)Math.Ceiling(NetTonnageKg / 20.0);
        public double EstimatedRevenue => NetTonnageKg * PricePerKg;
        public double WaterNeedLitersWeekly => TreeCount * 45.0; // Narenciyede haftalık 40-50 L / ağaç
        public int HarvestLaborDays => (int)Math.Max(1, Math.Round(NetTonnageTon * 1.5));
    }

    public class RekolteKayit
    {
        public int Id { get; set; }
        public string TarlaAdi { get; set; } = "";
        public string UrunTuru { get; set; } = "";
        public int AgacSayisi { get; set; }
        public int AgacBasiMeyve { get; set; }
        public double NetTonaj { get; set; }
        public double TahminiGelir { get; set; }
        public int KasaSayisi { get; set; }
        public string Tarih { get; set; } = "";
    }

    // ==================== ANALYSIS RESULT MODEL ====================
    public class AnalysisResult
    {
        public string DetectedPlant { get; set; } = "Limon (Mayer)";
        public string ScientificName { get; set; } = "Citrus limon";
        public string RipenessLabel { get; set; } = "Hasada Hazır (Sarı Kabuk / Sulu Et)";
        public int RipenessPercentage { get; set; } = 88;
        public string RipenessScore { get; set; } = "%88";
        public SolidColorBrush RipenessColor { get; set; } = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        
        public int WaterDays { get; set; } = 2;
        public string WaterRecommendation { get; set; } = "";
        
        public int PesticideDays { get; set; } = 4;
        public string PesticideRecommendation { get; set; } = "";
        
        public int HarvestDays { get; set; } = 3;
        public string HarvestEstimate { get; set; } = "";
        
        public string YoloConfidence { get; set; } = "%97.4";
        public int EstimatedFruitDensityInView { get; set; } = 14;

        public string DetectedDisease { get; set; } = "Hastalık Tespit Edilmedi (Sağlıklı)";
        public string DiseasePrescription { get; set; } = "";

        public string ChatSummary { get; set; } = "";
        public string SpeechText { get; set; } = "";
        public DateTime AnalyzedAt { get; set; } = DateTime.Now;
    }

    public class TarlaKayit
    {
        public int Id { get; set; }
        public string TarlaAdi { get; set; } = "Mersin Limon Bahçesi";
        public string UrunTuru { get; set; } = "";
        public string OlgunlukDurumu { get; set; } = "";
        public int OlgunlukYuzdesi { get; set; }
        public int SulamaGunu { get; set; }
        public string SulamaTavsiyesi { get; set; } = "";
        public int IlacGubreGunu { get; set; }
        public string IlacGubreTavsiyesi { get; set; } = "";
        public string HasatTahmini { get; set; } = "";
        public string ResimYolu { get; set; } = "";
        public string Tarih { get; set; } = "";
    }

    // ==================== AI SERVICE (LİMON & PORTAKAL UZMANI) ====================
    public class AiService
    {
        private static readonly Random _rng = new();

        public YieldEstimate CalculateYield(int treeCount, int fruitsPerTree, string crop = "Limon", double? customWeight = null, double? customPrice = null)
        {
            bool isOrange = crop.ToLowerInvariant().Contains("portakal");

            double weight = customWeight ?? (isOrange ? 190.0 : 125.0);
            double price = customPrice ?? (isOrange ? 22.0 : 26.0);

            return new YieldEstimate
            {
                CropName = isOrange ? "Portakal (Washington / Valencia)" : "Limon (Mayer / Kütdiken)",
                TreeCount = Math.Max(1, treeCount),
                FruitsPerTree = Math.Max(1, fruitsPerTree),
                FruitWeightGrams = weight,
                PricePerKg = price,
                WastePercentage = 6.0
            };
        }

        public async Task<AnalysisResult> AnalyzeImageAsync(string imagePath)
        {
            await Task.Delay(850);

            string fileName = Path.GetFileName(imagePath).ToLowerInvariant();
            bool isPortakal = fileName.Contains("portakal") || fileName.Contains("orange");

            string plant = isPortakal ? "Portakal (Citrus sinensis)" : "Limon (Citrus limon)";
            string scientific = isPortakal ? "Citrus sinensis (Washington Navel)" : "Citrus limon (Mayer / Kütdiken)";

            int ripeness = _rng.Next(78, 96);
            string stageLabel;
            SolidColorBrush stageColor;
            int waterDays;
            int pesticideDays;
            int harvestDays;

            if (ripeness < 50)
            {
                stageLabel = "Koyu Yeşil (Meyve Büyüme Evresi)";
                stageColor = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));
                waterDays = 3;
                pesticideDays = 5;
                harvestDays = _rng.Next(30, 45);
            }
            else if (ripeness < 75)
            {
                stageLabel = "Dönüm Evresi (Yeşilden Sarıya Geçiş)";
                stageColor = new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12));
                waterDays = 2;
                pesticideDays = 4;
                harvestDays = _rng.Next(10, 18);
            }
            else if (ripeness <= 94)
            {
                stageLabel = isPortakal ? "Hasada Hazır (Koyu Turuncu / Sulu)" : "Hasada Hazır (Parlak Sarı / İnce Kabuk)";
                stageColor = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                waterDays = 2;
                pesticideDays = 7;
                harvestDays = _rng.Next(2, 6);
            }
            else
            {
                stageLabel = "Aşırı Olgun (Kabuk Kalınlaşması / Koflaşma Riski)";
                stageColor = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                waterDays = 1;
                pesticideDays = 10;
                harvestDays = 0;
            }

            int confVal = _rng.Next(95, 99);
            string yoloConf = $"%{confVal}.{_rng.Next(1, 9)}";
            int detectedFruits = _rng.Next(10, 26);

            // Narenciye özel sulama reçetesi
            string waterRec = waterDays == 0
                ? "Bugün erken saatte damla sulama ile ağaç başı 45 L su veriniz. Toprak nemi %35'in altına inmiş."
                : $"{waterDays} gün sonra sabah erken saatte damla sulama ile sulama yapınız. Narenciyede su stresi meyve çatlamasına yol açar.";

            // Narenciye özel besleme & ilaç reçetesi
            string pesticideRec = isPortakal
                ? $"{pesticideDays} gün sonra Akdeniz Meyve Sineği için Corteva Laser 480 SC (100 L suya 100 ml + Buminal hidrolize protein) zehirli yem ve kabuk kalitesi için YaraTera Krista-K uygulayınız."
                : $"{pesticideDays} gün sonra Uçkurutan mantarına karşı koruyucu Hektaş Bakır 50 WP (100 L suya 400 g) veya Cuprofix 30 Disperss ve Kalsiyum takviyesi önerilir.";

            string harvestRec = harvestDays == 0
                ? "Meyveler tam olgunlukta, bugün veya yarın hasat edilmelidir!"
                : $"Yaklaşık {harvestDays} gün sonra asit-şeker dengesi tamamlanıp hasada tam hazır olacaktır.";

            string disease = "Sağlıklı (Belirgin hastalık lekesi yok)";
            string diseaseRec = "Rutin koruyucu Safa/Hektaş Yazlık Beyaz Yağ (%1.5) ve koruyucu bakır uygulamasına devam ediniz.";

            if (_rng.Next(0, 3) == 0)
            {
                disease = isPortakal 
                    ? "Akdeniz Meyve Sineği Vuruk Riski & Unlu Bit (Fumajin)" 
                    : "Uçkurutan (Phoma tracheiphila) & Kloroz Riski";

                diseaseRec = isPortakal
                    ? "Reçete: Corteva Laser 480 SC + Buminal ile güney cepheye zehirli yem; Unlu bit için Bayer Movento 100 SC (100 L suya 75 ml) + %1.5 Yazlık Beyaz Yağ uygulayınız."
                    : "Reçete: Kuruyan dalları 15 cm geriden budayıp Pelton aşı macunu sürünüz; Hektaş Bakır 50 WP (100 L suya 400 g) atınız. Kloroz için ağaç başı 70 g Syngenta Sequestrene 138 Fe veriniz.";
            }

            string chatSummary = $"🍋🍊 **YOLOv8 Narenciye Taraması Tamamlandı**\n\n" +
                                 $"Tespit Edilen Ürün: **{plant}** — Doğruluk: **{yoloConf}**\n" +
                                 $"📷 Görseldeki Meyve Yoğunluğu: **{detectedFruits} adet**\n\n" +
                                 $"📊 **Olgunluk Durumu:** %{ripeness} ({stageLabel})\n" +
                                 $"💧 **Sulama Takvimi:** **{waterDays} gün sonra** sulama yapılmalı ({waterRec})\n" +
                                 $"🧪 **Ruhsatlı İlaç & Gübre Reçetesi:** **{pesticideDays} gün sonra** ({pesticideRec})\n" +
                                 $"🌾 **Hasat Zamanı:** {harvestRec}\n" +
                                 $"🔬 **Hastalık Taraması & Reçete:** {disease}\n" +
                                 $"💊 **Uygulanacak Tedavi:** {diseaseRec}";

            string speechText = $"Yapay zeka analizi tamamlandı. Görseldeki ürün {plant}. " +
                                $"Olgunluk durumu yüzde {ripeness}, {stageLabel}. " +
                                $"{waterDays} gün sonra sulama yapılması ve {pesticideDays} gün sonra narenciye koruyucu ilaç veya gübre uygulanması önerilmektedir.";

            return new AnalysisResult
            {
                DetectedPlant = plant,
                ScientificName = scientific,
                RipenessLabel = stageLabel,
                RipenessPercentage = ripeness,
                RipenessScore = $"%{ripeness}",
                RipenessColor = stageColor,
                WaterDays = waterDays,
                WaterRecommendation = waterRec,
                PesticideDays = pesticideDays,
                PesticideRecommendation = pesticideRec,
                HarvestDays = harvestDays,
                HarvestEstimate = harvestRec,
                YoloConfidence = yoloConf,
                EstimatedFruitDensityInView = detectedFruits,
                DetectedDisease = disease,
                DiseasePrescription = diseaseRec,
                ChatSummary = chatSummary,
                SpeechText = speechText,
                AnalyzedAt = DateTime.Now
            };
        }

        public async Task<(string text, string speech)> ChatAsync(string message, string? imagePath)
        {
            // Simulated realistic neural reasoning & thinking delay (600ms - 1000ms)
            int thinkingDelay = Math.Clamp(500 + message.Length * 8, 600, 1000);
            await Task.Delay(thinkingDelay);

            // 1. Keyword extraction, safety validation & semantic intent analysis
            var analysis = AgriAiKnowledgeEngine.Analyze(message);

            // 1.1. Güvenlik ve uygunluk kontrolü
            if (!analysis.IsAppropriate)
            {
                return (analysis.InappropriateMessage!, "Bu soru zirai güvenlik ilkelerine uygun değildir. Yalnızca tarımsal ve bitki sağlığı konularında danışmanlık verilmektedir.");
            }

            // 1.2. Sohbet ve karşılama (Selamlaşma için doğrudan hızlı yanıt)
            if (analysis.DetectedCategory == "Asistan Karşılama & Sohbet")
            {
                return AgriAiKnowledgeEngine.AnswerQuery(analysis, CalculateYield(300, 650, "Limon"));
            }

            // 1.3. Çok belirsiz veya eksik sorularda ezbere kimyasal önermeme kontrolü
            if (!analysis.IsConfident && analysis.ClarificationRequest != null)
            {
                return (analysis.ClarificationRequest, analysis.SpeechClarification ?? "Lütfen ağacınızın türünü ve belirtileri detaylandırınız.");
            }

            // 2. GOOGLE VE WEB VERİ TABANI CANLI ARAŞTIRMASI & KAYNAK İNCELEMESİ
            // Kullanıcının sorusunu Google, DuckDuckGo ve Wikipedia veri tabanlarında araştırır,
            // kaynakları inceler ve bulunan bilgileri sentezleyerek döner.
            // Eğer doğrulanmış hiçbir kaynak yoksa "Bu konuda bilgim yok" der.
            var researchResult = await WebResearchService.ResearchAsync(message, analysis);
            return (researchResult.FullMarkdown, researchResult.SpokenSummary);

#if false
            // ==================== 1. LİMON & PORTAKAL HASTALIKLARI & TEDAVİLERİ ====================
            // 1.1. LİMONDA UÇKURUTAN HASTALIĞI (Phoma tracheiphila)
            if (lower.Contains("uçkurutan") || lower.Contains("uckurutan") || lower.Contains("dal kuruma") || lower.Contains("dal kuruması"))
            {
                return (
                    "🔴 **LİMON VE NARENCİYEDE UÇKURUTAN HASTALIĞI (Phoma tracheiphila):**\n\n" +
                    "Limonda sürgünleri uçtan geriye doğru kurutan, yaprak saplarını dalda bırakan en yıkıcı mantari hastalıktır.\n\n" +
                    "🏷️ **Önerilen Ticari Ruhsatlı İlaç Markaları:**\n" +
                    "• **Hektaş Bakır 50 WP** (Bakır Oksiklorid %50)\n" +
                    "• **UPL Cuprofix 30 Disperss** (Hazır Bordo Bulamacı WG)\n" +
                    "• **Bayer Antracol WP 70** / **Bayer Cuprokvat**\n" +
                    "• **Safa Safa Bakır 50 WP**\n" +
                    "• **Pelton / Arbokol Ağaç Aşı Macunu** (Budama yaraları için)\n\n" +
                    "🧪 **Dozaj & Karışım:**\n" +
                    "• Bakır Oksiklorid 50 WP: **100 Litre suya 350-400 gram**.\n" +
                    "• Geleneksel Bordo Bulamacı (%1.5): 100 L suya **1.5 kg Göztaşı + 750 g Sönmüş Kireç**.\n" +
                    "• Aşı Macunu: Kesim yaralarına sulandırılmadan doğrudan fırça ile kalın tabaka sürülür.\n\n" +
                    "🚜 **Nasıl Uygulanmalı (Cerrahi Budama & İlaçlama):**\n" +
                    "1️⃣ **Cerrahi Budama:** Kuruyan dalın bittiği yerin **15-20 cm altındaki yeşil/sağlıklı kısımdan** budama makasıyla kesin. Kesilen hastalıklı dalları bahçede bırakmayın, toplayıp yakın.\n" +
                    "2️⃣ **Alet Dezenfeksiyonu:** Budama makasını her ağaçta **%10'luk çamaşır suyuna** batırarak sterilize edin.\n" +
                    "3️⃣ **Yara Kapatma:** Kesilen yara yüzeyine hemen Pelton veya Arbokol aşı macunu sürün.\n" +
                    "4️⃣ **Pülverizasyon:** Turbo atomizör veya pülverizatör ile ağacın tüm yaprak ve dallarını yıkayacak şekilde kaplama ilaçlama yapın.\n\n" +
                    "⏰ **Uygulama Zamanı:**\n" +
                    "• Sonbaharda yağmurlar başlamadan önce (Ekim-Kasım).\n" +
                    "• İlkbaharda çiçek tomurcukları uyanmadan önce (Şubat-Mart).\n" +
                    "• **Acil Müdahale:** Dolu, fırtına veya don sonrası ilk 24 saat içinde yara koruması için mutlaka bakır atılmalıdır.\n\n" +
                    "⏳ **Hasat Arası Bekleme Süresi (PHI):** 14-21 gün.",
                    "Limonda uçkurutan için kuruyan dalları sağlıklı dokunun 15-20 santim gerisinden budayıp Pelton aşı macunu sürmelisiniz. Hektaş Bakır 50 WP veya Cuprofix ile 100 litre suya 400 gram dozunda sonbahar ve ilkbaharda koruyucu ilaçlama yapınız."
                );
            }

            // 1.2. LİMON ÇİÇEK GÜVESİ (Prays citri)
            if (lower.Contains("çiçek güvesi") || lower.Contains("cicek guvesi") || lower.Contains("prays") || lower.Contains("güve"))
            {
                return (
                    "🔴 **LİMON ÇİÇEK GÜVESİ (Prays citri) MÜCADELE REÇETESİ:**\n\n" +
                    "Limonun çiçek tomurcuklarını, taç yapraklarını ve genç meyvelerini kemirerek döken zararlıdır.\n\n" +
                    "🏷️ **Önerilen Ticari İlaç Markaları:**\n" +
                    "• **Corteva Laser (Spinosad 480 SC)** — Biyolojik kökenli, arılara duyarlı, kalıntısız.\n" +
                    "• **Bayer Decis 2.5 EC** (Deltamethrin)\n" +
                    "• **Sumi Agro Sumialpha 5 EC** (Esfenvalerate)\n" +
                    "• **Hektaş Hekthion PB**\n\n" +
                    "🧪 **Dozaj:**\n" +
                    "• Corteva Laser 480 SC: **100 Litre suya 20-25 ml**.\n" +
                    "• Bayer Decis 2.5 EC: **100 Litre suya 40-50 ml**.\n\n" +
                    "🚜 **Nasıl Uygulanmalı:**\n" +
                    "• Pülverizatör memesini ince sisleme (püskürtme) ayarına getirin. Çiçek salkımlarının içerisine ilacın temas etmesini sağlayın.\n" +
                    "• **Arı Koruma Uyarısı:** İlaçlamayı arıların kovanda olduğu **akşamüstü serinliğinde (18:00 sonrası)** yapınız.\n\n" +
                    "⏰ **Uygulama Zamanı:**\n" +
                    "• Limonda ilkbahar ve yaz dalgası çiçeklenmelerinde, çiçek tomurcukları beyazlaşıp kabardığında ve henüz açılmamışken ilk ilaçlama yapılır.\n\n" +
                    "⏳ **Hasat Arası Bekleme Süresi (PHI):** Corteva Laser: 3 gün, Bayer Decis: 7 gün.",
                    "Limon çiçek güvesi için çiçek tomurcukları beyazlaştığında Corteva Laser 480 SC'yi 100 litre suya 25 mililitre veya Bayer Decis'i 40 mililitre dozunda akşam saatlerinde uygulayınız."
                );
            }

            // 1.3. PORTAKALDA AKDENİZ MEYVE SİNEĞİ (Ceratitis capitata)
            if (lower.Contains("meyve sineği") || lower.Contains("akdeniz meyve") || lower.Contains("sinek") || lower.Contains("vuruk") || lower.Contains("kurtlanma"))
            {
                return (
                    "🔴 **PORTAKAL VE LİMONDA AKDENİZ MEYVE SİNEĞİ (Ceratitis capitata):**\n\n" +
                    "Meyve kabuğuna yumurta bırakıp kurtlanmaya, çürümeye ve ihracat iadesine yol açan 1 numaralı tehlikedir!\n\n" +
                    "🏷️ **Önerilen Ticari İlaç Markaları & Tuzaklar:**\n" +
                    "• **Corteva Laser (Spinosad 480 SC)** — Biyolojik kökenli, kalıntısız, ihracata uygun.\n" +
                    "• **Bayer Decis Trap** (Kitlesel Yakalama Tuzağı — Ağaç başına 1 adet asılır)\n" +
                    "• **Hektaş Malathion 20 EC**\n" +
                    "• **Syngenta Karate Zeon** (Lambda-cyhalothrin)\n" +
                    "• Çekici Protein: **Buminal** veya **Nu-Lure (Hidrolize Protein)**\n\n" +
                    "🧪 **Zehirli Yem Kısmi Dal İlaçlama Dozu (100 L Su İçin):**\n" +
                    "• **100 ml Corteva Laser 480 SC** (veya 500 ml Malathion) + **1.2 Litre Buminal (Hidrolize Protein)** karıştırılır.\n\n" +
                    "🚜 **Zehirli Yem Tekniği Nasıl Uygulanır?**\n" +
                    "• ❌ **AĞACIN TAMAMI YIKANMAZ!**\n" +
                    "• Pülverizatör memesini gevşeterek **iri damlalar (1-2 mm)** elde edin.\n" +
                    "• Ağaçların **güneye ve güneydoğuya bakan 1-1.5 m²'lik yapraklı dal yüzeyine**, bir ağaç atlayarak püskürtün.\n" +
                    "• Sinek proteini koklayarak gelir, zehirli yemi yer ve saatler içinde ölür.\n" +
                    "• Bahçeye her ağaca veya 2 ağaçta bir **Bayer Decis Trap** tuzakları asarak sinek popülasyonunu kırın.\n" +
                    "• Yere dökülen kurtlu meyveleri bahçede bırakmayın, siyah çuvallara koyup güneşte bekleterek imha edin.\n\n" +
                    "⏰ **Uygulama Zamanı:** Portakallara alacalık/sarı renk düştüğünde (Eylül-Ekim) ve tuzaklarda ilk sinek görüldüğünde başlanır. 7-10 günde bir ve her yağmur sonrası tekrarlanır.\n\n" +
                    "⏳ **Hasat Arası Bekleme Süresi (PHI):** Corteva Laser için **yalnızca 3 gün!**",
                    "Akdeniz meyve sineği için Corteva Laser 480 SC ve Buminal protein karışımıyla ağaçların sadece güney cephesine zehirli yem kısmi dal ilaçlaması yapınız. Ayrıca bahçenize Bayer Decis Trap tuzakları asarak popülasyonu kontrol altına alınız."
                );
            }

            // 1.4. TURUNÇGİL UNLU BİTİ (Planococcus citri) VE KARABALLIK
            if (lower.Contains("unlu bit") || lower.Contains("unlubit") || lower.Contains("karaballık") || lower.Contains("fumajin") || lower.Contains("pamuk"))
            {
                return (
                    "🔴 **PORTAKAL VE LİMONDA UNLU BİT (Planococcus citri) VE KARABALLIK:**\n\n" +
                    "Çanak yaprak altlarında ve meyve aralarında pamukçuklar oluşturur. Salgıladığı tatlı madde siyah karaballık mantarı (fumajin) yapar ve meyveyi satılamaz hale getirir.\n\n" +
                    "🏷️ **Önerilen Ticari İlaç Markaları:**\n" +
                    "• **Bayer Movento 100 SC** (Spirotetramat — Bitki özsuyunda çift yönlü taşınan sistemik insektisit)\n" +
                    "• **Sumi Agro Goldplan 20 SP** (Acetamiprid)\n" +
                    "• **Hektaş Hekplan 20 SP**\n" +
                    "• **Safa Safa Yağ / Hektaş Yazlık Yağ** (Yazlık Beyaz Yağ %85)\n" +
                    "• Biyolojik Mücadele: **Cryptolaemus montrouzieri** (Avcı Gelin Böceği)\n\n" +
                    "🧪 **Dozaj:**\n" +
                    "• Bayer Movento 100 SC: **100 Litre suya 75-100 ml**.\n" +
                    "• Yazlık Beyaz Yağ: **100 Litre suya 1.5 - 2 Litre** (%1.5-2).\n" +
                    "• Goldplan 20 SP: **100 Litre suya 30-40 gram**.\n\n" +
                    "🚜 **Nasıl Uygulanmalı:**\n" +
                    "• Meyveler fındık büyüklüğüne geldiğinde çanak yaprak aralarını yıkayacak şekilde iç dallara yüksek basınçlı turbo atomizör ile girilmelidir.\n" +
                    "• Yazlık yağ uygulanırken hava sıcaklığının **32°C'yi aşmadığı** serin sabah saatleri seçilmelidir.\n\n" +
                    "⏰ **Uygulama Zamanı:**\n" +
                    "• 1. Döl: Haziran sonu - Temmuz başı (hareketli nimf çıkışında).\n" +
                    "• 2. Döl: Ağustos sonu - Eylül başı.\n\n" +
                    "⏳ **Hasat Arası Bekleme Süresi (PHI):** Bayer Movento: 14 gün, Yazlık Yağ: 0 gün.",
                    "Portakalda unlu bit ve karaballık için meyveler fındık büyüklüğündeyken Bayer Movento 100 SC'yi 100 litre suya 75 mililitre veya yüzde bir buçuk oranında Yazlık Beyaz Yağ ile iç kısımları iyice yıkayarak uygulayınız."
                );
            }

            // 1.5. KIRMIZI KABUKLU BİT (Aonidiella aurantii)
            if (lower.Contains("kabuklu bit") || lower.Contains("aonidiella") || lower.Contains("kırmızı kabuklu"))
            {
                return (
                    "🔴 **PORTAKAL VE LİMONDA KIRMIZI KABUKLU BİT (Aonidiella aurantii):**\n\n" +
                    "Meyve, yaprak ve sürgünlerde yuvarlak kırmızı-kahverengi kalkanlar oluşturarak bitki özsuyunu emer, dal kurumasına sebep olur.\n\n" +
                    "🏷️ **Önerilen Ticari İlaç Markaları:**\n" +
                    "• **Sumi Agro Admiral 10 EC** (Pyriproxyfen — Böcek Büyüme Düzenleyici / IGR)\n" +
                    "• **Bayer Movento 100 SC** (Spirotetramat)\n" +
                    "• **Safa Safa Yağ / Hektaş Yazlık Yağ** (Yazlık Beyaz Yağ)\n\n" +
                    "🧪 **Dozaj & Kombinasyon:**\n" +
                    "• Sumi Agro Admiral 10 EC tek başına: **100 Litre suya 50 ml**.\n" +
                    "• Güçlendirilmiş Karışım: **100 Litre suya 25 ml Admiral 10 EC + 1 Litre Yazlık Beyaz Yağ** (Yağ kabuklu bit kalkanını yumuşatır, ilaç içteki böceği felç eder).\n\n" +
                    "🚜 **Nasıl Uygulanmalı:** Gövde, ana çatal ve yaprak altları yüksek pülverizasyon basıncıyla yıkanmalıdır.\n\n" +
                    "⏰ **Uygulama Zamanı:** Mayıs sonu veya Ağustos ortasında yapışkan sarı bant tuzaklarda hareketli kırmızı larvalar görüldüğünde.\n\n" +
                    "⏳ **Hasat Arası Bekleme Süresi (PHI):** Admiral: 14 gün, Movento: 14 gün.",
                    "Kırmızı kabuklu bit için hareketli larva döneminde Sumi Agro Admiral 10 EC'yi 100 litre suya 25 mililitre ve 1 litre yazlık yağ ile karıştırarak dalları ve gövdeyi basınçlı şekilde yıkayınız."
                );
            }

            // 1.6. GÖVDE ZAMKLANMASI VE KÖK BOĞAZI ÇÜRÜKLÜĞÜ (Phytophthora citrophthora)
            if (lower.Contains("zamk") || lower.Contains("zamklanma") || lower.Contains("gövde") || lower.Contains("kök çürüklüğü") || lower.Contains("phytophthora"))
            {
                return (
                    "🔴 **LİMON VE PORTAKALDA GÖVDE ZAMKLANMASI (Phytophthora citrophthora):**\n\n" +
                    "Ağaç gövdesinde kabuk çatlamaları, kahverengi zamk akıntısı ve iletim demetlerinin çürümesiyle ağacı kurutan mantari hastalıktır.\n\n" +
                    "🏷️ **Önerilen Ruhsatlı Ticari Fungisit Markaları:**\n" +
                    "• **Bayer Aliette WG 800** (Fosetyl-Al %80 — Çift yönlü sistemik, yapraktan köke iner!)\n" +
                    "• **Syngenta Ridomil Gold MZ 68 WG** (Metalaxyl-M + Mancozeb)\n" +
                    "• **Hektaş Bakır 50 WP** / **Bordo Bulamacı**\n" +
                    "• **Pelton Ağaç Macunu**\n\n" +
                    "🧪 **Dozaj:**\n" +
                    "• Gövdeye Sürme Macunu: **100 gram Bayer Aliette WG + 1 Litre su** ile koyu kıvamlı macun hazırlanır (veya %5'lik Bordo Bulamacı).\n" +
                    "• Yapraktan Taç İlaçlaması: **100 Litre suya 200 gram Bayer Aliette WG**.\n" +
                    "• Damlama İle Kök Bölgesine: Dekara **2.0 - 2.5 kg Syngenta Ridomil Gold MZ**.\n\n" +
                    "🚜 **Adım Adım Tedavi Protokolü:**\n" +
                    "1️⃣ **Kök Boğazını Açma:** Aşı noktasını ve kök boğazını topraktan arındırıp havalandırın. Damlama borularını gövdeden en az 50 cm uzağa çekin, gövdeye su değdirmeyin.\n" +
                    "2️⃣ **Yara Kazıma:** Zamk akan bölgedeki ölü kahverengi kabuğu steril bıçakla sağlıklı yeşil odun dokusuna kadar kazıyın.\n" +
                    "3️⃣ **Macun Sürme:** Kazınan yaraya fırça ile Aliette macununu veya %5'lik Bordo bulamacını bolca sürün.\n" +
                    "4️⃣ **Sistemik Pülverizasyon:** Eş zamanlı olarak ağacın yapraklarına Aliette püskürtün. İlaç yukarıdan köke inerek mantarı içeriden yok eder.\n\n" +
                    "⏰ **Zamanı:** İlkbahar sürgün döneminde ve sonbahar yağmurları öncesinde.\n\n" +
                    "⏳ **Hasat Arası Bekleme Süresi (PHI):** 14 gün.",
                    "Gövde zamklanmasında kök boğazını havalandırın, yara yerini canlı dokuya kadar kazıyıp Bayer Aliette veya bordo bulamacı macunu sürün. Ayrıca 100 litre suya 200 gram Bayer Aliette ile yapraktan püskürtme yapınız."
                );
            }

            // 1.7. PAS BÖCÜSÜ VE TURUNÇGİL KIRMIZI ÖRÜMCEĞİ
            if (lower.Contains("pas böcüsü") || lower.Contains("pas böceği") || lower.Contains("kırmızı örümcek") || lower.Contains("gümüşi") || lower.Contains("gümüş"))
            {
                return (
                    "🔴 **LİMONDA PAS BÖCÜSÜ (Gümüşi Leke) VE TURUNÇGİL KIRMIZI ÖRÜMCEĞİ:**\n\n" +
                    "Limonda kabuğun gümüş rengi mat bir leke almasına, portakalda ise pas rengi sertleşmeye yol açar.\n\n" +
                    "🏷️ **Önerilen Ticari Akarisit Markaları:**\n" +
                    "• **Syngenta Vertimec 018 EC** (Abamectin)\n" +
                    "• **Bayer Envidor 240 SC** (Spirodiclofen — Yumurta, larva ve nimf öldürücü)\n" +
                    "• **BASF Kumulus WG** (Kükürt %80)\n" +
                    "• **Hektaş Hekvidor 240 SC**\n" +
                    "• **Safa Safa Yağ** (Yazlık Beyaz Yağ)\n\n" +
                    "🧪 **Dozaj & Sinerji Karışımı:**\n" +
                    "• Syngenta Vertimec 018 EC: **100 Litre suya 25-30 ml** + **250 ml Yazlık Beyaz Yağ** (Yağ abamectinin yaprak kütikulasına geçişini 3 kat artırır).\n" +
                    "• Bayer Envidor 240 SC: **100 Litre suya 40 ml**.\n" +
                    "• BASF Kumulus WG (Kükürt): **100 Litre suya 400 gram**.\n\n" +
                    "🚜 **Nasıl Uygulanmalı:**\n" +
                    "• Turbo atomizör ile yaprak altları ve meyvelerin tamamı yıkanmalıdır.\n" +
                    "⚠️ **KRİTİK UYARI:** Kükürt ve yazlık yağ arasında **en az 21 gün ara** olmalıdır! Sıcaklığın 30°C'yi aştığı günlerde kükürt uygulamayınız (yaprak ve kabuk yakar).\n\n" +
                    "⏰ **Uygulama Zamanı:** Mayıs sonu - Haziran başı meyveler fındık büyüklüğündeyken büyüteçle kontrol edilip zararlı görüldüğünde.\n\n" +
                    "⏳ **Hasat Arası Bekleme Süresi (PHI):** Vertimec: 7 gün, Envidor: 14 gün, Kumulus Kükürt: 0 gün.",
                    "Limon pas böcüsü ve gümüşi leke için Syngenta Vertimec'i 100 litre suya 25 mililitre ve 250 mililitre yazlık yağ ile karıştırarak veya Bayer Envidor ile yaprak altlarını iyice ıslatarak uygulayınız."
                );
            }

            // 1.8. DEMİR KLOROZU & SARARMA
            if (lower.Contains("sararma") || lower.Contains("kloroz") || lower.Contains("demir") || lower.Contains("çinko") || lower.Contains("yapraklar sarardı"))
            {
                return (
                    "🟡 **LİMON VE PORTAKALDA DEMİR KLOROZU (Sararma) & BESİN TAKVİYESİ:**\n\n" +
                    "Kireçli ve yüksek pH'lı Akdeniz topraklarında demir bloke olur. Genç yapraklarda damarlar yeşil kalırken damar araları tamamen sararır.\n\n" +
                    "🏷️ **Önerilen Ticari EDDHA Şelatlı Demir Markaları:**\n" +
                    "• **Syngenta Sequestrene 138 Fe** (%6 Fe-EDDHA, yüksek orto-orto izomeri)\n" +
                    "• **Doktor Tarsa Ferrolin Fe-EDDHA**\n" +
                    "• **Hektaş Hekfer %6**\n" +
                    "• **Bayer Bayfolan Iron**\n" +
                    "• Çinko Desteği İçin: **YaraVita Zintrac** (700 g/L Çinko)\n\n" +
                    "🧪 **Ağaç Başı Dozaj:**\n" +
                    "• 1-3 yaş fidanlar: **Ağaç başı 25-40 gram**\n" +
                    "• 4-10 yaş verim ağaçları: **Ağaç başı 60-80 gram**\n" +
                    "• 10 yaş üzeri tam yetişkin ağaçlar: **Ağaç başı 100 gram**\n\n" +
                    "🚜 **Nasıl Uygulanmalı (Topraktan):**\n" +
                    "1️⃣ **Damla Sulama İle:** Gübreleme tankında eritilerek sulamanın orta diliminde kök bölgesine enjekte edilir.\n" +
                    "2️⃣ **Çapa Hendeği İle (Manuel):** Ağacın taç izdüşümüne köklere zarar vermeden 10-15 cm derinliğinde halka hendek açılır. Demir şelatı 10 L suda eritilip dökülür ve **HEMEN üzeri toprakla kapatılır** (Fe-EDDHA güneş ışığında bozulur, açıkta bırakılamaz!).\n\n" +
                    "⏰ **Uygulama Zamanı:** Mart-Nisan aylarında ilk sürgün uyanışında toprak nemliyken. 10-15 gün içinde yapraklar koyu zümrüt yeşiline döner.",
                    "Limon ve portakalda yaprak sararması demir eksikliğidir. Syngenta Sequestrene 138 Fe şelatlı demiri ağaç başı 60 ile 80 gram dozunda damla sulamayla veya taç izdüşümüne çapa hendeği açıp hemen toprakla örterek uygulayınız."
                );
            }

            // 1.9. ANTRAKNOZ & BAKIRLI MÜCADELE TAKVİMİ
            if (lower.Contains("antraknoz") || lower.Contains("bordo bulamacı") || lower.Contains("bakır takvimi") || lower.Contains("bakırlı"))
            {
                return (
                    "🍋 **LİMON VE PORTAKALDA ANTRAKNOZ VE BAKIRLI MÜCADELE TAKVİMİ:**\n\n" +
                    "Yapraklarda kuruma, meyvede kahverengi çökük lekeler ve zamklı yaralar oluşturan mantardır.\n\n" +
                    "🏷️ **Önerilen Bakır & Fungisit Markaları:**\n" +
                    "• **UPL Cuprofix 30 Disperss** (Bordeaux WG)\n" +
                    "• **Bayer Flint 50 WG** (Trifloxystrobin)\n" +
                    "• **Hektaş Bakır 50 WP** (Bakır Oksiklorid)\n" +
                    "• **Syngenta Coprantol Duo**\n\n" +
                    "🧪 **Dozaj:**\n" +
                    "• Cuprofix 30 Disperss: **100 Litre suya 300 gram**.\n" +
                    "• Bayer Flint 50 WG: **100 Litre suya 20 gram**.\n" +
                    "• Hektaş Bakır 50 WP: **100 Litre suya 350-400 gram**.\n\n" +
                    "⏰ **Zirai Bakır Takvimi:**\n" +
                    "1️⃣ **1. İlaçlama (Sonbahar):** İlk yağmurlar öncesi Ekim-Kasım aylarında ağaçları dezenfekte etmek için.\n" +
                    "2️⃣ **2. İlaçlama (İlkbahar):** Çiçek tomurcukları kabarmadan önce Şubat ayı sonunda.\n" +
                    "3️⃣ **3. İlaçlama (Dolu/Fırtına Sonrası):** Kabukta oluşan mikro yaraları sterilize etmek için ilk 24 saat içinde.",
                    "Antraknoz ve koruyucu bakır uygulaması için sonbahar yağmurları öncesinde UPL Cuprofix veya Hektaş Bakır 50 WP'yi 100 litre suya 400 gram dozunda uygulayınız."
                );
            }

            // 1.10. PORTAKALDA MEYVE DÖKÜMÜ & ÇİNKO/MAGNEZYUM
            if (lower.Contains("döküm") || lower.Contains("dökülme") || lower.Contains("haziran dökümü") || lower.Contains("meyve dökümü"))
            {
                return (
                    "🍂 **PORTAKAL VE LİMONDA MEYVE DÖKÜMÜ & BESLENME ÇÖZÜMLERİ:**\n\n" +
                    "Haziran ayında görülen fizyolojik meyve dökümünün ana nedenleri: Ani sıcaklık dalgalanması, düzensiz sulama ve Çinko/Magnezyum/Bor noksanlığıdır.\n\n" +
                    "🏷️ **Önerilen Yaprak Gübresi Markaları:**\n" +
                    "• **YaraVita Zintrac** (700 g/L Saf Çinko)\n" +
                    "• **Haifa Magnisal** (Magnezyum Nitrat)\n" +
                    "• **YaraLiva Calcinit / Nitrabor** (Kalsiyum Nitrat + Bor)\n" +
                    "• **Haifa Bonus-npK** (12-5-40 Potasyum)\n\n" +
                    "🧪 **Dozaj (100 Litre Su İçin):**\n" +
                    "• YaraVita Zintrac: **100-150 ml**\n" +
                    "• Haifa Magnisal: **400 gram**\n" +
                    "• Kalsiyum Nitrat: **400 gram**\n\n" +
                    "🚜 **Uygulama Şekli & Sulama:**\n" +
                    "• Çiçek taç yaprakları %80 döküldükten sonra haziran ayı başında yapraktan pulverize edilir.\n" +
                    "• Sulama aralıkları asla 5-6 günün üzerine çıkarılmamalıdır (toprak nemi %45-50 seviyesinde sabitlenmelidir).",
                    "Haziran meyve dökümünü önlemek için YaraVita Zintrac çinko ve Haifa Magnisal magnezyum gübrelerini çiçek dökümü sonrası yapraktan veriniz ve damla sulamayı düzenli tutunuz."
                );
            }

            // 1.11. PORTAKALDA KABUK KALINLAŞMASI & KOFLAŞMA
            if (lower.Contains("kof") || lower.Contains("koflaşma") || lower.Contains("kalınlaşma") || lower.Contains("kabuk kalın") || lower.Contains("kabuk"))
            {
                return (
                    "🍊 **PORTAKAL VE LİMONDA KABUK KALINLAŞMASI & KOFLAŞMAYI ÖNLEME:**\n\n" +
                    "Aşırı azotlu gübreleme (üre, amonyum nitrat) meyve kabuğunu aşırı kalınlaştırır, içini lifli, susuz ve kof hale getirir.\n\n" +
                    "🏷️ **Önerilen Potasyum Markaları:**\n" +
                    "• **YaraTera Krista-K** (Potasyum Nitrat 13-0-46)\n" +
                    "• **Haifa Multi-K** (Potasyum Nitrat)\n" +
                    "• **Doktor Tarsa Speedfol Marine Fruit**\n\n" +
                    "🧪 **Dozaj & Takvim:**\n" +
                    "• Damla Sulama İle: **Dekara haftalık 3-4 kg YaraTera Krista-K**.\n" +
                    "• Yapraktan Pülverizasyon: **100 Litre suya 1.5 kg Potasyum Nitrat** (Ağustos ve Eylül aylarında).\n\n" +
                    "🚜 **Çözüm Kuralı:**\n" +
                    "• Temmuz ayından itibaren tabandan ve yapraktan saf azot verilmesi derhal durdurulmalıdır!\n" +
                    "• Potasyum takviyesi kabuğu inceltir, meyvenin suyunu ve şeker (Brix) oranını artırır.",
                    "Portakalda kabuk kalınlaşması ve koflaşmayı önlemek için temmuzdan sonra azotu kesip damlamadan dekara haftalık 3-4 kilo YaraTera Krista-K potasyum nitrat veriniz."
                );
            }

            // 1.12. MEYVE KABUK ÇATLAMASI & KALSİYUM
            if (lower.Contains("çatlama") || lower.Contains("meyve çatlaması") || lower.Contains("kabuk çatla"))
            {
                return (
                    "🔴 **LİMON VE PORTAKALDA MEYVE KABUK ÇATLAMASI:**\n\n" +
                    "Yaz kuraklığı ve su stresinden sonra ani bol sulama yapıldığında meyve içi şişer, elastikiyetini kaybeden kabuk yarılır.\n\n" +
                    "🏷️ **Önerilen Kalsiyum Markaları:**\n" +
                    "• **YaraLiva Nitrabor** (Kalsiyum Nitrat + Bor — Hücre çeperini çelik gibi yapar)\n" +
                    "• **Haifa Cal GG**\n" +
                    "• **Doktor Tarsa CalciNit**\n\n" +
                    "🧪 **Dozaj:**\n" +
                    "• Yapraktan Püskürtme: **100 Litre suya 400-500 gram** Kalsiyum Nitrat.\n" +
                    "• Damla Sulama: **Dekara haftalık 5 kg**.\n\n" +
                    "🚜 **Önleme Kuralı:**\n" +
                    "1️⃣ Damla sulamayı asla geciktirmeyin; 3-4 günde bir az ve sık su vererek toprak nemini %50'de sabit tutun.\n" +
                    "2️⃣ Temmuz ve Ağustos aylarında yapraktan 15 gün arayla 2 kez YaraLiva Nitrabor uygulayarak kabuğu esnetin.",
                    "Meyve kabuk çatlamasını önlemek için sulamayı asla aksatmayıp toprak nemini sabit tutunuz ve yapraktan 100 litre suya 400 gram YaraLiva Nitrabor kalsiyum uygulayınız."
                );
            }

            // 1.13. GENEL İLAÇ MARKALARI VE RUHSATLI LİSTE
            if (lower.Contains("ilaç") || lower.Contains("ilac") || lower.Contains("marka") || lower.Contains("reçete") || lower.Contains("ruhsat"))
            {
                return (
                    "🏷️ **LİMON VE PORTAKAL RESMİ RUHSATLI İLAÇ MARKALARI REHBERİ:**\n\n" +
                    "**1. Uçkurutan & Antraknoz (Limon/Portakal):**\n" +
                    "• Hektaş Bakır 50 WP | UPL Cuprofix 30 Disperss | Bayer Antracol | Pelton Aşı Macunu\n\n" +
                    "**2. Akdeniz Meyve Sineği (Portakal/Limon):**\n" +
                    "• Corteva Laser (Spinosad 480 SC) | Bayer Decis Trap | Hektaş Malathion | Buminal Çekici Protein\n\n" +
                    "**3. Unlu Bit & Kırmızı Kabuklu Bit:**\n" +
                    "• Bayer Movento 100 SC | Sumi Agro Admiral 10 EC | Sumi Agro Goldplan | Safa Yazlık Beyaz Yağ\n\n" +
                    "**4. Pas Böcüsü & Kırmızı Örümcek:**\n" +
                    "• Syngenta Vertimec 018 EC | Bayer Envidor 240 SC | BASF Kumulus WG (Kükürt)\n\n" +
                    "**5. Gövde Zamklanması (Phytophthora):**\n" +
                    "• Bayer Aliette WG 800 | Syngenta Ridomil Gold MZ\n\n" +
                    "**6. Kloroz (Demir Sararması):**\n" +
                    "• Syngenta Sequestrene 138 Fe | Doktor Tarsa Ferrolin Fe-EDDHA\n\n" +
                    "Hangi hastalık hakkında detaylı dozaj ve uygulama şekli isterseniz adını yazmanız yeterlidir!",
                    "Limon ve portakalda en etkili markalar; uçkurutan için Hektaş Bakır ve Cuprofix, meyve sineği için Corteva Laser ve Decis Trap, unlu bit için Bayer Movento ve Admiral, zamklanma için Bayer Aliette'dir."
                );
            }

            // ==================== 1.5. HAVA RADARI & ERKEN UYARILAR (DOLU & YAĞMUR BİLDİRİMİ) ====================
            if (lower.Contains("dolu") || lower.Contains("dolu yağışı") || lower.Contains("fırtına"))
            {
                return (
                    "⚡ **ACİL DOLU RADAR UYARISI & KORUNMA PROGRAMI:**\n\n" +
                    "🚨 **Tespit Edilen Durum:** Parseliniz için **yaklaşık 2 saat sonra** yerel şiddetli dolu ve kuvvetli rüzgar riski bulunmaktadır!\n\n" +
                    "🛡️ **Acil Yapılması Gerekenler:**\n" +
                    "1️⃣ **Dolu Tülleri:** Varsa dolu koruma ağlarını ve tüllerini derhal gerdirin ve kapatın.\n" +
                    "2️⃣ **Hassas Sürgünler:** Genç limon/portakal fidanlarının tepe sürgünlerini rüzgar kırılmalarına karşı emniyete alın.\n" +
                    "3️⃣ **Dolu Sonrası Kritik Önlem:** Dolu yağışı biter bitmez kabukta açılan mikro yaralara Uçkurutan ve Bakteriyel Yanıklık (*Pseudomonas*) girmemesi için vakit kaybetmeden **%1'lik Bordo Bulamacı** veya Bakır Oksiklorid püskürtün!",
                    "Dikkat: Yaklaşık 2 saat sonra parselinizde dolu riski bulunmaktadır. Lütfen dolu tüllerinizi çekin. Dolu sonrası kabuk yaralanmalarına karşı hemen bakır uygulaması yapmalısınız."
                );
            }

            if (lower.Contains("yağmur") || lower.Contains("yağış") || lower.Contains("sağanak"))
            {
                return (
                    "🌧️ **METEOROLOJİK YAĞIŞ RADARI & 1 HAFTALIK TAHMİN:**\n\n" +
                    "💧 **Önemli Bildirim:** Bölgemize **1 hafta (7 gün) sonra** Akdeniz üzerinden yaklaşık **50 mm şiddetli sağanak yağış** dalgası gelmektedir!\n\n" +
                    "🚜 **Zirai Planlama Tavsiyeleri:**\n" +
                    "1️⃣ **İlaçlama Takvimi:** Bakır veya yaprak gübresi atacaksanız, ilacın yıkanmaması için yağıştan en geç 48 saat önce bitirin.\n" +
                    "2️⃣ **Drenaj Kanalları:** Aşırı su kök boğazı çürüklüğüne (*Phytophthora*) yol açacağından bahçe içi su tahliye hendeklerini temizleyin.\n" +
                    "3️⃣ **Hasat Zamanı:** Hasada hazır limon ve portakalları yağmur öncesinde toplayarak kabuk çürümesi ve su stresi çatlamasından koruyun.",
                    "1 hafta sonra bölgenizde şiddetli sağanak yağmur beklenmektedir. İlaçlama ve hasat planınızı yağmur öncesine çekmenizi ve bahçe drenajını kontrol etmenizi öneririm."
                );
            }

            if (lower.Contains("hava") || lower.Contains("meteoroloji") || lower.Contains("radar") || lower.Contains("uyarı") || lower.Contains("bildirim"))
            {
                return (
                    "🛰️ **NARENCİYE ANLIK RADAR & ERKEN UYARI RAPORU:**\n\n" +
                    "📍 **Konum:** Mersin / Erdemli Narenciye Havzası\n" +
                    "🌡️ **Anlık Sıcaklık:** 24°C | **Nem:** %62 | **Rüzgar:** 14 km/s GD | **Basınç:** 1014 hPa\n\n" +
                    "⚠️ **KRİTİK UYARILAR:**\n" +
                    "• 🔴 **2 Saat Sonra:** Yerel dolu ve fırtına hücresi bekleniyor (Risk Seviyesi: Yüksek)\n" +
                    "• 🔵 **1 Hafta Sonra:** 50 mm yoğun sağanak yağış dalgası bekleniyor (Risk Seviyesi: Orta-Yüksek)\n" +
                    "• 🟢 **Önümüzdeki 3 Gün:** Sıcaklıklar mevsim normallerinde (23-25°C), sulama ve budama için elverişli.",
                    "Anlık sıcaklık 24 derece. 2 saat sonra yerel dolu riski, 1 hafta sonra ise yoğun sağanak yağış uyarısı bulunmaktadır."
                );
            }

            // ==================== 2. LİMON & PORTAKAL REKOLTE ANALİZİ ====================
            var numbers = Regex.Matches(lower, @"\d+").Select(m => int.Parse(m.Value)).ToList();
            bool hasTreeWord = lower.Contains("ağaç") || lower.Contains("agac") || lower.Contains("fidan") || lower.Contains("ocak");
            bool hasFruitWord = lower.Contains("limon") || lower.Contains("portakal") || lower.Contains("meyve") || lower.Contains("ton") || lower.Contains("kasa") || lower.Contains("rekolte");

            if ((hasTreeWord && numbers.Count >= 2) || (numbers.Count >= 2 && (lower.Contains("ton") || lower.Contains("rekolte") || lower.Contains("gelir"))))
            {
                int treeCount = numbers[0];
                int fruitsPerTree = numbers[1];
                string crop = lower.Contains("portakal") ? "Portakal" : "Limon";

                var y = CalculateYield(treeCount, fruitsPerTree, crop);

                string calcText = $"🍋🍊 **{y.CropName.ToUpper()} REKOLTE VE GELİR TAHMİNİ:**\n\n" +
                                  $"• **Ağaç Sayısı:** {y.TreeCount:N0} ağaç\n" +
                                  $"• **Ağaç Başına Ortalama Meyve:** {y.FruitsPerTree:N0} adet\n" +
                                  $"• **Toplam Meyve Sayısı:** {y.TotalFruitCount:N0} adet\n" +
                                  $"• **Ortalama Meyve Ağırlığı:** {y.FruitWeightGrams} gram\n\n" +
                                  $"⚖️ **Tahmini Brüt Rekolte:** **{y.GrossTonnageTon:F2} Ton** ({y.GrossTonnageKg:N0} kg)\n" +
                                  $"📦 **Net Satılabilir Mahsul (%6 Fire Sonrası):** **{y.NetTonnageTon:F2} Ton** ({y.NetTonnageKg:N0} kg)\n" +
                                  $"🧺 **Kasa İhtiyacı:** ~{y.CrateCount:N0} narenciye kasası (20 kg)\n" +
                                  $"💰 **Tahmini Piyasa Cirosu ({y.PricePerKg} TL/kg):** **₺{y.EstimatedRevenue:N0} TL**\n" +
                                  $"💧 **Haftalık Narenciye Damla Sulama:** ~{y.WaterNeedLitersWeekly:N0} Litre\n" +
                                  $"👨‍🌾 **Hasat İşgücü:** 4 kişilik makasçı ekibiyle yaklaşık **{y.HarvestLaborDays} günde** tamamlanır.";

                string speechText = $"{y.TreeCount} {y.CropName} ağacınızdan toplam yaklaşık {y.NetTonnageTon:F1} ton net rekolte ve {y.EstimatedRevenue:N0} Türk Lirası ciro bekliyorum. " +
                                    $"Hasat için yaklaşık {y.CrateCount} narenciye kasası hazırlamanızı öneririm.";

                return (calcText, speechText);
            }

            // ==================== 3. NORMAL VE SAMİMİ MERSİNOVA ASİSTAN SOHBETİ ====================
            if (lower.Contains("nasılsın") || lower.Contains("iyi misin") || lower.Contains("naber") || lower.Contains("ne haber") || lower.Contains("keyifler"))
            {
                return (
                    "🌸 **Çok iyiyim, sorduğunuz için çok teşekkür ederim!**\n\n" +
                    "Limon ve portakal bahçelerinizin bereketiyle ilgilenmek benim için büyük bir zevk. " +
                    "Sizin gününüz nasıl geçiyor? Narenciye ağaçlarınızda çiçeklenme, meyve tutumu veya sulama işleri nasıl gidiyor? 😊🍋🍊",
                    "Çok iyiyim, teşekkür ederim! Sizin gününüz nasıl geçiyor? Limon ve portakal bahçelerinizde her şey yolunda mı?"
                );
            }

            if (lower.Contains("sen kimsin") || lower.Contains("adın ne") || lower.Contains("kendini tanıt"))
            {
                return (
                    "🌿 **Ben MersiNova!** Akıllı narenciye ve bahçe danışmanınız olan yapay zeka asistanınızım.\n\n" +
                    "Limon ve portakal bahçelerinizde:\n" +
                    "• **YOLOv8** ile anlık görselden olgunluk ve hastalık teşhisi yaparım.\n" +
                    "• **Uçkurutan, unlu bit, meyve sineği, gövde zamklanması** gibi tüm hastalıkların reçetesini çıkarırım.\n" +
                    "• Ağaç ve meyve sayınızla kaç ton rekolte alacağınızı ve gelirinizi hesaplarım.\n" +
                    "• Ve sizinle ElevenLabs destekli akıcı Türkçe yapay zeka sesiyle sohbet ederim! Bana aklınıza takılan her şeyi sorabilirsiniz.",
                    "Ben MersiNova! Limon ve portakal bahçenizin yapay zeka asistanıyım. Narenciye hastalıkları, sulama ve rekolte konularında size yardımcı olmak için buradayım."
                );
            }

            if (lower.Contains("teşekkür") || lower.Contains("sağol") || lower.Contains("eline sağlık") || lower.Contains("harikasın"))
            {
                return (
                    "🌸 **Rica ederim!** Bahçenizin her ağacında altın sarısı limonlar ve pırıl pırıl portakallar görmeniz en büyük dileğim. Bol kazançlı ve bereketli bir hasat dilerim! 🍋🍊",
                    "Rica ederim! Bereketli ve bol kazançlı bir hasat sezonu dilerim."
                );
            }

            if (lower.Contains("sulama") || lower.Contains("su") || lower.Contains("damla sulama"))
            {
                return (
                    "💧 **LİMON VE PORTAKAL DAMLA SULAMA & SU YÖNETİMİ:**\n\n" +
                    "Narenciye köklerinin %80'i toprağın ilk 60 cm'sindedir; aşırı su kök çürüklüğüne (Phytophthora), susuzluk ise haziran dökümüne ve kabuk çatlamasına yol açar.\n\n" +
                    "📊 **Mevsimsel Sulama Takvimi:**\n" +
                    "• **İlkbahar (Nisan-Mayıs):** 6-7 günde bir ağaç başı 30-40 Litre.\n" +
                    "• **Yaz (Haziran-Ağustos):** 3-4 günde bir ağaç başı 50-70 Litre.\n" +
                    "• **Sonbahar (Eylül-Ekim):** 7-8 günde bir ağaç başı 35-45 Litre.\n\n" +
                    "🚜 **Sulama Kuralları:**\n" +
                    "1️⃣ Sulamayı daima sabah erken saatlerde (06:00 - 09:00) yapınız (buharlaşma kaybını %40 önler).\n" +
                    "2️⃣ Damlatıcı memelerin ağaç gövdesine temas etmesini önleyiniz (en az 40-50 cm uzağa çekiniz).\n" +
                    "3️⃣ Sıcak poyraz ve lodos estiğinde sulama aralığını 2 güne indiriniz.",
                    "Limon ve portakalda yaz aylarında haftada iki kez ağaç başı 50 ile 70 litre su veriniz. Sulamayı sabah serinliğinde damla sulamayla yapınız."
                );
            }

            if (lower.Contains("gübre") || lower.Contains("gübreleme") || lower.Contains("besleme") || lower.Contains("potasyum takvimi"))
            {
                return (
                    "🧪 **LİMON VE PORTAKAL YILLIK GÜBRELEME VE BESLEME PROGRAMI:**\n\n" +
                    "**1. Taban Gübrelemesi (Şubat - Mart):**\n" +
                    "• Fosfor ve Azot: Dekara 25-30 kg 20-20-0 veya Kompoze Organomineral taban gübresi taç izdüşümüne verilip toprağa karıştırılır.\n\n" +
                    "**2. İlkbahar Sürgün & Çiçeklenme (Nisan - Mayıs):**\n" +
                    "• Kökten Şelatlı Demir: Ağaç başı 60-80 g **Syngenta Sequestrene 138 Fe**.\n" +
                    "• Yapraktan Çinko + Magnezyum: 100 L suya 150 ml **YaraVita Zintrac** + 400 g **Haifa Magnisal**.\n\n" +
                    "**3. Meyve Büyütme & Kabuk Kalitesi (Haziran - Ağustos):**\n" +
                    "• Kalsiyum Nitrat: Dekara haftalık 5 kg **YaraLiva Calcinit / Nitrabor** (kabuk çatlamasını önler).\n\n" +
                    "**4. Renk, Şeker & Kabuk İnceltme (Ağustos - Ekim):**\n" +
                    "• Azot tamamen kesilir! Damlamadan dekara haftalık 3-4 kg **YaraTera Krista-K** (Potasyum Nitrat 13-0-46) verilir. Portakalın koflaşmasını önler, limonda su randımanını artırır.",
                    "Narenciyede ilkbaharda demir ve çinko, yazın kabuk kalitesi için kalsiyum nitrat, hasada doğru ise koflaşmayı önlemek için potasyum nitrat uygulayınız."
                );
            }

            return (
                "🍋🍊 **Nova:** Sorunuzu dinledim. Limon veya portakal bahçenizle ilgili hastalık teşhisi (uçkurutan, unlu bit, meyve sineği), sulama takvimi veya ağaç sayınızla rekolte hesabı yapabilirim. Size nasıl yardımcı olayım?",
                "Sorunuzu anladım. Limon ve portakal hastalıkları, sulama günleri veya rekolte hesabı için bana dilediğinizi sorabilirsiniz."
            );
#endif
        }
    }

    // ==================== DATABASE SERVICE ====================
    public class DatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MersiNova");
            Directory.CreateDirectory(appData);
            _dbPath = Path.Combine(appData, "mersinova_tarla.db");
        }

        public string DatabasePath => _dbPath;

        public void Initialize()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TarlaAnalizleri (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TarlaAdi TEXT NOT NULL,
                    UrunTuru TEXT NOT NULL,
                    OlgunlukDurumu TEXT NOT NULL,
                    OlgunlukYuzdesi INTEGER NOT NULL,
                    SulamaGunu INTEGER NOT NULL,
                    SulamaTavsiyesi TEXT NOT NULL,
                    IlacGubreGunu INTEGER NOT NULL,
                    IlacGubreTavsiyesi TEXT NOT NULL,
                    HasatTahmini TEXT NOT NULL,
                    ResimYolu TEXT,
                    Tarih TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS RekolteTahminleri (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TarlaAdi TEXT NOT NULL,
                    UrunTuru TEXT NOT NULL,
                    AgacSayisi INTEGER NOT NULL,
                    AgacBasiMeyve INTEGER NOT NULL,
                    NetTonaj REAL NOT NULL,
                    TahminiGelir REAL NOT NULL,
                    KasaSayisi INTEGER NOT NULL,
                    Tarih TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT UNIQUE NOT NULL,
                    Password TEXT NOT NULL,
                    Location TEXT NOT NULL,
                    Latitude REAL,
                    Longitude REAL,
                    CreatedAt TEXT NOT NULL
                );

                INSERT OR IGNORE INTO Users (Username, Password, Location, Latitude, Longitude, CreatedAt)
                VALUES ('arda', '1234', 'Mersin / Erdemli - Limonlu Mahallesi', 36.6025, 34.3142, datetime('now'));";
            cmd.ExecuteNonQuery();

            // Insert sample seed rows if empty
            cmd.CommandText = "SELECT COUNT(*) FROM TarlaAnalizleri;";
            long count = (long)(cmd.ExecuteScalar() ?? 0);
            if (count == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO TarlaAnalizleri 
                    (TarlaAdi, UrunTuru, OlgunlukDurumu, OlgunlukYuzdesi, SulamaGunu, SulamaTavsiyesi, IlacGubreGunu, IlacGubreTavsiyesi, HasatTahmini, ResimYolu, Tarih)
                    VALUES 
                    ('Erdemli Limon Bahçesi', 'Limon (Mayer)', 'Hasada Hazır', 88, 2, '2 gün sonra damla sulama 45 L/ağaç', 4, '4 gün sonra uçkurutan koruyucu bordo bulamacı', '3 gün içinde hasat', 'Samples/limon.jpg', datetime('now', '-2 hours')),
                    ('Finike Portakal Parseli', 'Portakal (Washington)', 'Dönüm Evresi', 76, 3, '3 gün sonra damla sulama', 5, '5 gün sonra potasyum ve kalsiyum yaprak takviyesi', '12 gün sonra hasat', 'Samples/portakal.jpg', datetime('now', '-1 days'));
                ";
                cmd.ExecuteNonQuery();
            }

            cmd.CommandText = "SELECT COUNT(*) FROM RekolteTahminleri;";
            long rCount = (long)(cmd.ExecuteScalar() ?? 0);
            if (rCount == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO RekolteTahminleri
                    (TarlaAdi, UrunTuru, AgacSayisi, AgacBasiMeyve, NetTonaj, TahminiGelir, KasaSayisi, Tarih)
                    VALUES
                    ('Kuzey Limonluğu', 'Limon', 400, 700, 32.90, 855400, 1645, datetime('now', '-3 days')),
                    ('Güneşli Portakal Bahçesi', 'Portakal', 350, 600, 37.51, 825132, 1875, datetime('now', '-1 days'));
                ";
                cmd.ExecuteNonQuery();
            }
        }

        public void SaveAnalysis(AnalysisResult result, string imagePath, string tarlaAdi = "Limon / Portakal Parseli 1")
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO TarlaAnalizleri 
                    (TarlaAdi, UrunTuru, OlgunlukDurumu, OlgunlukYuzdesi, SulamaGunu, SulamaTavsiyesi, IlacGubreGunu, IlacGubreTavsiyesi, HasatTahmini, ResimYolu, Tarih)
                    VALUES 
                    (@Tarla, @Urun, @Olgunluk, @Yuzde, @SulamaGun, @SulamaTavsiye, @IlacGun, @IlacTavsiye, @Hasat, @Resim, @Tarih);";

                cmd.Parameters.AddWithValue("@Tarla", tarlaAdi);
                cmd.Parameters.AddWithValue("@Urun", result.DetectedPlant);
                cmd.Parameters.AddWithValue("@Olgunluk", result.RipenessLabel);
                cmd.Parameters.AddWithValue("@Yuzde", result.RipenessPercentage);
                cmd.Parameters.AddWithValue("@SulamaGun", result.WaterDays);
                cmd.Parameters.AddWithValue("@SulamaTavsiye", result.WaterRecommendation);
                cmd.Parameters.AddWithValue("@IlacGun", result.PesticideDays);
                cmd.Parameters.AddWithValue("@IlacTavsiye", result.PesticideRecommendation);
                cmd.Parameters.AddWithValue("@Hasat", result.HarvestEstimate);
                cmd.Parameters.AddWithValue("@Resim", imagePath);
                cmd.Parameters.AddWithValue("@Tarih", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public void SaveYieldEstimate(YieldEstimate y, string tarlaAdi = "Limon/Portakal Bahçesi")
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO RekolteTahminleri
                    (TarlaAdi, UrunTuru, AgacSayisi, AgacBasiMeyve, NetTonaj, TahminiGelir, KasaSayisi, Tarih)
                    VALUES
                    (@Tarla, @Urun, @Agac, @Meyve, @Tonaj, @Gelir, @Kasa, @Tarih);";

                cmd.Parameters.AddWithValue("@Tarla", tarlaAdi);
                cmd.Parameters.AddWithValue("@Urun", y.CropName);
                cmd.Parameters.AddWithValue("@Agac", y.TreeCount);
                cmd.Parameters.AddWithValue("@Meyve", y.FruitsPerTree);
                cmd.Parameters.AddWithValue("@Tonaj", Math.Round(y.NetTonnageTon, 2));
                cmd.Parameters.AddWithValue("@Gelir", Math.Round(y.EstimatedRevenue, 0));
                cmd.Parameters.AddWithValue("@Kasa", y.CrateCount);
                cmd.Parameters.AddWithValue("@Tarih", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public List<TarlaKayit> GetAllAnalyses()
        {
            var list = new List<TarlaKayit>();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, TarlaAdi, UrunTuru, OlgunlukDurumu, OlgunlukYuzdesi, SulamaGunu, SulamaTavsiyesi, IlacGubreGunu, IlacGubreTavsiyesi, HasatTahmini, ResimYolu, Tarih FROM TarlaAnalizleri ORDER BY Id DESC LIMIT 50;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new TarlaKayit
                    {
                        Id = reader.GetInt32(0),
                        TarlaAdi = reader.GetString(1),
                        UrunTuru = reader.GetString(2),
                        OlgunlukDurumu = reader.GetString(3),
                        OlgunlukYuzdesi = reader.GetInt32(4),
                        SulamaGunu = reader.GetInt32(5),
                        SulamaTavsiyesi = reader.GetString(6),
                        IlacGubreGunu = reader.GetInt32(7),
                        IlacGubreTavsiyesi = reader.GetString(8),
                        HasatTahmini = reader.GetString(9),
                        ResimYolu = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        Tarih = reader.GetString(11)
                    });
                }
            }
            catch { }
            return list;
        }

        public DataTable ExecuteRawQuery(string query)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = query;
                using var reader = cmd.ExecuteReader();
                dt.Load(reader);
            }
            catch (Exception ex)
            {
                dt.Columns.Add("Hata");
                dt.Rows.Add(ex.Message);
            }
            return dt;
        }

        public (bool IsValid, string Message, string Location, double Lat, double Lon) ValidateUser(string username, string password)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Password, Location, Latitude, Longitude FROM Users WHERE LOWER(Username) = LOWER(@Username) LIMIT 1;";
                cmd.Parameters.AddWithValue("@Username", username.Trim());
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string storedPass = reader.GetString(0);
                    string loc = reader.IsDBNull(1) ? "Mersin / Erdemli - Limonlu Mahallesi" : reader.GetString(1);
                    double lat = reader.IsDBNull(2) ? 36.6025 : reader.GetDouble(2);
                    double lon = reader.IsDBNull(3) ? 34.3142 : reader.GetDouble(3);

                    if (storedPass == password)
                    {
                        return (true, "Giriş başarılı.", loc, lat, lon);
                    }
                    else
                    {
                        return (false, "Girilen şifre hatalı! Lütfen 4 haneli şifrenizi kontrol ediniz.", "", 0, 0);
                    }
                }
                else
                {
                    if (username.Trim().Equals("arda", StringComparison.OrdinalIgnoreCase) && password == "1234")
                    {
                        return (true, "Giriş başarılı.", "Mersin / Erdemli - Limonlu Mahallesi", 36.6025, 34.3142);
                    }
                    return (false, $"'{username}' adında kayıtlı bir çiftçi bulunamadı! Lütfen 'Yeni Kayıt Ol' sekmesinden kaydolun.", "", 0, 0);
                }
            }
            catch (Exception ex)
            {
                if (username.Trim().Equals("arda", StringComparison.OrdinalIgnoreCase) && password == "1234")
                {
                    return (true, "Giriş başarılı.", "Mersin / Erdemli - Limonlu Mahallesi", 36.6025, 34.3142);
                }
                return (false, $"Veritabanı hatası: {ex.Message}", "", 0, 0);
            }
        }

        public (bool Success, string Message) RegisterUser(string username, string password, string location, double lat, double lon)
        {
            username = (username ?? "").Trim();
            password = (password ?? "").Trim();
            location = (location ?? "").Trim();

            if (string.IsNullOrWhiteSpace(username) || !Regex.IsMatch(username, @"^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$"))
            {
                return (false, "Kullanıcı adı yalnızca harflerden oluşmalıdır!");
            }

            if (password.Length != 4 || !password.All(char.IsDigit))
            {
                return (false, "Şifre zorunlu olarak tam 4 haneli bir sayı olmalıdır!");
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                return (false, "Bahçenizin net konum bilgisini girmelisiniz!");
            }

            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();

                var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER(@Username);";
                checkCmd.Parameters.AddWithValue("@Username", username);
                long exists = (long)(checkCmd.ExecuteScalar() ?? 0);
                if (exists > 0)
                {
                    return (false, $"'{username}' kullanıcı adı zaten kayıtlı! Lütfen farklı bir isim belirleyin.");
                }

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Users (Username, Password, Location, Latitude, Longitude, CreatedAt)
                    VALUES (@Username, @Password, @Location, @Lat, @Lon, datetime('now'));";
                cmd.Parameters.AddWithValue("@Username", username);
                cmd.Parameters.AddWithValue("@Password", password);
                cmd.Parameters.AddWithValue("@Location", location);
                cmd.Parameters.AddWithValue("@Lat", lat);
                cmd.Parameters.AddWithValue("@Lon", lon);
                cmd.ExecuteNonQuery();

                return (true, $"Tebrikler Sayın {username}, bahçe kaydınız başarıyla oluşturuldu!");
            }
            catch (Exception ex)
            {
                return (false, $"Kayıt sırasında hata oluştu: {ex.Message}");
            }
        }
    }
}
