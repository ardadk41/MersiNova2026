using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace MersiNova
{
    public partial class GoogleMapsWindow : Window
    {
        public string SelectedLocationString { get; private set; } = "Mersin / Erdemli - Limonlu Havzası (36.6025° K, 34.3142° D)";
        public double SelectedLatitude { get; private set; } = 36.6025;
        public double SelectedLongitude { get; private set; } = 34.3142;

        private static readonly HttpClient _geocoderClient = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        // Offline / quick fallback dictionary for Turkish cities
        private static readonly Dictionary<string, (double Lat, double Lng, string FullName)> _cityCatalog = new(StringComparer.OrdinalIgnoreCase)
        {
            ["erdemli"] = (36.6025, 34.3142, "Mersin / Erdemli - Limonlu"),
            ["limonlu"] = (36.6025, 34.3142, "Mersin / Erdemli - Limonlu"),
            ["silifke"] = (36.3778, 33.9344, "Mersin / Silifke - Göksu Vadisi"),
            ["tarsus"] = (36.9167, 34.8950, "Mersin / Tarsus - Narenciye Ovası"),
            ["mezitli"] = (36.7210, 34.4820, "Mersin / Mezitli - Sahil Narenciye"),
            ["mersin"] = (36.8121, 34.6415, "Mersin / Merkez"),
            ["adana"] = (36.9914, 35.3308, "Adana / Yüreğir - Portakal Bahçesi"),
            ["yüreğir"] = (36.9850, 35.3289, "Adana / Yüreğir - Portakal Bahçesi"),
            ["ceyhan"] = (37.0247, 35.8175, "Adana / Ceyhan - Çukurova"),
            ["kozan"] = (37.4552, 35.8158, "Adana / Kozan - Narenciye"),
            ["antalya"] = (36.8865, 30.7030, "Antalya / Muratpaşa - Narenciye"),
            ["alanya"] = (36.5438, 31.9998, "Antalya / Alanya - Narenciye"),
            ["finike"] = (36.2941, 30.1432, "Antalya / Finike - Portakal Ovası"),
            ["kumluca"] = (36.3703, 30.2889, "Antalya / Kumluca - Sera & Bahçe"),
            ["manavgat"] = (36.7869, 31.4422, "Antalya / Manavgat"),
            ["serik"] = (36.9172, 31.1000, "Antalya / Serik"),
            ["hatay"] = (36.2023, 36.1613, "Hatay / Antakya"),
            ["antakya"] = (36.2023, 36.1613, "Hatay / Antakya"),
            ["dörtyol"] = (36.8433, 36.2258, "Hatay / Dörtyol - Narenciye"),
            ["erzin"] = (36.9536, 36.2047, "Hatay / Erzin - Narenciye"),
            ["iskenderun"] = (36.5872, 36.1735, "Hatay / İskenderun"),
            ["muğla"] = (37.2153, 28.3636, "Muğla / Menteşe"),
            ["fethiye"] = (36.6217, 29.1164, "Muğla / Fethiye - Narenciye"),
            ["köyceğiz"] = (36.9689, 28.6942, "Muğla / Köyceğiz - Portakal Bahçesi"),
            ["ortaca"] = (36.8375, 28.7667, "Muğla / Ortaca - Limon Havzası"),
            ["bodrum"] = (37.0344, 27.4305, "Muğla / Bodrum - Mandalina"),
            ["aydın"] = (37.8481, 27.8453, "Aydın / Efeler"),
            ["nazilli"] = (37.9136, 28.3228, "Aydın / Nazilli - Bahçe"),
            ["söke"] = (37.7514, 27.4042, "Aydın / Söke Ovası"),
            ["kuşadası"] = (37.8579, 27.2610, "Aydın / Kuşadası"),
            ["izmir"] = (38.4192, 27.1287, "İzmir / Konak"),
            ["seferihisar"] = (38.1978, 26.8392, "İzmir / Seferihisar - Mandalina"),
            ["menderes"] = (38.2522, 27.1333, "İzmir / Menderes - Narenciye"),
            ["urla"] = (38.3225, 26.7642, "İzmir / Urla"),
            ["bursa"] = (40.1885, 29.0610, "Bursa / Nilüfer"),
            ["istanbul"] = (41.0082, 28.9784, "İstanbul / Kadıköy"),
            ["ankara"] = (39.9334, 32.8597, "Ankara / Çankaya"),
            ["konya"] = (37.8746, 32.4932, "Konya / Meram"),
            ["gaziantep"] = (37.0662, 37.3833, "Gaziantep / Şahinbey")
        };

        static GoogleMapsWindow()
        {
            if (!_geocoderClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _geocoderClient.DefaultRequestHeaders.Add("User-Agent", "MersiNovaApp/2.5 (Citrus Agricultural Decision Support)");
            }
        }

        public GoogleMapsWindow(string? initialLocation = null)
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(initialLocation))
            {
                SelectedLocationString = initialLocation.Trim();
                DisplayLocationText.Text = SelectedLocationString;

                // Attempt to parse coordinates if present in string e.g. (36.1234° K, 34.5678° D)
                ParseInitialCoordinates(SelectedLocationString);
            }

            Loaded += async (s, e) => await InitializeGoogleMapsAsync();
        }

        private void ParseInitialCoordinates(string loc)
        {
            try
            {
                int open = loc.LastIndexOf('(');
                int close = loc.LastIndexOf(')');
                if (open != -1 && close > open)
                {
                    string sub = loc.Substring(open + 1, close - open - 1);
                    var parts = sub.Split(',');
                    if (parts.Length == 2)
                    {
                        string p1 = parts[0].Replace("°", "").Replace("K", "").Replace("N", "").Trim();
                        string p2 = parts[1].Replace("°", "").Replace("D", "").Replace("E", "").Trim();

                        if (double.TryParse(p1, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                            double.TryParse(p2, NumberStyles.Any, CultureInfo.InvariantCulture, out double lng))
                        {
                            SelectedLatitude = lat;
                            SelectedLongitude = lng;
                        }
                    }
                }
            }
            catch { }
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
            DialogResult = false;
            Close();
        }

        private async Task InitializeGoogleMapsAsync()
        {
            try
            {
                string userDataFolder = Path.Combine(Path.GetTempPath(), "MersiNova_WebView2_Cache");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await MapWebView.EnsureCoreWebView2Async(env);

                MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                string htmlContent = GetGoogleMapsHtml(SelectedLatitude, SelectedLongitude);
                MapWebView.NavigateToString(htmlContent);

                await Task.Delay(500);
                MapLoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MapLoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show("Google Maps yüklenirken bir durum oluştu: " + ex.Message, "Google Maps Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string jsonString = e.WebMessageAsJson;
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.TryGetProperty("lat", out var latProp) && root.TryGetProperty("lng", out var lngProp))
                {
                    double lat = latProp.GetDouble();
                    double lng = lngProp.GetDouble();

                    SelectedLatitude = lat;
                    SelectedLongitude = lng;

                    // Perform real reverse geocoding to resolve ANY city/district in Turkey or abroad
                    string locationName = await ReverseGeocodeAsync(lat, lng);
                    SelectedLocationString = $"{locationName} ({lat:F4}° K, {lng:F4}° D)";

                    Dispatcher.Invoke(() =>
                    {
                        DisplayLocationText.Text = SelectedLocationString;
                        DisplayCoordsText.Text = $"Koordinat: {lat:F4}° K, {lng:F4}° D • Google Uydu Aktif";
                    });
                }
            }
            catch
            {
                // Fallback
            }
        }

        private async Task<string> ReverseGeocodeAsync(double lat, double lng)
        {
            try
            {
                string latStr = lat.ToString("F5", CultureInfo.InvariantCulture);
                string lonStr = lng.ToString("F5", CultureInfo.InvariantCulture);
                string url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latStr}&lon={lonStr}&zoom=14&addressdetails=1";

                string json = await _geocoderClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("address", out var addr))
                {
                    string province = "";
                    if (addr.TryGetProperty("province", out var provProp)) province = provProp.GetString() ?? "";
                    else if (addr.TryGetProperty("state", out var stateProp)) province = stateProp.GetString() ?? "";
                    else if (addr.TryGetProperty("city", out var cityProp)) province = cityProp.GetString() ?? "";

                    string district = "";
                    if (addr.TryGetProperty("town", out var townProp)) district = townProp.GetString() ?? "";
                    else if (addr.TryGetProperty("county", out var countyProp)) district = countyProp.GetString() ?? "";
                    else if (addr.TryGetProperty("suburb", out var subProp)) district = subProp.GetString() ?? "";
                    else if (addr.TryGetProperty("village", out var vilProp)) district = vilProp.GetString() ?? "";

                    province = province.Replace("İli", "").Replace("ili", "").Trim();

                    if (!string.IsNullOrWhiteSpace(province) && !string.IsNullOrWhiteSpace(district))
                    {
                        return $"{province} / {district}";
                    }
                    if (!string.IsNullOrWhiteSpace(province))
                    {
                        return $"{province} / Bahçe";
                    }
                }
            }
            catch
            {
                // Fallback to offline catalog or geometric resolution
            }

            // Quick geometric fallback for common regions
            return ResolveLocalFallback(lat, lng);
        }

        private static string ResolveLocalFallback(double lat, double lng)
        {
            if (lat >= 36.4 && lat <= 37.2 && lng >= 30.0 && lng <= 32.5)
                return "Antalya / Narenciye Parseli";
            if (lat >= 36.5 && lat <= 37.3 && lng >= 35.5 && lng <= 36.6)
                return "Hatay / Narenciye Bahçesi";
            if (lat >= 36.5 && lat <= 37.4 && lng >= 34.8 && lng <= 36.0)
                return "Adana / Çukurova Bahçesi";
            if (lat >= 36.5 && lat <= 37.3 && lng >= 33.7 && lng <= 34.8)
                return "Mersin / Narenciye Parseli";
            if (lat >= 36.5 && lat <= 37.5 && lng >= 27.0 && lng <= 29.5)
                return "Muğla / Narenciye Bahçesi";
            if (lat >= 37.5 && lat <= 38.2 && lng >= 27.0 && lng <= 29.0)
                return "Aydın / Narenciye Bahçesi";
            if (lat >= 38.0 && lat <= 39.0 && lng >= 26.5 && lng <= 27.8)
                return "İzmir / Mandalina Bahçesi";

            return $"Zirai Parsel ({lat:F3}° K, {lng:F3}° D)";
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            string cleanKey = query.ToLowerInvariant()
                                   .Replace("ili", "")
                                   .Replace("ilçesi", "")
                                   .Replace("mah.", "")
                                   .Replace("mahallesi", "")
                                   .Trim();

            // 1. Check local catalog first (instant, 0 latency)
            foreach (var kvp in _cityCatalog)
            {
                if (cleanKey.Contains(kvp.Key) || kvp.Key.Contains(cleanKey))
                {
                    await FlyToAsync(kvp.Value.Lat, kvp.Value.Lng, kvp.Value.FullName);
                    return;
                }
            }

            // 2. Query Real Online Geocoding (Nominatim OpenStreetMap)
            try
            {
                string encoded = Uri.EscapeDataString(query);
                string url = $"https://nominatim.openstreetmap.org/search?format=json&q={encoded}&countrycodes=tr&limit=1";
                string json = await _geocoderClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement;

                if (arr.GetArrayLength() > 0)
                {
                    var item = arr[0];
                    string latStr = item.GetProperty("lat").GetString() ?? "";
                    string lonStr = item.GetProperty("lon").GetString() ?? "";
                    string disp = item.GetProperty("display_name").GetString() ?? query;

                    if (double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lng))
                    {
                        var parts = disp.Split(',');
                        string shortName = parts.Length >= 2 ? $"{parts[0].Trim()} / {parts[1].Trim()}" : query;
                        await FlyToAsync(lat, lng, shortName);
                        return;
                    }
                }
            }
            catch
            {
                // Network error on search
            }

            // If not found, notify user
            MessageBox.Show($"'{query}' konumu bulunamadı. Lütfen il veya ilçe adını (örn: Antalya, Finike, Hatay, İzmir) kontrol ediniz veya harita üzerinden doğrudan tıklayarak seçiniz.", "Konum Arama", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
        }

        private async void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var parts = tag.Split(',');
                if (parts.Length >= 3 &&
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lng))
                {
                    await FlyToAsync(lat, lng, parts[2]);
                }
            }
        }

        private async Task FlyToAsync(double lat, double lng, string name)
        {
            SelectedLatitude = lat;
            SelectedLongitude = lng;
            SelectedLocationString = $"{name} ({lat:F4}° K, {lng:F4}° D)";
            DisplayLocationText.Text = SelectedLocationString;
            DisplayCoordsText.Text = $"Koordinat: {lat:F4}° K, {lng:F4}° D • Google Uydu Aktif";

            if (MapWebView.CoreWebView2 != null)
            {
                string script = $"window.setMapCenter({lat.ToString(CultureInfo.InvariantCulture)}, {lng.ToString(CultureInfo.InvariantCulture)}, 15);";
                await MapWebView.ExecuteScriptAsync(script);
            }
        }

        private void ConfirmLocation_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private static string GetGoogleMapsHtml(double initialLat, double initialLng)
        {
            string latStr = initialLat.ToString(CultureInfo.InvariantCulture);
            string lngStr = initialLng.ToString(CultureInfo.InvariantCulture);

            return $$"""
            <!DOCTYPE html>
            <html lang="tr">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
              <title>Google Maps Zirai Harita</title>
              <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
              <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
              <style>
                * { box-sizing: border-box; }
                html, body, #map { margin: 0; padding: 0; height: 100%; width: 100%; background: #081220; overflow: hidden; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
                .leaflet-control-zoom a { background: #0c1b33 !important; color: #00e676 !important; border-color: #1c3d68 !important; }
                .leaflet-control-layers { background: rgba(12, 27, 51, 0.92) !important; color: #eceff4 !important; border: 1.5px solid #1c3d68 !important; border-radius: 10px !important; box-shadow: 0 4px 14px rgba(0,0,0,0.5) !important; }
                .leaflet-control-layers-toggle { filter: invert(1); }
                .leaflet-popup-content-wrapper { background: #0c1b33 !important; color: #eceff4 !important; border: 1.5px solid #00e676 !important; border-radius: 12px !important; }
                .leaflet-popup-tip { background: #0c1b33 !important; }
                .badge { display: inline-block; background: #00e676; color: #04121e; padding: 2px 7px; border-radius: 6px; font-weight: bold; font-size: 11px; margin-bottom: 4px; }
              </style>
            </head>
            <body>
              <div id="map"></div>
              <script>
                // 1. Google Hybrid Satellite Layer (Satellite + Labels)
                var googleHybrid = L.tileLayer('https://mt{s}.google.com/vt/lyrs=y&x={x}&y={y}&z={z}', {
                  maxZoom: 21,
                  subdomains: ['0','1','2','3'],
                  attribution: '© Google Maps'
                });

                // 2. Google Road Map Layer
                var googleRoadmap = L.tileLayer('https://mt{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}', {
                  maxZoom: 21,
                  subdomains: ['0','1','2','3'],
                  attribution: '© Google Maps'
                });

                // 3. OpenStreetMap
                var osm = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                  maxZoom: 19,
                  attribution: '© OpenStreetMap'
                });

                var map = L.map('map', {
                  center: [{{latStr}}, {{lngStr}}],
                  zoom: 15,
                  layers: [googleHybrid]
                });

                var baseMaps = {
                  "🛰️ Google Uydu (Hibrit)": googleHybrid,
                  "🗺️ Google Yol Haritası": googleRoadmap,
                  "🌐 OpenStreetMap": osm
                };
                L.control.layers(baseMaps, null, { position: 'topright' }).addTo(map);

                // Custom Pin Icon
                var customIcon = L.icon({
                  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-green.png',
                  shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
                  iconSize: [25, 41],
                  iconAnchor: [12, 41],
                  popupAnchor: [1, -34],
                  shadowSize: [41, 41]
                });

                var marker = L.marker([{{latStr}}, {{lngStr}}], {
                  draggable: true,
                  icon: customIcon
                }).addTo(map);

                marker.bindPopup("<div style='text-align:center;'><span class='badge'>🍋 SEÇİLİ BAHÇE</span><br/><b>MersiNova Zirai Parseli</b><br/><small style='color:#8899aa;'>Konumu değiştirmek için haritada bir yere tıklayın veya pini sürükleyin.</small></div>").openPopup();

                function notifyCSharp(lat, lng) {
                  if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({
                      type: 'locationSelected',
                      lat: lat,
                      lng: lng
                    });
                  }
                }

                // Click to drop marker anywhere
                map.on('click', function(e) {
                  var lat = e.latlng.lat;
                  var lng = e.latlng.lng;
                  marker.setLatLng([lat, lng]);
                  marker.openPopup();
                  notifyCSharp(lat, lng);
                });

                // Drag marker
                marker.on('dragend', function(e) {
                  var pos = marker.getLatLng();
                  notifyCSharp(pos.lat, pos.lng);
                });

                // Global method callable from C#
                window.setMapCenter = function(lat, lng, zoom) {
                  map.setView([lat, lng], zoom || 15);
                  marker.setLatLng([lat, lng]);
                  marker.openPopup();
                  notifyCSharp(lat, lng);
                };
              </script>
            </body>
            </html>
            """;
        }
    }
}
