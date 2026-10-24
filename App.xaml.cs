using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace MersiNova
{
    public partial class App : Application
    {
        public static string AppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MersiNova");
        public static string SamplesDirectory => Path.Combine(AppDataDir, "Samples");

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                string crashLog = Path.Combine(AppDataDir, "crash.log");

                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    try { File.WriteAllText(crashLog, args.ExceptionObject?.ToString() ?? "Unknown exception"); } catch { }
                };

                DispatcherUnhandledException += (s, args) =>
                {
                    try { File.WriteAllText(crashLog, args.Exception.ToString()); } catch { }
                    MessageBox.Show("Hata:\n" + args.Exception.Message, "MersiNova Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Handled = true;
                };
            }
            catch { }

            base.OnStartup(e);

            ExtractEmbeddedSamples();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var login = new LoginWindow();
            bool? result = login.ShowDialog();

            if (result == true)
            {
                var main = new MainWindow(login.ConfirmedUsername, login.ConfirmedLocation, login.ConfirmedLatitude, login.ConfirmedLongitude);
                this.MainWindow = main;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                main.Show();
                main.Activate();
                main.Focus();
            }
            else
            {
                Shutdown();
            }
        }

        private void ExtractEmbeddedSamples()
        {
            try
            {
                Directory.CreateDirectory(SamplesDirectory);
                var asm = Assembly.GetExecutingAssembly();
                string[] names = asm.GetManifestResourceNames();

                foreach (var name in names)
                {
                    if (name.IndexOf("Samples.", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int idx = name.IndexOf("Samples.", StringComparison.OrdinalIgnoreCase);
                        string fileName = name.Substring(idx + "Samples.".Length);
                        string destFile = Path.Combine(SamplesDirectory, fileName);

                        if (!File.Exists(destFile))
                        {
                            using var stream = asm.GetManifestResourceStream(name);
                            if (stream != null)
                            {
                                using var fs = File.Create(destFile);
                                stream.CopyTo(fs);
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
