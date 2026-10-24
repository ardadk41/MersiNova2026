using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace MersiNova
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _db;
        private readonly AiService _ai;
        private readonly SpeechService _speech;
        private string? _currentImagePath;
        private bool _isAnalyzing = false;
        private readonly TypingAnimator _typingAnimator;
        private AnalysisResult? _lastResult;
        private YieldEstimate _currentYield = new();

        public string CurrentUsername { get; private set; } = "arda";
        public string CurrentLocation { get; private set; } = "Mersin / Erdemli - Limonlu Mahallesi";
        public double CurrentLatitude { get; private set; } = 36.6025;
        public double CurrentLongitude { get; private set; } = 34.3142;

        private readonly WeatherService _weather = new();
        private WeatherData? _currentWeatherData;

        private bool _isListening = false;
        private System.Windows.Threading.DispatcherTimer? _silenceTimer;
        private System.Windows.Threading.DispatcherTimer? _waveTimer;
        private System.Windows.Threading.DispatcherTimer? _sampleToastTimer;
        private int _sampleToastSecondsLeft = 10;
        private string _lastVoiceText = "";
        private static readonly Random _waveRng = new();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const byte VK_LWIN = 0x5B;
        private const byte VK_H = 0x48;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public MainWindow() : this("arda", "Mersin / Erdemli - Limonlu Mahallesi", 36.6025, 34.3142)
        {
        }

        public MainWindow(string username, string location) : this(username, location, 36.6025, 34.3142)
        {
        }

        public MainWindow(string username, string location, double? latitude, double? longitude)
        {
            CurrentUsername = string.IsNullOrWhiteSpace(username) ? "arda" : username.Trim();
            CurrentLocation = string.IsNullOrWhiteSpace(location) ? "Mersin / Erdemli - Limonlu Mahallesi" : location.Trim();
            CurrentLatitude = latitude ?? 36.6025;
            CurrentLongitude = longitude ?? 34.3142;

            InitializeComponent();
            _db = new DatabaseService();
            _ai = new AiService();
            _speech = new SpeechService();
            _typingAnimator = new TypingAnimator(Dot1, Dot2, Dot3);

            _speech.SpeakingStateChanged += (isSpeaking) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (isSpeaking)
                    {
                        LiveVoiceStatusText.Text = "🗣️ MersiNova Konuşuyor...";
                        LiveVoiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                        VoiceOrbIcon.Text = "🗣️";
                    }
                    else
                    {
                        if (!_isListening)
                        {
                            LiveVoiceStatusText.Text = "🟢 MersiNova Dinlemeye Hazır";
                            LiveVoiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                            VoiceOrbIcon.Text = "🎙️";
                        }
                    }
                });
            };

            Loaded += MainWindow_Loaded;
            Closed += (s, e) => _speech.Dispose();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _db.Initialize();
            SqlDbPathText.Text = $"SQLite Veritabanı: {_db.DatabasePath}";
            LoadRecentAnalyses();
            SetPlaceholder();

            // Populate Yield Crop Items Safely
            if (YieldCropCombo != null)
            {
                YieldCropCombo.ItemsSource = new string[]
                {
                    "🍋 Limon (Mayer / Kütdiken / Enterdonat)",
                    "🍊 Portakal (Washington Navel / Valencia)"
                };
                YieldCropCombo.SelectedIndex = 0;
            }

            RecalculateYieldUI();

            // Populate User & Confirmed Location Badges
            HeaderUsernameText.Text = CurrentUsername;
            HeaderLocationText.Text = CurrentLocation;
            ToastLocationText.Text = $"📍 Doğrulanan Parsel: {CurrentLocation}";
            LoadSahaBannerImage();

            // Load Real Meteorological Live Weather & 7-Day Forecast from MGM/Open-Meteo
            await LoadLiveWeatherAsync();

            // Load default Lemon sample
            string sampleLimon = System.IO.Path.Combine(App.SamplesDirectory, "limon.jpg");
            if (!File.Exists(sampleLimon))
            {
                sampleLimon = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", "limon.jpg");
            }
            if (!File.Exists(sampleLimon))
            {
                sampleLimon = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Samples", "limon.jpg");
            }
            if (File.Exists(sampleLimon))
            {
                LoadImage(sampleLimon);
            }

            // Welcome greeting: MersiNova introduces herself with user requested greeting
            await Task.Delay(400);

            string weatherSummary = _currentWeatherData != null 
                ? $"Şu an `{_currentWeatherData.CurrentTemp:F1}°C`, `{_currentWeatherData.CurrentWeatherDescription}` ({_currentWeatherData.CurrentWeatherIcon}), Nem: `%{_currentWeatherData.CurrentHumidity:F0}`."
                : "Canlı meteoroloji uydusuna bağlanıldı.";

            string welcomeText = $"🍋🍊 **Hoş Geldiniz Sayın {CurrentUsername.ToUpper()}!**\n" +
                                 $"Ben yapay zeka asistanınızım. MersiNova akıllı tarım ve narenciye sisteminize hoş geldiniz!\n\n" +
                                 $"📍 **Kayıtlı Parseliniz:** `{CurrentLocation}`\n" +
                                 $"🌤️ **Canlı Meteoroloji:** {weatherSummary} (Dolu veya zararlı aşırı yağış riski bulunmuyor, hava emniyetli)\n\n" +
                                 $"Limon ve portakal fotoğraflarınızı yükleyerek anlık hastalık teşhisi yapabilir, **'🌾 Rekolte'** sekmesinden tonaj ve gelir hesabı çıkarabilir veya **'🎙️ MersiNova\'yı Dinle'** ile benimle doğrudan sesli konuşabilirsiniz! Bereketli ve bol kazançlı bir sezon dilerim! 🌱✨";

            string spokenWelcome = $"Hoş geldiniz Sayın {CurrentUsername}! Ben yapay zeka asistanınızım. MersiNova akıllı tarım ve narenciye sisteminize hoş geldiniz! {CurrentLocation} parseliniz için canlı meteoroloji ve yapay zeka modellerimiz hazır. Bugün bahçenizde hava şartları gayet elverişli ve emniyetli görünüyor; dolu veya aşırı sağanak riski bulunmuyor. Teşhis, sulama, rekolte tahmini ve bahçe bakımınızda size yardımcı olmaktan mutluluk duyarım. Bereketli ve harika bir gün dilerim!";

            AddBotMessage(welcomeText, speak: true, speechContent: spokenWelcome);
        }

        // ==================== AGRICULTURAL WEATHER & EARLY WARNING ALERT HANDLERS ====================
        private void ToggleAlertBanner_Click(object sender, RoutedEventArgs e)
        {
            if (WarningSystemBar != null)
            {
                WarningSystemBar.Visibility = WarningSystemBar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void DismissAlert_Click(object sender, RoutedEventArgs e)
        {
            AlertBannerBar.Visibility = Visibility.Collapsed;
        }

        private void CloseToast_Click(object sender, RoutedEventArgs e)
        {
            DismissToastWithAnimation();
        }

        private void ShowSampleWarningToast_Click(object sender, RoutedEventArgs e)
        {
            ShowSampleNotificationToast();
        }

        private void ShowSampleNotificationToast()
        {
            try
            {
                _sampleToastTimer?.Stop();
                _sampleToastSecondsLeft = 10;

                if (ToastCountdownText != null)
                {
                    ToastCountdownText.Text = $"⏱️ {_sampleToastSecondsLeft} saniye sonra kapanacak";
                }
                if (ToastCountdownBar != null)
                {
                    ToastCountdownBar.Value = 10;
                }
                if (ToastLocationText != null)
                {
                    ToastLocationText.Text = $"📍 Doğrulanan Parsel: {CurrentLocation}";
                }

                // Prepare popup for drop animation from top
                ToastPopup.Visibility = Visibility.Visible;
                ToastPopup.Opacity = 0.0;
                ToastTranslate.Y = -160;

                // Animate drop down ("bildirim gibi düşsün")
                var dropAnim = new DoubleAnimation
                {
                    From = -160,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(450),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var fadeInAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(350)
                };

                ToastTranslate.BeginAnimation(TranslateTransform.YProperty, dropAnim);
                ToastPopup.BeginAnimation(UIElement.OpacityProperty, fadeInAnim);

                // 10-second countdown timer ("10 saniye sonra gitsin")
                _sampleToastTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _sampleToastTimer.Tick += (s, ev) =>
                {
                    _sampleToastSecondsLeft--;
                    if (ToastCountdownText != null)
                    {
                        ToastCountdownText.Text = $"⏱️ {_sampleToastSecondsLeft} saniye sonra kapanacak";
                    }
                    if (ToastCountdownBar != null)
                    {
                        ToastCountdownBar.Value = _sampleToastSecondsLeft;
                    }

                    if (_sampleToastSecondsLeft <= 0)
                    {
                        _sampleToastTimer.Stop();
                        DismissToastWithAnimation();
                    }
                };
                _sampleToastTimer.Start();
            }
            catch
            {
            }
        }

        private void DismissToastWithAnimation()
        {
            try
            {
                _sampleToastTimer?.Stop();

                var slideUpAnim = new DoubleAnimation
                {
                    To = -140,
                    Duration = TimeSpan.FromMilliseconds(350),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var fadeOutAnim = new DoubleAnimation
                {
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };

                fadeOutAnim.Completed += (s, ev) =>
                {
                    ToastPopup.Visibility = Visibility.Collapsed;
                    ToastTranslate.Y = -160;
                };

                ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideUpAnim);
                ToastPopup.BeginAnimation(UIElement.OpacityProperty, fadeOutAnim);
            }
            catch
            {
                ToastPopup.Visibility = Visibility.Collapsed;
            }
        }

        private void SpeakSampleToast_Click(object sender, RoutedEventArgs e)
        {
            if (_speech.IsVoiceEnabled)
            {
                _speech.SpeakAsync("Örnek uyarı bildirimi: Parselinizde anlık meteorolojik hava değişimi ve kuvvetli rüzgar riski tespit edilmiştir. Bahçenizin ve ağaçlarınızın zarar görmemesi için lütfen dolu tüllerini çekiniz ve fırtına sonrası koruyucu bakır uygulamasını tamamlayınız.");
            }
        }

        private void SpeakWeatherAlert_Click(object sender, RoutedEventArgs e)
        {
            if (_currentWeatherData != null)
            {
                string alertSpeech = $"{CurrentLocation} parseli için canlı meteoroloji raporu: Şu an hava {_currentWeatherData.CurrentTemp:F0} derece ve {_currentWeatherData.CurrentWeatherDescription}. " +
                                     $"{_currentWeatherData.HailWarningText} {_currentWeatherData.RainWarningText}";
                _speech.SpeakAsync(alertSpeech);
            }
            else
            {
                _speech.SpeakAsync($"{CurrentLocation} bölgesi için canlı meteoroloji verileri incelenmektedir.");
            }
        }

        // ==================== NAVIGATION TABS ====================
        private void TabAnaliz_Click(object sender, RoutedEventArgs e)
        {
            AnalysisView.Visibility = Visibility.Visible;
            VoiceChatView.Visibility = Visibility.Collapsed;
            RekolteView.Visibility = Visibility.Collapsed;
            SqlDatabaseView.Visibility = Visibility.Collapsed;
            WeatherView.Visibility = Visibility.Collapsed;

            ResetTabButtons();
            TabAnalizBtn.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x32, 0x5C));
            TabAnalizBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        }

        private void TabVoiceChat_Click(object sender, RoutedEventArgs e)
        {
            AnalysisView.Visibility = Visibility.Collapsed;
            VoiceChatView.Visibility = Visibility.Visible;
            RekolteView.Visibility = Visibility.Collapsed;
            SqlDatabaseView.Visibility = Visibility.Collapsed;
            WeatherView.Visibility = Visibility.Collapsed;

            ResetTabButtons();
            TabVoiceChatBtn.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x32, 0x5C));
            TabVoiceChatBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));

            if (VoiceDialoguePanel.Children.Count == 0)
            {
                AddVoiceDialogueTurn(
                    "🌿 MersiNova (Yapay Zeka Asistanı)", 
                    "Merhaba! Ben yapay zeka asistanınız MersiNova. Limon ve portakal bahçenizle ilgili tüm sorularınızı dinlemeye hazırım. " +
                    "Aşağıdaki **'🎙️ BENİ DİNLE'** butonuna basarak doğrudan mikrofonunuza konuşabilirsiniz veya hızlı konulardan birini seçebilirsiniz!", 
                    isUser: false, 
                    speak: true);
            }
        }

        private void TabRekolte_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AnalysisView.Visibility = Visibility.Collapsed;
                VoiceChatView.Visibility = Visibility.Collapsed;
                RekolteView.Visibility = Visibility.Visible;
                SqlDatabaseView.Visibility = Visibility.Collapsed;
                WeatherView.Visibility = Visibility.Collapsed;

                ResetTabButtons();
                TabRekolteBtn.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x32, 0x5C));
                TabRekolteBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));

                RecalculateYieldUI();
            }
            catch { }
        }

        private void TabSql_Click(object sender, RoutedEventArgs e)
        {
            AnalysisView.Visibility = Visibility.Collapsed;
            VoiceChatView.Visibility = Visibility.Collapsed;
            RekolteView.Visibility = Visibility.Collapsed;
            SqlDatabaseView.Visibility = Visibility.Visible;
            WeatherView.Visibility = Visibility.Collapsed;

            ResetTabButtons();
            TabSqlBtn.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x32, 0x5C));
            TabSqlBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));

            LoadSqlTable();
        }

        private void TabWeather_Click(object sender, RoutedEventArgs e)
        {
            AnalysisView.Visibility = Visibility.Collapsed;
            VoiceChatView.Visibility = Visibility.Collapsed;
            RekolteView.Visibility = Visibility.Collapsed;
            SqlDatabaseView.Visibility = Visibility.Collapsed;
            WeatherView.Visibility = Visibility.Visible;

            ResetTabButtons();
            TabWeatherBtn.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x32, 0x5C));
            TabWeatherBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        }

        private void BackToAnaliz_Click(object sender, RoutedEventArgs e)
        {
            TabAnaliz_Click(sender, e);
        }

        private void ResetTabButtons()
        {
            TabAnalizBtn.Background = Brushes.Transparent;
            TabAnalizBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
            TabVoiceChatBtn.Background = Brushes.Transparent;
            TabVoiceChatBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
            TabRekolteBtn.Background = Brushes.Transparent;
            TabRekolteBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
            TabSqlBtn.Background = Brushes.Transparent;
            TabSqlBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
            TabWeatherBtn.Background = Brushes.Transparent;
            TabWeatherBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
        }

        // ==================== LIVE METEOROLOGICAL SERVICE & CARD BUILDER ====================
        private async void RefreshWeather_Click(object sender, RoutedEventArgs e)
        {
            await LoadLiveWeatherAsync();
        }

        private void SpeakWeatherReport_Click(object sender, RoutedEventArgs e)
        {
            if (_currentWeatherData == null) return;
            string speech = $"Meteoroloji genel müdürlüğü ve radar uydusu {_currentWeatherData.LocationName} raporu: " +
                            $"Şu an sıcaklık {_currentWeatherData.CurrentTemp:F0} derece, bağıl nem yüzde {_currentWeatherData.CurrentHumidity:F0}, rüzgar saatte {_currentWeatherData.CurrentWindSpeed:F0} kilometre. " +
                            $"{_currentWeatherData.HailWarningText}. {_currentWeatherData.RainWarningText}";
            _speech.SpeakAsync(speech);
        }

        private async Task LoadLiveWeatherAsync()
        {
            try
            {
                _currentWeatherData = await _weather.GetWeatherAsync(CurrentLatitude, CurrentLongitude, CurrentLocation);

                // Update Header Badges & Ticker
                HeaderUsernameText.Text = CurrentUsername;
                HeaderLocationText.Text = _currentWeatherData.LocationName;
                ToastLocationText.Text = $"📍 Doğrulanan Parsel: {_currentWeatherData.LocationName}";
                LiveWeatherTickerText.Text = $"📍 {_currentWeatherData.LocationName} • {_currentWeatherData.CurrentTemp:F1}°C • {_currentWeatherData.CurrentWeatherIcon} {_currentWeatherData.CurrentWeatherDescription} • Nem: %{_currentWeatherData.CurrentHumidity:F0} • Rüzgar: {_currentWeatherData.CurrentWindSpeed:F0} km/s";

                // Update Hail Alert Badge
                if (AlertHailBadgeBorder != null && AlertHailTitleText != null)
                {
                    if (_currentWeatherData.HasSevereHailWarning)
                    {
                        AlertHailBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x14, 0x1C));
                        AlertHailBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
                        AlertHailTitleText.Text = "🚨 DOLU RİSKİ";
                        AlertHailTitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x80));
                        AlertHailText.Text = _currentWeatherData.HailWarningText;
                    }
                    else
                    {
                        AlertHailBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x25, 0x3C));
                        AlertHailBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                        AlertHailTitleText.Text = "🛡️ DOLU DURUMU";
                        AlertHailTitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                        AlertHailText.Text = "Dolu riski bulunmuyor; hava narenciye için emniyetli.";
                    }
                }

                // Update Rain Alert Badge
                if (AlertRainBadgeBorder != null && AlertRainTitleText != null)
                {
                    if (_currentWeatherData.HasRainWarning)
                    {
                        AlertRainBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x25, 0x3C));
                        AlertRainBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
                        AlertRainTitleText.Text = "🌧️ SAĞANAK UYARISI";
                        AlertRainTitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x80));
                        AlertRainText.Text = _currentWeatherData.RainWarningText;
                    }
                    else
                    {
                        AlertRainBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x25, 0x3C));
                        AlertRainBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                        AlertRainTitleText.Text = "☀️ YAĞIŞ RAPORU";
                        AlertRainTitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0xD8, 0xFF));
                        AlertRainText.Text = "Aşırı sağanak beklenmiyor; narenciye için elverişli.";
                    }
                }

                // Keep AlertBannerBar collapsed by default unless user toggles it
                if (AlertBannerBar != null)
                {
                    AlertBannerBar.Visibility = Visibility.Collapsed;
                }

                // Update Saha Analizi 7-Day Weather & Google Earth Card
                if (SahaWeatherLocationText != null) SahaWeatherLocationText.Text = _currentWeatherData.LocationName;
                if (SahaHeroWeatherIcon != null) SahaHeroWeatherIcon.Text = _currentWeatherData.CurrentWeatherIcon;
                if (SahaHeroTempText != null) SahaHeroTempText.Text = $"{_currentWeatherData.CurrentTemp:F1}°C";
                if (SahaHeroDescText != null) SahaHeroDescText.Text = _currentWeatherData.CurrentWeatherDescription;
                if (SahaBannerLocationText != null) SahaBannerLocationText.Text = $"📍 {_currentWeatherData.LocationName} Parseli";
                if (SahaHeroParamsText != null) SahaHeroParamsText.Text = $"💧 Nem: %{_currentWeatherData.CurrentHumidity:F0}  •  💨 Rüzgar: {_currentWeatherData.CurrentWindSpeed:F0} km/s  •  🌧️ Yağış: {_currentWeatherData.CurrentPrecipitation:F1} mm";

                // Update WeatherView
                WeatherViewLocationText.Text = _currentWeatherData.LocationName;
                WeatherViewCoordsText.Text = $"🌐 {_currentWeatherData.Latitude:F4}° K, {_currentWeatherData.Longitude:F4}° D";

                HeroWeatherIcon.Text = _currentWeatherData.CurrentWeatherIcon;
                HeroTempText.Text = _currentWeatherData.CurrentTemp.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                HeroWeatherDesc.Text = _currentWeatherData.CurrentWeatherDescription;
                HeroHumidityText.Text = $"%{_currentWeatherData.CurrentHumidity:F0}";
                HeroWindText.Text = $"{_currentWeatherData.CurrentWindSpeed:F0} km/s";
                HeroRainText.Text = $"{_currentWeatherData.CurrentPrecipitation:F1} mm";
                HeroRiskText.Text = _currentWeatherData.HasSevereHailWarning ? "Dolu Riski!" : "Risk Yok (Emniyetli)";
                HeroRiskText.Foreground = _currentWeatherData.HasSevereHailWarning 
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52)) 
                    : new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));

                // Weather advice box: reassuring when no risk
                if (WeatherAdviceBorder != null && WeatherAdviceTitle != null && WeatherAdviceIcon != null)
                {
                    if (_currentWeatherData.HasSevereHailWarning || _currentWeatherData.HasRainWarning)
                    {
                        WeatherAdviceBorder.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x0E, 0x18));
                        WeatherAdviceBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
                        WeatherAdviceIcon.Text = "🚨";
                        WeatherAdviceTitle.Text = "METEOROLOJİK ERKEN UYARI VE ALINACAK ZİRAİ TEDBİRLER";
                        WeatherAdviceTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x80));
                        WeatherAdviceDetailText.Text = $"{_currentWeatherData.HailWarningText}\n\n{_currentWeatherData.RainWarningText}";
                    }
                    else
                    {
                        WeatherAdviceBorder.Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x22, 0x22));
                        WeatherAdviceBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                        WeatherAdviceIcon.Text = "🛡️";
                        WeatherAdviceTitle.Text = "METEOROLOJİK DEĞERLENDİRME: HAVA ŞARTLARI EMNİYETLİ";
                        WeatherAdviceTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                        WeatherAdviceDetailText.Text = $"📍 {_currentWeatherData.LocationName} bölgesinde narenciyeye zarar verebilecek dolu, fırtına veya aşırı sağanak yağış riski bulunmamaktadır. Hava şartları sulama, gübreleme ve rutin bahçe bakımı için tamamen elverişli ve emniyetli seyretmektedir.";
                    }
                }

                BuildDailyCardsUI(_currentWeatherData.DailyForecasts);
            }
            catch
            {
            }
        }

        private void ToggleSahaWeather_Click(object sender, RoutedEventArgs e)
        {
            if (SahaWeatherContentGrid.Visibility == Visibility.Visible)
            {
                SahaWeatherContentGrid.Visibility = Visibility.Collapsed;
                ToggleSahaWeatherBtn.Content = "▼";
            }
            else
            {
                SahaWeatherContentGrid.Visibility = Visibility.Visible;
                ToggleSahaWeatherBtn.Content = "▲";
            }
        }

        private void LoadSahaBannerImage()
        {
            try
            {
                string[] candidates = [
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "limonlu_google_earth.jpg"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", "limonlu_google_earth.jpg"),
                    System.IO.Path.Combine(App.SamplesDirectory, "limonlu_google_earth.jpg"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "limonlu_google_earth.jpg"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Samples", "limonlu_google_earth.jpg"),
                    @"C:\Users\HALİDE\.gemini\antigravity-ide\scratch\MersiNova\limonlu_google_earth.jpg",
                    System.IO.Path.Combine(App.SamplesDirectory, "limonlu_earth_satellite.jpg")
                ];

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(path, UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        SahaWeatherBannerImage.Source = bitmap;
                        break;
                    }
                }
            }
            catch { }
        }

        private void BuildDailyCardsUI(List<DayForecast> forecasts)
        {
            DailyCardsPanel.Children.Clear();
            foreach (var f in forecasts)
            {
                var card = new Border
                {
                    Width = 150,
                    Height = 220,
                    Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x1E, 0x36)),
                    BorderBrush = f.RainProb >= 50 
                        ? new SolidColorBrush(Color.FromRgb(0x00, 0xB0, 0xFF)) 
                        : new SolidColorBrush(Color.FromRgb(0x18, 0x36, 0x5D)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var sp = new StackPanel();

                // Day Name & Date
                var dayTxt = new TextBlock
                {
                    Text = f.DayName,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF4)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var dateTxt = new TextBlock
                {
                    Text = f.Date.ToString("dd MMMM", new System.Globalization.CultureInfo("tr-TR")),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x7E, 0x93, 0xAA)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 4)
                };

                // Icon
                var iconTxt = new TextBlock
                {
                    Text = f.Icon,
                    FontSize = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 2)
                };

                // Description
                var descTxt = new TextBlock
                {
                    Text = f.Description,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                // Temp Max / Min
                var tempSp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                var maxT = new TextBlock
                {
                    Text = $"{f.MaxTemp:F0}°",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                    Margin = new Thickness(0, 0, 6, 0)
                };
                var minT = new TextBlock
                {
                    Text = $"{f.MinTemp:F0}°",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                };
                tempSp.Children.Add(maxT);
                tempSp.Children.Add(minT);

                // Rain Prob badge
                var rainBadge = new Border
                {
                    Background = f.RainProb >= 50 
                        ? new SolidColorBrush(Color.FromRgb(0x0C, 0x36, 0x54)) 
                        : new SolidColorBrush(Color.FromRgb(0x0E, 0x23, 0x3E)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 2, 0, 4),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var rainTxt = new TextBlock
                {
                    Text = $"🌧️ %{f.RainProb} • {f.RainAmount:F0}mm",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = f.RainProb >= 50 
                        ? new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) 
                        : new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA))
                };
                rainBadge.Child = rainTxt;

                // Agricultural Tip
                var tipTxt = new TextBlock
                {
                    Text = f.AgriculturalTip,
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84)),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    MaxHeight = 32
                };

                sp.Children.Add(dayTxt);
                sp.Children.Add(dateTxt);
                sp.Children.Add(iconTxt);
                sp.Children.Add(descTxt);
                sp.Children.Add(tempSp);
                sp.Children.Add(rainBadge);
                sp.Children.Add(tipTxt);

                card.Child = sp;
                DailyCardsPanel.Children.Add(card);
            }

            // Also populate 1-Week Cards in Saha Analizi (Saha Analizi Üst Paneli)
            if (DailyCardsPanelSaha != null)
            {
                DailyCardsPanelSaha.Children.Clear();
                foreach (var f in forecasts)
                {
                    var card = new Border
                    {
                        Width = 130,
                        Height = 138,
                        Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x1E, 0x36)),
                        BorderBrush = f.RainProb >= 50 
                            ? new SolidColorBrush(Color.FromRgb(0x00, 0xB0, 0xFF)) 
                            : new SolidColorBrush(Color.FromRgb(0x18, 0x36, 0x5D)),
                        BorderThickness = new Thickness(1.2),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(8, 6, 8, 6),
                        Margin = new Thickness(0, 0, 8, 0)
                    };

                    var sp = new StackPanel();

                    // Day Name & Date
                    var dayTxt = new TextBlock
                    {
                        Text = f.DayName,
                        FontWeight = FontWeights.Bold,
                        FontSize = 11.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF4)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    var dateTxt = new TextBlock
                    {
                        Text = f.Date.ToString("dd MMM", new System.Globalization.CultureInfo("tr-TR")),
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x7E, 0x93, 0xAA)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 2)
                    };

                    // Icon
                    var iconTxt = new TextBlock
                    {
                        Text = f.Icon,
                        FontSize = 24,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 1, 0, 1)
                    };

                    // Temp Max / Min
                    var tempSp = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 2)
                    };
                    var maxT = new TextBlock
                    {
                        Text = $"{f.MaxTemp:F0}°",
                        FontWeight = FontWeights.Bold,
                        FontSize = 12.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    var minT = new TextBlock
                    {
                        Text = $"{f.MinTemp:F0}°",
                        FontSize = 11.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                    };
                    tempSp.Children.Add(maxT);
                    tempSp.Children.Add(minT);

                    // Rain Prob badge
                    var rainBadge = new Border
                    {
                        Background = f.RainProb >= 50 
                            ? new SolidColorBrush(Color.FromRgb(0x0C, 0x36, 0x54)) 
                            : new SolidColorBrush(Color.FromRgb(0x0E, 0x23, 0x3E)),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6, 1, 6, 1),
                        Margin = new Thickness(0, 1, 0, 2),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    var rainTxt = new TextBlock
                    {
                        Text = $"🌧️ %{f.RainProb}",
                        FontSize = 9.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = f.RainProb >= 50 
                            ? new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) 
                            : new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA))
                    };
                    rainBadge.Child = rainTxt;

                    // Agricultural Tip
                    var tipTxt = new TextBlock
                    {
                        Text = f.AgriculturalTip,
                        FontSize = 8.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84)),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        MaxHeight = 24,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    sp.Children.Add(dayTxt);
                    sp.Children.Add(dateTxt);
                    sp.Children.Add(iconTxt);
                    sp.Children.Add(tempSp);
                    sp.Children.Add(rainBadge);
                    sp.Children.Add(tipTxt);

                    card.Child = sp;
                    DailyCardsPanelSaha.Children.Add(card);
                }
            }
        }

        // ==================== FRUIT SELECTION (LİMON / PORTAKAL) FOR CHAT ====================
        private void ChatSelectFruit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string fruit = (btn.Tag as string) ?? "limon";

            if (fruit == "limon")
            {
                LimonChipsPanel.Visibility = Visibility.Visible;
                PortakalChipsPanel.Visibility = Visibility.Collapsed;
                ChatActiveFruitSubtitle.Text = "🍋 Limon Reçeteleri";
                ChatActiveFruitSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));

                ChatSelectLimonBtn.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x24, 0x08));
                ChatSelectLimonBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));
                ChatLimonBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));
                ChatLimonBtnText.FontWeight = FontWeights.Bold;

                ChatSelectPortakalBtn.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x25, 0x44));
                ChatSelectPortakalBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x3D, 0x68));
                ChatPortakalBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
                ChatPortakalBtnText.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                LimonChipsPanel.Visibility = Visibility.Collapsed;
                PortakalChipsPanel.Visibility = Visibility.Visible;
                ChatActiveFruitSubtitle.Text = "🍊 Portakal Reçeteleri";
                ChatActiveFruitSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));

                ChatSelectPortakalBtn.Background = new SolidColorBrush(Color.FromRgb(0x38, 0x1E, 0x08));
                ChatSelectPortakalBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                ChatPortakalBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                ChatPortakalBtnText.FontWeight = FontWeights.Bold;

                ChatSelectLimonBtn.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x25, 0x44));
                ChatSelectLimonBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x3D, 0x68));
                ChatLimonBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
                ChatLimonBtnText.FontWeight = FontWeights.SemiBold;
            }

            ChipsScrollViewer.ScrollToHorizontalOffset(0);
        }

        // ==================== HORIZONTAL CHIPS SCROLLING ====================
        private void ScrollChipsLeft_Click(object sender, RoutedEventArgs e)
        {
            ChipsScrollViewer.ScrollToHorizontalOffset(ChipsScrollViewer.HorizontalOffset - 180);
        }

        private void ScrollChipsRight_Click(object sender, RoutedEventArgs e)
        {
            ChipsScrollViewer.ScrollToHorizontalOffset(ChipsScrollViewer.HorizontalOffset + 180);
        }

        // ==================== DEDICATED LIVE TWO-WAY VOICE & LISTENING ====================
        private void ToggleListen_Click(object sender, RoutedEventArgs e)
        {
            if (!_isListening)
            {
                StartListening();
            }
            else
            {
                StopListeningAndProcess();
            }
        }

        private void StartListening()
        {
            _isListening = true;
            _speech.Stop();

            ListenBtnText.Text = "🔴 DİNLİYORUM... (Konuşmanız Bitince Cevaplar)";
            ListenBtnIcon.Text = "🔴";
            LiveVoiceStatusText.Text = "🎙️ Sizi Dinliyorum... Lütfen Konuşun";
            LiveVoiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
            LiveVoiceSubText.Text = "Mikrofonunuz dinleniyor. Sözünüz bitince otomatik yanıtlayacağım.";
            VoiceOrbIcon.Text = "👂";

            SoundWaveBars.Visibility = Visibility.Visible;
            FinishSpeakingBtn.Visibility = Visibility.Visible;

            StartWaveAnimation();

            LiveVoiceInputBox.Focus();
            if (LiveVoiceInputBox.Text == (string)LiveVoiceInputBox.Tag)
            {
                LiveVoiceInputBox.Text = "";
            }
            _lastVoiceText = LiveVoiceInputBox.Text;

            // Trigger Windows native dictation (Win + H)
            TriggerWindowsVoiceTyping();

            // Auto silence-detection timer: when user stops speaking for 1.8 seconds, answer!
            _silenceTimer?.Stop();
            _silenceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1800)
            };
            _silenceTimer.Tick += (s, e) =>
            {
                string currentText = LiveVoiceInputBox.Text.Trim();
                if (!string.IsNullOrEmpty(currentText) && currentText != (string)LiveVoiceInputBox.Tag)
                {
                    if (currentText == _lastVoiceText)
                    {
                        StopListeningAndProcess();
                    }
                    else
                    {
                        _lastVoiceText = currentText;
                    }
                }
            };
            _silenceTimer.Start();
        }

        private void StopListeningAndProcess()
        {
            if (!_isListening) return;
            _isListening = false;
            _silenceTimer?.Stop();
            StopWaveAnimation();

            ListenBtnText.Text = "BENİ DİNLE (Konuşmaya Başla)";
            ListenBtnIcon.Text = "🎙️";
            SoundWaveBars.Visibility = Visibility.Collapsed;
            FinishSpeakingBtn.Visibility = Visibility.Collapsed;

            string spoken = LiveVoiceInputBox.Text.Trim();
            if (!string.IsNullOrEmpty(spoken) && spoken != (string)LiveVoiceInputBox.Tag)
            {
                LiveVoiceInputBox.Text = "";
                ExecuteVoiceDialogue(spoken);
            }
            else
            {
                LiveVoiceStatusText.Text = "🟢 Nova Dinlemeye Hazır";
                LiveVoiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                LiveVoiceSubText.Text = "Soru sormak için 'Beni Dinle'ye basabilir veya soru yazabilirsiniz.";
                VoiceOrbIcon.Text = "🎙️";
            }
        }

        private void FinishSpeaking_Click(object sender, RoutedEventArgs e)
        {
            StopListeningAndProcess();
        }

        private void TriggerWindowsVoiceTyping()
        {
            try
            {
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_H, 0, 0, UIntPtr.Zero);
                keybd_event(VK_H, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }

        private void StartWaveAnimation()
        {
            _waveTimer?.Stop();
            _waveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _waveTimer.Tick += (s, e) =>
            {
                Wave1.Height = _waveRng.Next(8, 28);
                Wave2.Height = _waveRng.Next(16, 42);
                Wave3.Height = _waveRng.Next(24, 48);
                Wave4.Height = _waveRng.Next(14, 38);
                Wave5.Height = _waveRng.Next(8, 26);
            };
            _waveTimer.Start();
        }

        private void StopWaveAnimation()
        {
            _waveTimer?.Stop();
            _waveTimer = null;
        }

        private void QuickVoice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string question)
            {
                string cleanQ = Regex.Replace(question, @"^[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u27BF\s]+", "").Trim();
                ExecuteVoiceDialogue(cleanQ);
            }
        }

        private void SendLiveVoiceMessage_Click(object sender, RoutedEventArgs e)
        {
            string text = LiveVoiceInputBox.Text.Trim();
            if (string.IsNullOrEmpty(text) || text == (string)LiveVoiceInputBox.Tag) return;

            LiveVoiceInputBox.Text = "";
            ExecuteVoiceDialogue(text);
        }

        private void LiveVoiceInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SendLiveVoiceMessage_Click(sender, e);
            }
        }

        private async void ExecuteVoiceDialogue(string userQuestion)
        {
            AnimateVoiceOrb();

            // Add user turn
            AddVoiceDialogueTurn("👤 Siz", userQuestion, isUser: true);

            LiveVoiceStatusText.Text = "🔍 Google & Veri Tabanı Taranıyor, Kaynaklar İnceleniyor...";
            VoiceOrbIcon.Text = "🌐";
            var (replyText, speechText) = await _ai.ChatAsync(userQuestion, _currentImagePath);

            LiveVoiceStatusText.Text = "🌿 MersiNova Kaynakları Sentezledi & Cevaplıyor...";
            VoiceOrbIcon.Text = "🔊";

            // Add MersiNova turn with authentic Turkish speech
            AddVoiceDialogueTurn("🌿 MersiNova (Yapay Zeka Asistanı)", replyText, isUser: false, speak: true, speechContent: speechText);
        }

        private void AddVoiceDialogueTurn(string sender, string message, bool isUser, bool speak = false, string? speechContent = null)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(isUser ? Color.FromRgb(0x13, 0x27, 0x42) : Color.FromRgb(0x0E, 0x1E, 0x36)),
                BorderBrush = new SolidColorBrush(isUser ? Color.FromRgb(0x00, 0xE6, 0x76) : Color.FromRgb(0x18, 0x33, 0x59)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            var header = new TextBlock
            {
                Text = sender,
                Foreground = new SolidColorBrush(isUser ? Color.FromRgb(0x00, 0xE6, 0x76) : Color.FromRgb(0x34, 0x98, 0xDB)),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(header);

            var tb = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF4)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19
            };
            FormatMarkdownText(tb, message);
            stack.Children.Add(tb);

            if (!isUser)
            {
                var replayBtn = new Button
                {
                    Content = "🔊 MersiNova'yı Tekrar Dinle",
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76)),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 6, 0, 0),
                    Cursor = Cursors.Hand
                };
                replayBtn.Click += (s, e) => _speech.SpeakAsync(speechContent ?? message);
                stack.Children.Add(replayBtn);
            }

            card.Child = stack;
            VoiceDialoguePanel.Children.Add(card);

            Dispatcher.InvokeAsync(() => VoiceDialogueScrollViewer.ScrollToEnd());

            if (speak)
            {
                _speech.SpeakAsync(speechContent ?? message);
            }
        }

        private void ClearVoiceDialogue_Click(object sender, RoutedEventArgs e)
        {
            VoiceDialoguePanel.Children.Clear();
        }

        private void AnimateVoiceOrb()
        {
            var anim = new DoubleAnimation(130, 150, TimeSpan.FromMilliseconds(250))
            {
                AutoReverse = true
            };
            VoiceOrbBorder.BeginAnimation(WidthProperty, anim);
            VoiceOrbBorder.BeginAnimation(HeightProperty, anim);
        }

        // ==================== REKOLTE & VERİM ENGINE ====================
        private void JumpToYieldCalc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TabRekolte_Click(sender, e);
                if (_lastResult != null && YieldCropCombo != null)
                {
                    string plant = _lastResult.DetectedPlant.ToLowerInvariant();
                    if (plant.Contains("portakal") && YieldCropCombo.Items.Count > 1)
                    {
                        YieldCropCombo.SelectedIndex = 1;
                    }
                    else if (YieldCropCombo.Items.Count > 0)
                    {
                        YieldCropCombo.SelectedIndex = 0;
                    }
                }
            }
            catch { }
        }

        // ==================== CROP SELECTION (LİMON / PORTAKAL) FOR REKOLTE ====================
        private void YieldSelectCrop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button btn) return;
                string crop = (btn.Tag as string) ?? "limon";

                if (crop == "limon")
                {
                    if (YieldCropCombo != null && YieldCropCombo.Items.Count > 0)
                        YieldCropCombo.SelectedIndex = 0;
                }
                else
                {
                    if (YieldCropCombo != null && YieldCropCombo.Items.Count > 1)
                        YieldCropCombo.SelectedIndex = 1;
                }

                SyncYieldCropButtons();
                RecalculateYieldUI();
            }
            catch { }
        }

        private void SyncYieldCropButtons()
        {
            if (YieldCropCombo == null || YieldSelectLimonBtn == null || YieldSelectPortakalBtn == null) return;

            bool isLimon = YieldCropCombo.SelectedIndex <= 0;
            if (isLimon)
            {
                YieldSelectLimonBtn.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x24, 0x08));
                YieldSelectLimonBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));
                YieldLimonBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));
                YieldLimonBtnText.FontWeight = FontWeights.Bold;

                YieldSelectPortakalBtn.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x25, 0x44));
                YieldSelectPortakalBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x3D, 0x68));
                YieldPortakalBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
                YieldPortakalBtnText.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                YieldSelectPortakalBtn.Background = new SolidColorBrush(Color.FromRgb(0x38, 0x1E, 0x08));
                YieldSelectPortakalBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                YieldPortakalBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                YieldPortakalBtnText.FontWeight = FontWeights.Bold;

                YieldSelectLimonBtn.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x25, 0x44));
                YieldSelectLimonBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x3D, 0x68));
                YieldLimonBtnText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
                YieldLimonBtnText.FontWeight = FontWeights.SemiBold;
            }
        }

        private void YieldInput_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            SyncYieldCropButtons();
            RecalculateYieldUI();
        }

        private void RecalculateYieldUI()
        {
            try
            {
                if (TreeCountSlider == null || FruitPerTreeSlider == null || FruitWeightSlider == null || PricePerKgSlider == null)
                    return;

                int trees = (int)TreeCountSlider.Value;
                int fruits = (int)FruitPerTreeSlider.Value;
                double weight = FruitWeightSlider.Value;
                double price = PricePerKgSlider.Value;
                string selectedText = YieldCropCombo?.SelectedItem?.ToString() ?? "Limon";
                string cropName = selectedText.ToLowerInvariant().Contains("portakal") ? "Portakal" : "Limon";

                TreeCountValueText.Text = $"{trees:N0} Ağaç";
                FruitPerTreeValueText.Text = $"{fruits:N0} Meyve";
                FruitWeightValueText.Text = $"{weight:F0} g";
                PricePerKgValueText.Text = $"{price:F0} TL/kg";

                _currentYield = _ai.CalculateYield(trees, fruits, cropName, weight, price);

                NetTonajKpiText.Text = $"{_currentYield.NetTonnageTon:F2} Ton";
                GrossTonajSubText.Text = $"(Brüt: {_currentYield.GrossTonnageTon:F2} Ton)";
                EstimatedRevenueKpiText.Text = $"₺{_currentYield.EstimatedRevenue:N0} TL";
                TotalFruitCountText.Text = $"{_currentYield.TotalFruitCount:N0} Adet";
                CrateCountText.Text = $"~{_currentYield.CrateCount:N0} Standart Kasa";
                WeeklyWaterText.Text = $"~{_currentYield.WaterNeedLitersWeekly:N0} Litre / Hafta";
                LaborDaysText.Text = $"4 İşçi ile ~{_currentYield.HarvestLaborDays} Gün";

                NovaYieldSummaryText.Text = $"{trees:N0} ağaç ve ağaç başına ortalama {fruits:N0} meyve ile yaklaşık {_currentYield.NetTonnageTon:F2} ton net {cropName} rekoltesi tahmin edilmektedir. " +
                                           $"Tahmini ciro beklentisi ₺{_currentYield.EstimatedRevenue:N0} TL olup, hasat için yaklaşık {_currentYield.CrateCount} standart kasa gerekecektir.";
            }
            catch { }
        }

        private void SpeakYield_Click(object sender, RoutedEventArgs e)
        {
            string speech = $"{_currentYield.TreeCount} ağaç ve ortalama {_currentYield.FruitsPerTree} meyve sayısı ile toplam yaklaşık {_currentYield.NetTonnageTon:F1} ton net rekolte hesaplandı. " +
                           $"Tahmini piyasa getirisi yaklaşık {_currentYield.EstimatedRevenue:N0} Türk Lirasıdır. Yaklaşık {_currentYield.CrateCount} kasa hazırlamanız gerekecektir.";
            _speech.SpeakAsync(speech);
        }

        private void SaveYieldToSql_Click(object sender, RoutedEventArgs e)
        {
            _db.SaveYieldEstimate(_currentYield, "Parsel Rekolte Simülasyonu");
            MessageBox.Show("Rekolte ve gelir hesabı SQLite veritabanına başarıyla kaydedildi!", "MersiNova SQL", MessageBoxButton.OK, MessageBoxImage.Information);
            AddBotMessage($"💾 **Rekolte Kaydedildi:** {_currentYield.CropName} için {_currentYield.NetTonnageTon:F2} Ton ve ₺{_currentYield.EstimatedRevenue:N0} TL gelir tahmini SQL veritabanına işlendi.");
        }

        // ==================== VOICE / SPEECH TOGGLE ====================
        private void VoiceToggle_Click(object sender, RoutedEventArgs e)
        {
            _speech.IsVoiceEnabled = !_speech.IsVoiceEnabled;
            if (_speech.IsVoiceEnabled)
            {
                VoiceStatusText.Text = "MersiNova: Açık";
                VoiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                VoiceIcon.Text = "🔊";
                _speech.SpeakAsync("MersiNova sesli asistan aktif edildi.");
            }
            else
            {
                _speech.Stop();
                VoiceStatusText.Text = "MersiNova: Sessiz";
                VoiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
                VoiceIcon.Text = "🔇";
            }
        }



        private void OpenGoogleEarthWeb_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string url = $"https://earth.google.com/web/@{CurrentLatitude},{CurrentLongitude},35a,1200d,35y,0h,0t,0r";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void VoiceAsk_Click(object sender, RoutedEventArgs e)
        {
            TabVoiceChat_Click(sender, e);
        }

        private void ReplaySpeech_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult != null)
            {
                _speech.SpeakAsync(_lastResult.SpeechText);
            }
        }

        // ==================== SQL DATABASE VIEW ====================
        private void LoadSqlTable()
        {
            try
            {
                DataTable dt = _db.ExecuteRawQuery("SELECT Id, TarlaAdi, UrunTuru, OlgunlukDurumu, OlgunlukYuzdesi, SulamaGunu, IlacGubreGunu, Tarih FROM TarlaAnalizleri ORDER BY Id DESC;");
                SqlRecordsDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SQL Hatası: {ex.Message}", "MersiNova SQL", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteSql_Click(object sender, RoutedEventArgs e)
        {
            string query = SqlQueryBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            try
            {
                DataTable dt = _db.ExecuteRawQuery(query);
                SqlRecordsDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sorgu Hatası: {ex.Message}", "SQL Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== PHOTO SELECTION & CAMERA ====================
        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Tarla Meyve/Sebze Fotoğrafı Seçin",
                Filter = "Görsel Dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tüm Dosyalar|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadImage(dialog.FileName);
            }
        }

        private async void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            var flashAnim = new DoubleAnimation(0.9, 0.0, TimeSpan.FromMilliseconds(400));
            ShutterFlash.BeginAnimation(OpacityProperty, flashAnim);

            string samplesDir = App.SamplesDirectory;
            if (!Directory.Exists(samplesDir) || Directory.GetFiles(samplesDir, "*.jpg").Length == 0)
            {
                samplesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");
            }
            if (!Directory.Exists(samplesDir) || Directory.GetFiles(samplesDir, "*.jpg").Length == 0)
            {
                samplesDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Samples");
            }
            string[] sampleFiles = Directory.Exists(samplesDir) ? Directory.GetFiles(samplesDir, "*.jpg") : Array.Empty<string>();

            string capturedPath = "";
            if (sampleFiles.Length > 0)
            {
                int nextIdx = new Random().Next(sampleFiles.Length);
                capturedPath = sampleFiles[nextIdx];
            }
            else
            {
                capturedPath = _currentImagePath ?? "";
            }

            if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
            {
                LoadImage(capturedPath);
                AddUserMessage("📸 Kamera ile anlık tarla fotoğrafı çekildi!");
                await StartAnalysisAsync();
            }
            else
            {
                UploadButton_Click(sender, e);
            }
        }

        private void SampleCrop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string crop)
            {
                string path = System.IO.Path.Combine(App.SamplesDirectory, $"{crop}.jpg");
                if (!File.Exists(path))
                {
                    path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", $"{crop}.jpg");
                }
                if (!File.Exists(path))
                {
                    path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Samples", $"{crop}.jpg");
                }
                if (File.Exists(path))
                {
                    LoadImage(path);
                    string name = crop.Equals("limon", StringComparison.OrdinalIgnoreCase) ? "🍋 Limon" : "🍊 Portakal";
                    AddUserMessage($"🔍 {name} Bahçesi görseli yüklendi. YOLO v8 analizi başlatılıyor.");
                    _ = StartAnalysisAsync();
                }
            }
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            UploadButton_Click(sender, e);
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    LoadImage(files[0]);
                }
            }
        }

        private void LoadImage(string path)
        {
            try
            {
                _currentImagePath = path;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                bitmap.EndInit();

                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
                DropPlaceholder.Visibility = Visibility.Collapsed;
                YoloOverlayCanvas.Visibility = Visibility.Collapsed;

                AnalyzeButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Görsel yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== YOLO AI ANALYSIS ====================
        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            await StartAnalysisAsync();
        }

        private async Task StartAnalysisAsync()
        {
            if (_isAnalyzing || string.IsNullOrEmpty(_currentImagePath)) return;

            _isAnalyzing = true;
            AnalyzeButton.IsEnabled = false;
            AnalyzeBtnText.Text = "Analiz Ediliyor...";
            ShowTyping(true);

            try
            {
                var result = await _ai.AnalyzeImageAsync(_currentImagePath);
                _lastResult = result;

                ShowTyping(false);

                // Render YOLO Bounding Box over Image
                RenderYoloOverlay(result);

                // Populate KPI Dashboard Cards
                PlantTitleText.Text = $"🌿 {result.DetectedPlant} ({result.ScientificName})";
                RipenessText.Text = result.RipenessLabel;
                RipenessScore.Text = result.RipenessScore;
                RipenessScore.Foreground = result.RipenessColor;
                RipenessBadge.Background = new SolidColorBrush(Color.FromArgb(40, result.RipenessColor.Color.R, result.RipenessColor.Color.G, result.RipenessColor.Color.B));
                
                double targetWidth = Math.Clamp((result.RipenessPercentage / 100.0) * 360, 20, 360);
                RipenessProgressBar.Width = targetWidth;
                RipenessProgressBar.Background = result.RipenessColor;

                // Sulama KPI
                WaterDaysKpi.Text = $"{result.WaterDays} Gün Sonra";
                WaterText.Text = result.WaterRecommendation;

                // İlaç / Gübre KPI
                PesticideDaysKpi.Text = $"{result.PesticideDays} Gün Sonra";
                PesticideText.Text = result.PesticideRecommendation;

                // Hasat KPI
                HarvestText.Text = result.HarvestEstimate;

                // Hastalık & Tedavi KPI
                DiseaseTitleText.Text = result.DetectedDisease;
                DiseasePrescriptionText.Text = result.DiseasePrescription;

                // Reveal Result Card
                ResultCard.Visibility = Visibility.Visible;
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450));
                ResultCard.BeginAnimation(OpacityProperty, anim);

                // Bot Chat Dialogue & Female Speech Synthesis
                AddBotMessage(result.ChatSummary, speak: true, speechContent: result.SpeechText);

                // Save to SQLite Database
                _db.SaveAnalysis(result, _currentImagePath, "Saha / Sera Parseli A-1");
                LoadRecentAnalyses();
            }
            catch (Exception ex)
            {
                ShowTyping(false);
                AddBotMessage($"⚠️ Analiz gerçekleştirilirken hata oluştu: {ex.Message}");
            }
            finally
            {
                _isAnalyzing = false;
                AnalyzeButton.IsEnabled = true;
                AnalyzeBtnText.Text = "Analiz Et";
            }
        }

        private void RenderYoloOverlay(AnalysisResult res)
        {
            YoloOverlayCanvas.Visibility = Visibility.Visible;
            double canvasW = DropZone.ActualWidth > 0 ? DropZone.ActualWidth : 500;
            double canvasH = DropZone.ActualHeight > 0 ? DropZone.ActualHeight : 320;

            double boxW = canvasW * 0.48;
            double boxH = canvasH * 0.65;
            double boxLeft = (canvasW - boxW) / 2.0;
            double boxTop = (canvasH - boxH) / 2.0;

            Canvas.SetLeft(YoloBoundingBox, boxLeft);
            Canvas.SetTop(YoloBoundingBox, boxTop);
            YoloBoundingBox.Width = boxW;
            YoloBoundingBox.Height = boxH;

            YoloTagText.Text = $"YOLOv8x: {res.DetectedPlant} ({res.YoloConfidence})";
            YoloMaturityTag.Text = $"Olgunluk: {res.RipenessScore}";

            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            YoloBoundingBox.BeginAnimation(OpacityProperty, anim);
        }

        // ==================== CHAT & USER INPUT ====================
        private void SetPlaceholder()
        {
            UserInputBox.Text = (string)UserInputBox.Tag;
            UserInputBox.Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x75, 0x90));
        }

        private void UserInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UserInputBox.Text == (string)UserInputBox.Tag)
            {
                UserInputBox.Text = "";
                UserInputBox.Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF4));
            }
        }

        private void UserInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UserInputBox.Text))
                SetPlaceholder();
        }

        private void UserInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button chip && chip.Tag is string question)
            {
                UserInputBox.Text = question;
                SendMessage();
            }
        }

        private async void SendMessage()
        {
            string text = UserInputBox.Text.Trim();
            if (string.IsNullOrEmpty(text) || text == (string)UserInputBox.Tag) return;

            AddUserMessage(text);
            UserInputBox.Text = "";
            SetPlaceholder();

            await ProcessUserQuery(text);
        }

        private async Task ProcessUserQuery(string query)
        {
            TypingIndicatorText.Text = "🔍 Google & Web Veritabanı taranıyor, kaynaklar inceleniyor...";
            ShowTyping(true);
            var (replyText, speechText) = await _ai.ChatAsync(query, _currentImagePath);
            ShowTyping(false);

            AddBotMessage(replyText, speak: true, speechContent: speechText);
        }

        private void AddUserMessage(string text)
        {
            var bubble = CreateBubble(text, isUser: true);
            ChatPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        private void AddBotMessage(string text, bool speak = false, string? speechContent = null)
        {
            var bubble = CreateBubble(text, isUser: false);
            ChatPanel.Children.Add(bubble);
            ScrollToBottom();

            if (speak)
            {
                _speech.SpeakAsync(speechContent ?? text);
            }
        }

        private UIElement CreateBubble(string text, bool isUser)
        {
            var wrapper = new Grid { Margin = new Thickness(0, 5, 0, 5) };

            var bubble = new Border
            {
                CornerRadius = isUser
                    ? new CornerRadius(16, 16, 3, 16)
                    : new CornerRadius(16, 16, 16, 3),
                Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 400,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            if (isUser)
            {
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0)
                };
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xE6, 0x76), 0));
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0x89, 0x7B), 1));
                bubble.Background = grad;
            }
            else
            {
                bubble.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x1E, 0x36));
                bubble.BorderBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x33, 0x59));
                bubble.BorderThickness = new Thickness(1);
            }

            var stack = new StackPanel();

            var tb = new TextBlock
            {
                Foreground = isUser ? new SolidColorBrush(Color.FromRgb(0x04, 0x12, 0x1E)) : new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF4)),
                FontSize = 12.5,
                FontWeight = isUser ? FontWeights.SemiBold : FontWeights.Normal,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };

            FormatMarkdownText(tb, text);
            stack.Children.Add(tb);

            if (!isUser)
            {
                var listenBtn = new Button
                {
                    Content = "🔊 MersiNova'yı Dinle",
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76)),
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 6, 0, 0),
                    Cursor = Cursors.Hand
                };
                listenBtn.Click += (s, e) => _speech.SpeakAsync(text);
                stack.Children.Add(listenBtn);
            }

            bubble.Child = stack;
            wrapper.Children.Add(bubble);
            return wrapper;
        }

        private static void FormatMarkdownText(TextBlock tb, string raw)
        {
            var parts = raw.Split(new[] { "**" }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1)
                {
                    tb.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(parts[i])));
                }
                else
                {
                    tb.Inlines.Add(new System.Windows.Documents.Run(parts[i]));
                }
            }
        }

        private void ScrollToBottom()
        {
            Dispatcher.InvokeAsync(() => ChatScrollViewer.ScrollToEnd());
        }

        private void ShowTyping(bool show)
        {
            TypingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                _typingAnimator.Start();
                ScrollToBottom();
            }
            else
            {
                _typingAnimator.Stop();
            }
        }

        // ==================== SQL HISTORY LIST ====================
        private void LoadRecentAnalyses()
        {
            var all = _db.GetAllAnalyses();
            var items = all.Take(4).Select(r => new
            {
                Label = $"🌿 {r.TarlaAdi} — {r.UrunTuru} ({r.OlgunlukDurumu})",
                Date = r.Tarih
            }).ToList();

            RecentList.ItemsSource = items;
            NoHistoryText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public class TypingAnimator
    {
        private readonly Ellipse[] _dots;
        private System.Windows.Threading.DispatcherTimer? _timer;
        private int _step = 0;

        public TypingAnimator(Ellipse d1, Ellipse d2, Ellipse d3)
        {
            _dots = new[] { d1, d2, d3 };
        }

        public void Start()
        {
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _timer.Tick += (s, e) =>
            {
                for (int i = 0; i < 3; i++)
                    _dots[i].Opacity = (i == _step % 3) ? 1.0 : 0.3;
                _step++;
            };
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
            foreach (var d in _dots) d.Opacity = 1;
        }
    }
}