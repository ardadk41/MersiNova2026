using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MersiNova
{
    public partial class SplashWindow : Window
    {
        private CancellationTokenSource? _thinkingCts;

        public SplashWindow()
        {
            InitializeComponent();
        }

        // ─── Window Drag ───────────────────────────────────────────────
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // ─── Window Controls ────────────────────────────────────────────
        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            anim.Completed += (s, _) => Close();
            RootBorder.BeginAnimation(OpacityProperty, anim);
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // ─── Camera Button: open file dialog OR launch camera ──────────
        private void CameraBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Fotoğraf Seç veya Kamera Görüntüsü Yükle",
                Filter = "Görsel Dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp|Tüm Dosyalar|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                AddUserImageMessage(filePath);
                _ = SimulateAiAnalysis(Path.GetFileName(filePath));
            }
        }

        // ─── Attach Button: open file dialog ───────────────────────────
        private void AttachBtn_Click(object sender, RoutedEventArgs e)
        {
            CameraBtn_Click(sender, e);
        }

        // ─── Quick action chips ─────────────────────────────────────────
        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                MessageInput.Text = tag;
                SendMessage();
            }
        }

        // ─── Enter key sends message ────────────────────────────────────
        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Could add character counter here
        }

        // ─── Send button click ──────────────────────────────────────────
        private void SendBtn_Click(object sender, RoutedEventArgs e) => SendMessage();

        // ─── Core: send & respond ───────────────────────────────────────
        private void SendMessage()
        {
            string text = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            AddUserTextMessage(text);
            MessageInput.Text = string.Empty;
            _ = SimulateAiResponse(text);
        }

        // ─── UI Helpers ─────────────────────────────────────────────────
        private void AddUserTextMessage(string text)
        {
            var bubble = BuildUserBubble(text);
            ChatPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        private void AddUserImageMessage(string filePath)
        {
            var container = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var bubble = new Border
            {
                CornerRadius = new CornerRadius(16, 4, 16, 16),
                Padding = new Thickness(8),
                MaxWidth = 260,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new LinearGradientBrush(
                    Color.FromArgb(50, 0, 255, 135),
                    Color.FromArgb(30, 100, 255, 218),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 255, 135)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            try
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri(filePath)),
                    MaxHeight = 180,
                    MaxWidth = 240,
                    Stretch = Stretch.UniformToFill
                };
                var imgBorder = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    ClipToBounds = true,
                    Child = img
                };
                stack.Children.Add(imgBorder);
            }
            catch { /* ignore invalid images */ }

            var label = new TextBlock
            {
                Text = "📷 " + System.IO.Path.GetFileName(filePath),
                Foreground = new SolidColorBrush(Color.FromArgb(200, 232, 244, 253)),
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(4, 4, 4, 0)
            };
            stack.Children.Add(label);
            bubble.Child = stack;

            Grid.SetColumn(bubble, 1);
            container.Children.Add(bubble);
            ChatPanel.Children.Add(container);
            ScrollToBottom();
        }

        private Grid BuildUserBubble(string text)
        {
            var container = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var bubble = new Border
            {
                CornerRadius = new CornerRadius(16, 4, 16, 16),
                Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 320,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new LinearGradientBrush(
                    Color.FromArgb(50, 0, 255, 135),
                    Color.FromArgb(30, 100, 255, 218),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 255, 135)),
                BorderThickness = new Thickness(1)
            };

            bubble.Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 232, 244, 253)),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetColumn(bubble, 1);
            container.Children.Add(bubble);
            return container;
        }

        private Grid BuildAiBubble(string text)
        {
            var container = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Avatar
            var avatar = new Grid { Width = 32, Height = 32, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            var avatarEllipse = new System.Windows.Shapes.Ellipse
            {
                Width = 32, Height = 32,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(80, 0, 255, 135),
                    Color.FromArgb(40, 0, 212, 170))
            };
            avatarEllipse.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromArgb(255, 0, 255, 135),
                BlurRadius = 8,
                Opacity = 0.5,
                ShadowDepth = 0
            };
            avatar.Children.Add(avatarEllipse);
            Grid.SetColumn(avatar, 0);
            container.Children.Add(avatar);

            var bubble = new Border
            {
                CornerRadius = new CornerRadius(4, 16, 16, 16),
                Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 360,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(
                    Color.FromArgb(255, 13, 27, 42),
                    Color.FromArgb(255, 17, 34, 64),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 30, 58, 95)),
                BorderThickness = new Thickness(1)
            };

            bubble.Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 136, 146, 176)),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };

            Grid.SetColumn(bubble, 1);
            container.Children.Add(bubble);
            return container;
        }

        private void ScrollToBottom()
        {
            Dispatcher.InvokeAsync(() =>
            {
                ChatScrollViewer.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ─── Thinking dots animation ────────────────────────────────────
        private async Task SimulateAiResponse(string userText)
        {
            ShowThinkingIndicator();
            await Task.Delay(1800 + new Random().Next(0, 800));
            HideThinkingIndicator();

            string response = GenerateResponse(userText);
            var bubble = BuildAiBubble(response);
            ChatPanel.Children.Add(bubble);

            // Animate bubble in
            bubble.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            bubble.BeginAnimation(OpacityProperty, fadeIn);

            ScrollToBottom();
        }

        private async Task SimulateAiAnalysis(string fileName)
        {
            ShowThinkingIndicator();
            await Task.Delay(2500);
            HideThinkingIndicator();

            string response = $"🔍 **Görsel analiz edildi:** `{fileName}`\n\n" +
                              "YOLO modeli görüntüyü işliyor...\n\n" +
                              "📊 **Tespit Sonuçları:**\n" +
                              "• Toplam meyve sayısı: 47 adet\n" +
                              "• Olgun (hasat hazır): %62 — 29 adet 🟢\n" +
                              "• Yarı olgun: %28 — 13 adet 🟡\n" +
                              "• Ham: %10 — 5 adet 🔴\n\n" +
                              "🗓️ **Önerilen hasat tarihi:** 3-5 gün içinde\n" +
                              "📈 **Tahmini verim:** 12.4 kg";

            var bubble = BuildAiBubble(response);
            ChatPanel.Children.Add(bubble);

            bubble.Opacity = 0;
            bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)));
            ScrollToBottom();
        }

        private void ShowThinkingIndicator()
        {
            _thinkingCts?.Cancel();
            _thinkingCts = new CancellationTokenSource();

            ThinkingIndicator.Visibility = Visibility.Visible;
            ScrollToBottom();

            _ = AnimateThinkingDots(_thinkingCts.Token);
        }

        private void HideThinkingIndicator()
        {
            _thinkingCts?.Cancel();
            ThinkingIndicator.Visibility = Visibility.Collapsed;
        }

        private async Task AnimateThinkingDots(CancellationToken ct)
        {
            int step = 0;
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(400, ct); } catch { break; }
                if (ct.IsCancellationRequested) break;

                Dispatcher.Invoke(() =>
                {
                    switch (step % 4)
                    {
                        case 0:
                            ThinkingDot1.Opacity = 1.0; ThinkingDot2.Opacity = 0.3; ThinkingDot3.Opacity = 0.1;
                            break;
                        case 1:
                            ThinkingDot1.Opacity = 0.6; ThinkingDot2.Opacity = 1.0; ThinkingDot3.Opacity = 0.3;
                            break;
                        case 2:
                            ThinkingDot1.Opacity = 0.3; ThinkingDot2.Opacity = 0.6; ThinkingDot3.Opacity = 1.0;
                            break;
                        case 3:
                            ThinkingDot1.Opacity = 0.6; ThinkingDot2.Opacity = 0.6; ThinkingDot3.Opacity = 0.6;
                            break;
                    }
                    step++;
                });
            }
        }

        // ─── Smart response generator ────────────────────────────────────
        private string GenerateResponse(string input)
        {
            string lower = input.ToLower();

            if (lower.Contains("bahçe") || lower.Contains("analiz"))
                return "🌿 Bahçenizi analiz etmek için bir fotoğraf yükleyin. Sol alttaki 📷 kamera butonuna basın veya görsel dosyası seçin. YOLO modelim meyve sayımı, olgunluk tespiti ve hasat tahmini yapacak.";

            if (lower.Contains("hasat") || lower.Contains("plan"))
                return "🗓️ Hasat planı oluşturmak için önce bahçe görselinizi yükleyin. Mevcut olgunluk verilerine dayanarak:\n\n• Optimum hasat penceresi hesaplanır\n• Lojistik tavsiye verilir\n• Ürün kayıp riski analiz edilir";

            if (lower.Contains("verimlilik") || lower.Contains("rapor"))
                return "📊 Verimlilik raporu için geçmiş hasat verilerini veya anlık görüntüyü paylaşın. Sistem şunları hesaplar:\n\n• Ağaç başına verim (kg)\n• Parsel bazlı karşılaştırma\n• Yıllık trend analizi\n• Simio simülasyon çıktısı";

            if (lower.Contains("sayım") || lower.Contains("meyve"))
                return "🔢 Meyve sayımı için görsel yükleyin. YOLO v8 modelim:\n\n✓ Her meyveyi bounding box ile işaretler\n✓ Toplam adedi çıkarır\n✓ Olgunluk sınıfına göre gruplar\n✓ Konum haritası oluşturur";

            if (lower.Contains("yolo") || lower.Contains("yapay zeka") || lower.Contains("ai"))
                return "🤖 MersiNova'nın AI motoru YOLO (You Only Look Once) mimarisi üzerine kuruludur. Gerçek zamanlı nesne tespiti yaparak:\n\n• Saniyede 30+ kare işler\n• %94+ doğruluk oranı\n• Çoklu meyve türü desteği\n• SQL Server'a anlık veri kaydeder";

            if (lower.Contains("merhaba") || lower.Contains("selam") || lower.Contains("nasıl"))
                return "👋 Merhaba! Ben MersiNova yapay zekâ asistanınızım. Bahçenizi analiz etmek, hasat planı oluşturmak veya herhangi bir tarım sorunuzu yanıtlamak için buradayım. Nasıl yardımcı olabilirim?";

            return "🌱 Anladım. Bu konuda size yardımcı olabilirim. Daha detaylı analiz için lütfen bahçenizin fotoğrafını yükleyin. Görsel analiz sonrası kapsamlı bir rapor sunacağım.";
        }
    }
}
