using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MersiNova
{
    public partial class ElevenLabsSettingsWindow : Window
    {
        private readonly SpeechService _speech;
        public bool SettingsChanged { get; private set; } = false;

        public ElevenLabsSettingsWindow(SpeechService speech)
        {
            InitializeComponent();
            _speech = speech;

            // Populate API Key
            ApiKeyBox.Text = _speech.ElevenLabsApiKey ?? "";

            // Populate Voices
            VoiceComboBox.ItemsSource = SpeechService.AvailableVoices;
            var currentVoice = SpeechService.AvailableVoices.FirstOrDefault(v => v.Id == _speech.ElevenLabsVoiceId)
                               ?? SpeechService.AvailableVoices.FirstOrDefault();
            VoiceComboBox.SelectedItem = currentVoice;

            UpdateStatusText();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void UpdateStatusText()
        {
            if (_speech.IsElevenLabsConfigured)
            {
                StatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                StatusBadgeText.Text = $"🟢 ElevenLabs Aktif: {_speech.ActiveVoiceName} (Multilingual V2)";
            }
            else
            {
                StatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                StatusBadgeText.Text = "ℹ️ Yerleşik Akıcı Doğal Ses Motoru Devrede. (İsteğe bağlı ElevenLabs API anahtarı ekleyebilirsiniz)";
            }
        }

        private void TestSpeech_Click(object sender, RoutedEventArgs e)
        {
            string key = ApiKeyBox.Text.Trim();
            string voiceId = (VoiceComboBox.SelectedItem as ElevenLabsVoiceItem)?.Id ?? _speech.ElevenLabsVoiceId;

            // Apply temporarily for test
            _speech.SaveElevenLabsConfig(key, voiceId);

            StatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
            StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
            StatusBadgeText.Text = "🔊 MersiNova ses testi yapılıyor...";

            _speech.SpeakAsync("Merhaba efendim! Ben yapay zeka asistanınız MersiNova. ElevenLabs ses entegrasyonu başarıyla aktif edildi. Narenciye bahçeniz için hizmetinizdeyim.");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string key = ApiKeyBox.Text.Trim();
            string voiceId = (VoiceComboBox.SelectedItem as ElevenLabsVoiceItem)?.Id ?? _speech.ElevenLabsVoiceId;

            _speech.SaveElevenLabsConfig(key, voiceId);
            SettingsChanged = true;
            UpdateStatusText();

            MessageBox.Show(
                string.IsNullOrWhiteSpace(key)
                    ? "ElevenLabs anahtarı temizlendi. MersiNova yerleşik doğal ses motoru kullanılacak."
                    : "ElevenLabs yapay zeka ses ayarları başarıyla kaydedildi ve MersiNova'ya uygulandı!",
                "MersiNova Ses Ayarları", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
