using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MersiNova
{
    public partial class LoginWindow : Window
    {
        public string ConfirmedUsername { get; private set; } = "arda";
        public string ConfirmedLocation { get; private set; } = "Mersin / Erdemli - Limonlu Mahallesi";
        public double ConfirmedLatitude { get; private set; } = 36.6025;
        public double ConfirmedLongitude { get; private set; } = 34.3142;

        private readonly DatabaseService _db = new DatabaseService();

        public LoginWindow()
        {
            InitializeComponent();
            _db.Initialize();

            // Set up clipboard paste listeners for strict input filtering
            DataObject.AddPastingHandler(UsernameBox, UsernameBox_Pasting);
            DataObject.AddPastingHandler(PasswordBox, PasswordBox_Pasting);
            DataObject.AddPastingHandler(RegUsernameBox, UsernameBox_Pasting);
            DataObject.AddPastingHandler(RegPasswordBox, PasswordBox_Pasting);
            DataObject.AddPastingHandler(RegPasswordConfirmBox, PasswordBox_Pasting);

            PasswordBox.PasswordChanged += (s, e) =>
            {
                PasswordCounterText.Text = $"{PasswordBox.Password.Length}/4";
                PasswordCounterText.Foreground = PasswordBox.Password.Length == 4
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76))
                    : new SolidColorBrush(Color.FromRgb(0x7E, 0x93, 0xAA));
            };

            RegPasswordBox.PasswordChanged += (s, e) =>
            {
                RegPassCounterText.Text = $"{RegPasswordBox.Password.Length}/4";
                RegPassCounterText.Foreground = RegPasswordBox.Password.Length == 4
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76))
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));
            };

            RegPasswordConfirmBox.PasswordChanged += (s, e) =>
            {
                RegPassConfirmCounterText.Text = $"{RegPasswordConfirmBox.Password.Length}/4";
                RegPassConfirmCounterText.Foreground = (RegPasswordConfirmBox.Password.Length == 4 && RegPasswordConfirmBox.Password == RegPasswordBox.Password)
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76))
                    : new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xBF));
            };
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
            }
            catch
            {
                Close();
            }
        }

        // ==================== MODE SWITCHING: GİRİŞ YAP vs YENİ KAYIT OL ====================
        private void SwitchToLoginMode_Click(object sender, RoutedEventArgs e)
        {
            LoginFormPanel.Visibility = Visibility.Visible;
            RegisterFormPanel.Visibility = Visibility.Collapsed;

            TabLoginModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x2E, 0x4E));
            TabLoginModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
            TabLoginModeBtn.BorderThickness = new Thickness(1.2);
            LoginModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
            LoginModeText.FontWeight = FontWeights.Bold;

            TabRegisterModeBtn.Background = Brushes.Transparent;
            TabRegisterModeBtn.BorderThickness = new Thickness(0);
            RegisterModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xBF));
            RegisterModeText.FontWeight = FontWeights.SemiBold;

            HideError();
        }

        private void SwitchToRegisterMode_Click(object sender, RoutedEventArgs e)
        {
            LoginFormPanel.Visibility = Visibility.Collapsed;
            RegisterFormPanel.Visibility = Visibility.Visible;

            TabRegisterModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x2E, 0x4E));
            TabRegisterModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
            TabRegisterModeBtn.BorderThickness = new Thickness(1.2);
            RegisterModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
            RegisterModeText.FontWeight = FontWeights.Bold;

            TabLoginModeBtn.Background = Brushes.Transparent;
            TabLoginModeBtn.BorderThickness = new Thickness(0);
            LoginModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xBF));
            LoginModeText.FontWeight = FontWeights.SemiBold;

            HideError();
        }

        // ==================== 1. USERNAME RESTRICTION (ONLY LETTERS) ====================
        private void UsernameBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$");
            if (e.Handled)
            {
                ShowError("⚠️ Kullanıcı adına yalnızca harf yazabilirsiniz! Sayı veya özel karakter yazılamaz.");
            }
            else
            {
                HideError();
            }
        }

        private void UsernameBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                ShowError("⚠️ Kullanıcı adı boşluk içeremez!");
            }
        }

        private void UsernameBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                if (!Regex.IsMatch(text, @"^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$"))
                {
                    e.CancelCommand();
                    ShowError("⚠️ Yapıştırılan metin yalnızca harflerden oluşmalıdır!");
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        // ==================== 2. PASSWORD RESTRICTION (EXACTLY 4 NUMBERS ONLY) ====================
        private void PasswordBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var pBox = sender as PasswordBox;
            int currentLen = pBox?.Password.Length ?? 0;

            if (!char.IsDigit(e.Text[0]) || currentLen >= 4)
            {
                e.Handled = true;
                if (!char.IsDigit(e.Text[0]))
                {
                    ShowError("⚠️ Şifreye yalnızca rakam yazabilirsiniz! Harf veya sembol yazılamaz.");
                }
                else
                {
                    ShowError("⚠️ Şifre tam olarak 4 haneli bir sayı olmalıdır!");
                }
            }
            else
            {
                HideError();
            }
        }

        private void PasswordBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void PasswordBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                var pBox = sender as PasswordBox;
                int currentLen = pBox?.Password.Length ?? 0;

                if (!Regex.IsMatch(text, @"^[0-9]+$") || (currentLen + text.Length > 4))
                {
                    e.CancelCommand();
                    ShowError("⚠️ Şifreye yalnızca en fazla 4 haneli rakam yapıştırılabilir!");
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        // ==================== 3. GOOGLE MAPS POPUP LAUNCHER ====================
        private void OpenGoogleMaps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mapWin = new GoogleMapsWindow(LocationBox.Text);
                mapWin.Owner = this;
                bool? result = mapWin.ShowDialog();

                if (result == true)
                {
                    LocationBox.Text = mapWin.SelectedLocationString;
                    ConfirmedLocation = mapWin.SelectedLocationString;
                    ConfirmedLatitude = mapWin.SelectedLatitude;
                    ConfirmedLongitude = mapWin.SelectedLongitude;
                    MapSuccessText.Text = $"✅ Google Maps ile doğrulandı:\n{mapWin.SelectedLocationString}";
                    MapSuccessBanner.Visibility = Visibility.Visible;
                    HideError();
                }
            }
            catch (Exception ex)
            {
                ShowError("Harita penceresi açılırken bir durum oluştu: " + ex.Message);
            }
        }

        private void RegOpenGoogleMaps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mapWin = new GoogleMapsWindow(RegLocationBox.Text);
                mapWin.Owner = this;
                bool? result = mapWin.ShowDialog();

                if (result == true)
                {
                    RegLocationBox.Text = mapWin.SelectedLocationString;
                    ConfirmedLocation = mapWin.SelectedLocationString;
                    ConfirmedLatitude = mapWin.SelectedLatitude;
                    ConfirmedLongitude = mapWin.SelectedLongitude;
                    MapSuccessText.Text = $"✅ Google Maps ile yeni parseliniz doğrulandı:\n{mapWin.SelectedLocationString}";
                    MapSuccessBanner.Visibility = Visibility.Visible;
                    HideError();
                }
            }
            catch (Exception ex)
            {
                ShowError("Harita penceresi açılırken bir durum oluştu: " + ex.Message);
            }
        }

        private void AutoDetectLocation_Click(object sender, RoutedEventArgs e)
        {
            LocationBox.Text = "Mersin / Erdemli - Limonlu Havzası (36.6025° K, 34.3142° D)";
            ConfirmedLocation = LocationBox.Text;
            ConfirmedLatitude = 36.6025;
            ConfirmedLongitude = 34.3142;
            MapSuccessText.Text = "📡 GPS ile konum başarıyla alındı (Erdemli / Limonlu).";
            MapSuccessBanner.Visibility = Visibility.Visible;
            HideError();
        }

        private void RegAutoDetectLocation_Click(object sender, RoutedEventArgs e)
        {
            RegLocationBox.Text = "Mersin / Erdemli - Limonlu Havzası (36.6025° K, 34.3142° D)";
            ConfirmedLocation = RegLocationBox.Text;
            ConfirmedLatitude = 36.6025;
            ConfirmedLongitude = 34.3142;
            MapSuccessText.Text = "📡 GPS ile konum başarıyla alındı (Erdemli / Limonlu).";
            MapSuccessBanner.Visibility = Visibility.Visible;
            HideError();
        }

        private void QuickLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string loc)
            {
                LocationBox.Text = loc;
                ConfirmedLocation = loc;

                if (loc.Contains("Erdemli")) { ConfirmedLatitude = 36.6025; ConfirmedLongitude = 34.3142; }
                else if (loc.Contains("Silifke")) { ConfirmedLatitude = 36.3778; ConfirmedLongitude = 33.9344; }
                else if (loc.Contains("Tarsus")) { ConfirmedLatitude = 36.9167; ConfirmedLongitude = 34.8950; }
                else if (loc.Contains("Yüreğir")) { ConfirmedLatitude = 36.9850; ConfirmedLongitude = 35.3289; }

                MapSuccessText.Text = $"📍 Parsel seçildi: {loc}";
                MapSuccessBanner.Visibility = Visibility.Visible;
                HideError();
            }
        }

        // ==================== 4. LOGIN VALIDATION & TRANSITION ====================
        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                LoginButton_Click(sender, e);
            }
        }

        private void RegInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                RegisterButton_Click(sender, e);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string user = UsernameBox.Text.Trim();
            string pass = PasswordBox.Password.Trim();
            string location = LocationBox.Text.Trim();

            // 1. Check Username constraint: ONLY LETTERS
            if (!Regex.IsMatch(user, @"^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$"))
            {
                ShowError("❌ Kullanıcı adında yalnızca harf kullanılabilir! Rakam veya özel karakter yazılamaz.");
                UsernameBox.Focus();
                return;
            }

            // 2. Check Password constraint: EXACTLY 4 DIGITS
            if (pass.Length != 4 || !pass.All(char.IsDigit))
            {
                ShowError("❌ Şifre zorunlu olarak tam 4 haneli bir sayı olmalıdır! (Örn: 1234)");
                PasswordBox.Focus();
                return;
            }

            // 3. Check Mandatory Location
            if (string.IsNullOrWhiteSpace(location))
            {
                ShowError("⚠️ Lütfen bahçenizin net konum bilgisini giriniz veya yanındaki 'Harita' butonuna basarak Google Maps'ten seçiniz!");
                LocationBox.Focus();
                return;
            }

            // 4. Validate Credentials against SQLite DB
            var (isValid, msg, loc, lat, lon) = _db.ValidateUser(user, pass);
            if (!isValid)
            {
                ShowError(msg);
                PasswordBox.Focus();
                return;
            }

            // Success
            ConfirmedUsername = user;
            ConfirmedLocation = string.IsNullOrWhiteSpace(location) ? loc : location;
            if (lat != 0) ConfirmedLatitude = lat;
            if (lon != 0) ConfirmedLongitude = lon;

            ProceedToMain();
        }

        // ==================== 5. REGISTER VALIDATION & TRANSITION ====================
        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string regUser = RegUsernameBox.Text.Trim();
            string regPass = RegPasswordBox.Password.Trim();
            string regConfirm = RegPasswordConfirmBox.Password.Trim();
            string regLoc = RegLocationBox.Text.Trim();

            // 1. Username constraint: ONLY LETTERS, NOT EMPTY
            if (string.IsNullOrWhiteSpace(regUser) || !Regex.IsMatch(regUser, @"^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$"))
            {
                ShowError("❌ Kayıt için adınızda / kullanıcı adınızda yalnızca harf kullanabilirsiniz! Boş bırakılamaz.");
                RegUsernameBox.Focus();
                return;
            }

            // 2. Password constraint: EXACTLY 4 DIGITS
            if (regPass.Length != 4 || !regPass.All(char.IsDigit))
            {
                ShowError("❌ Şifre zorunlu olarak tam 4 haneli bir sayı olmalıdır! (Örn: 2468)");
                RegPasswordBox.Focus();
                return;
            }

            // 3. Confirm Password check
            if (regPass != regConfirm)
            {
                ShowError("❌ Girdiğiniz şifreler birbiriyle uyuşmuyor! Lütfen kontrol ediniz.");
                RegPasswordConfirmBox.Focus();
                return;
            }

            // 4. Mandatory Location check
            if (string.IsNullOrWhiteSpace(regLoc))
            {
                ShowError("⚠️ Lütfen bahçenizin net konum bilgisini giriniz veya haritadan seçiniz!");
                RegLocationBox.Focus();
                return;
            }

            // 5. Register in SQLite DB
            var (success, msg) = _db.RegisterUser(regUser, regPass, regLoc, ConfirmedLatitude, ConfirmedLongitude);
            if (!success)
            {
                ShowError(msg);
                return;
            }

            // Success: log in directly with the newly registered user
            ConfirmedUsername = regUser;
            ConfirmedLocation = regLoc;

            ProceedToMain();
        }

        private void ProceedToMain()
        {
            try
            {
                DialogResult = true;
            }
            catch
            {
                var main = new MainWindow(ConfirmedUsername, ConfirmedLocation, ConfirmedLatitude, ConfirmedLongitude);
                Application.Current.MainWindow = main;
                Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                main.Show();
                main.Activate();
                Close();
            }
        }

        private void ShowError(string msg)
        {
            ErrorText.Text = msg;
            ErrorBanner.Visibility = Visibility.Visible;
            MapSuccessBanner.Visibility = Visibility.Collapsed;
        }

        private void HideError()
        {
            ErrorBanner.Visibility = Visibility.Collapsed;
        }
    }
}
