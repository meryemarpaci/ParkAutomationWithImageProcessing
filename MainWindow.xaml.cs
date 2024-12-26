using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tesseract;
using OpenCvSharp;


using OpenCvWindow = OpenCvSharp.Window; // Alias tanımlandı

namespace VeriTabaniProjesi
{
    public partial class MainWindow : System.Windows.Window
    {
        // MSSQL bağlantı dizesi
        private readonly string connectionString = "Server=localhost;Database=OtoparkSistemi;Trusted_Connection=True;";

        public MainWindow()
        {
            InitializeComponent();
            StyleDataGrid();
            // Başlangıçta sadece Araç Listesi gösterilir
            ShowAracListesi();
        }

        // DataGrid tasarımı
        private void StyleDataGrid()
        {
            // Araç Listesi için DataGrid stilini ayarlama
            dataGridAracListesi.GridLinesVisibility = DataGridGridLinesVisibility.None;
            dataGridAracListesi.RowBackground = Brushes.White;
            dataGridAracListesi.AlternatingRowBackground = Brushes.LightGray;
            dataGridAracListesi.BorderBrush = Brushes.DarkGray;
            dataGridAracListesi.BorderThickness = new Thickness(1);
            dataGridAracListesi.FontSize = 14;
            dataGridAracListesi.FontFamily = new FontFamily("Segoe UI");
            dataGridAracListesi.HorizontalAlignment = HorizontalAlignment.Stretch;
            dataGridAracListesi.VerticalAlignment = VerticalAlignment.Stretch;
            dataGridAracListesi.HeadersVisibility = DataGridHeadersVisibility.Column;
            dataGridAracListesi.ColumnHeaderHeight = 40;
            dataGridAracListesi.RowHeight = 35;
            dataGridAracListesi.Margin = new Thickness(10);

            // Diğer DataGrid'ler için benzer stiller uygulanabilir
            // dataGridKullanicilar, dataGridYetkiliPlakalar, dataGridOdemeler için de stil ayarlamalarını yapabilirsiniz
        }

        // OCR ile Plaka Okuma (Python)
        private void BtnPlakaOku_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pythonScript = "ocr_script.py";
                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("OCR script dosyası bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = pythonScript,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(output);

                    if (result.error != null)
                    {
                        MessageBox.Show($"Hata: {result.error}", "OCR Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (result.plate != null)
                    {
                        string plate = result.plate;
                        var confirmation = MessageBox.Show(
                            $"Algılanan Plaka: {plate}\nBu plakayı onaylıyor musunuz?",
                            "Plaka Onayı",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (confirmation == MessageBoxResult.Yes)
                        {
                            // Plakayı veritabanına kaydet
                            InsertAracGirisCikis(plate);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // OpenCV & Tesseract ile Manuel Plaka Okuma
        private string ReadPlateFromImage(Mat image)
        {
            try
            {
                Mat gray = new Mat();
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

                Mat blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);

                Mat edged = new Mat();
                Cv2.Canny(blurred, edged, 100, 200);

                OpenCvSharp.Point[][] contours;
                OpenCvSharp.HierarchyIndex[] hierarchy;
                Cv2.FindContours(edged, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                foreach (var contour in contours)
                {
                    var rect = Cv2.BoundingRect(contour);

                    // Plaka boyutu için basit bir kriter
                    if (rect.Width > rect.Height && rect.Width > 100)
                    {
                        Mat plateRegion = new Mat(image, rect);
                        Cv2.ImWrite("plate.jpg", plateRegion);

                        using (var engine = new TesseractEngine("tessdata", "eng", EngineMode.Default))
                        {
                            string tempPath = "temp_plate.jpg";
                            Cv2.ImWrite(tempPath, plateRegion);

                            using (var img = Pix.LoadFromFile(tempPath))
                            {
                                using (var page = engine.Process(img))
                                {
                                    string text = page.GetText();
                                    return text.Trim();
                                }
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OCR Hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        // Araç Girişi (manuel veya OCR sonrası)
        private void BtnAracGirisi_Click(object sender, RoutedEventArgs e)
        {
            InsertAracGirisCikis(null);
        }

        private void InsertAracGirisCikis(string plaka)
        {
            try
            {
                // Plaka dışarıdan gelmezse, manuel InputBox
                if (string.IsNullOrWhiteSpace(plaka))
                {
                    plaka = Microsoft.VisualBasic.Interaction.InputBox("Lütfen aracın plakasını girin:", "Plaka Girişi", "");
                    if (string.IsNullOrWhiteSpace(plaka))
                    {
                        MessageBox.Show("Geçerli bir plaka girmelisiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Şirket ID girilmemişse InputBox
                string sirketIdInput = Microsoft.VisualBasic.Interaction.InputBox("Lütfen şirket ID'sini girin:", "Şirket ID Girişi", "");
                if (string.IsNullOrWhiteSpace(sirketIdInput) || !int.TryParse(sirketIdInput, out int sirketIdValue))
                {
                    MessageBox.Show("Geçerli bir Şirket ID girmelisiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int sirketId = sirketIdValue;

                // YetkiliPlakalar kontrolü
                bool yetkiliPlaka = IsAuthorizedPlate(plaka, sirketId);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO AracGirisCikis 
                                    (Plaka, GirisTarihi, SirketID, Durum) 
                                    VALUES (@Plaka, @GirisTarihi, @SirketID, 'GirisYapildi')";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Plaka", plaka);
                    command.Parameters.AddWithValue("@GirisTarihi", DateTime.Now);
                    command.Parameters.AddWithValue("@SirketID", sirketId);
                    command.ExecuteNonQuery();

                    if (yetkiliPlaka)
                    {
                        MessageBox.Show("Yetkili plaka giriş yaptı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Plaka başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // YetkiliPlakalar Tablosunda Sorgu
        private bool IsAuthorizedPlate(string plaka, int sirketId)
        {
            bool result = false;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string checkQuery = @"SELECT COUNT(*) FROM YetkiliPlakalar 
                                         WHERE Plaka = @Plaka AND SirketID = @SirketID";
                    SqlCommand cmd = new SqlCommand(checkQuery, connection);
                    cmd.Parameters.AddWithValue("@Plaka", plaka);
                    cmd.Parameters.AddWithValue("@SirketID", sirketId);

                    int count = (int)cmd.ExecuteScalar();
                    result = (count > 0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"YetkiliPlakalar kontrol hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return result;
        }

        // Araç Listesi Butonu
        private void BtnAracListesi_Click(object sender, RoutedEventArgs e)
        {
            ShowAracListesi();
        }

        private void ShowAracListesi()
        {
            // Tüm içerikleri gizle
            dataGridSirketler.Visibility = Visibility.Collapsed;
            chartPanel.Visibility = Visibility.Collapsed;
            dataGridKullanicilar.Visibility = Visibility.Collapsed;
            dataGridYetkiliPlakalar.Visibility = Visibility.Collapsed;
            dataGridOdemeler.Visibility = Visibility.Collapsed;

            // Araç Listesi göster
            dataGridAracListesi.Visibility = Visibility.Visible;

            // Veri yükleme
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            AracID, 
                            Plaka, 
                            GirisTarihi, 
                            CikisTarihi, 
                            SirketID,
                            Durum 
                        FROM AracGirisCikis";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    dataGridAracListesi.ItemsSource = dataTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Programı Kapatma Butonu
        private void BtnCikis_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Araç Çıkışı
        private void BtnAracCikisi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string plaka = Microsoft.VisualBasic.Interaction.InputBox("Lütfen çıkış yapacak aracın plakasını girin:", "Araç Çıkışı", "");
                if (string.IsNullOrWhiteSpace(plaka))
                {
                    MessageBox.Show("Lütfen geçerli bir plaka girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // 1) GirisYapildi olan son kaydı bul
                    string selectQuery = @"
                        SELECT TOP 1 AracID 
                        FROM AracGirisCikis 
                        WHERE Plaka = @Plaka AND Durum = 'GirisYapildi' 
                        ORDER BY GirisTarihi DESC";
                    SqlCommand selectCommand = new SqlCommand(selectQuery, connection);
                    selectCommand.Parameters.AddWithValue("@Plaka", plaka);

                    object aracIdObj = selectCommand.ExecuteScalar();
                    if (aracIdObj == null)
                    {
                        MessageBox.Show("Bu plakaya ait çıkış yapabilecek bir kayıt bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int aracId = (int)aracIdObj;

                    // 2) AracGirisCikis tablosunda çıkış işlemi
                    string updateQuery = @"
                        UPDATE AracGirisCikis 
                        SET CikisTarihi = @CikisTarihi, Durum = 'CikisYapildi' 
                        WHERE AracID = @AracID";
                    SqlCommand updateCommand = new SqlCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@CikisTarihi", DateTime.Now);
                    updateCommand.Parameters.AddWithValue("@AracID", aracId);
                    updateCommand.ExecuteNonQuery();

                    MessageBox.Show("Araç çıkışı başarıyla gerçekleşti.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                    // İsteğe bağlı otomatik ücret hesaplama
                    // AutoCalculateAndSavePayment(aracId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Ücret Hesaplama & Odemeler Tablosu Kaydı
        private void BtnUcretHesapla_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string plaka = Microsoft.VisualBasic.Interaction.InputBox("Lütfen ücretini hesaplayacağınız aracın plakasını girin:", "Ücret Hesaplama", "");
                if (string.IsNullOrWhiteSpace(plaka))
                {
                    MessageBox.Show("Lütfen geçerli bir plaka girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // 1) Son çıkış yapan kaydı bul
                    string query = @"
                        SELECT TOP 1 
                            AracID, 
                            GirisTarihi, 
                            CikisTarihi, 
                            SirketID
                        FROM AracGirisCikis 
                        WHERE Plaka = @Plaka AND Durum = 'CikisYapildi' 
                        ORDER BY CikisTarihi DESC";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Plaka", plaka);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int aracId = reader.GetInt32(0);
                            DateTime girisTarihi = reader.GetDateTime(1);
                            DateTime cikisTarihi = reader.GetDateTime(2);
                            int sirketID = reader.GetInt32(3);

                            // 2) Süre hesabı
                            TimeSpan sure = cikisTarihi - girisTarihi;
                            double saat = sure.TotalHours;
                            if (saat < 1) saat = 1; // ilk saat

                            // 3) Şirketin saatlik ücreti
                            decimal saatlikUcret = GetSirketSaatlikUcret(sirketID);

                            // 4) Toplam ücret hesapla
                            double ucret = Math.Ceiling(saat) * (double)saatlikUcret;

                            MessageBox.Show(
                                $"Aracın otoparkta kaldığı süre: {Math.Ceiling(saat)} saat\n" +
                                $"Toplam Ücret: {ucret} TL",
                                "Ücret Hesaplama",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            // 5) Odemeler tablosuna kaydet
                            SavePayment(aracId, (decimal)ucret);
                        }
                        else
                        {
                            MessageBox.Show("Bu plakaya ait hesaplanacak bir ücret kaydı bulunamadı.",
                                "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private decimal GetSirketSaatlikUcret(int sirketID)
        {
            decimal saatlikUcret = 50; // Varsayılan
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT SaatlikUcret FROM Sirketler WHERE SirketID = @SirketID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@SirketID", sirketID);

                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        saatlikUcret = (decimal)result;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şirket ücreti sorgulama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return saatlikUcret;
        }

        private void SavePayment(int aracID, decimal ucret)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string insertQuery = @"
                        INSERT INTO Odemeler (AracID, OdemeMiktari, OdemeTarihi)
                        VALUES (@AracID, @OdemeMiktari, GETDATE())";
                    SqlCommand cmd = new SqlCommand(insertQuery, connection);
                    cmd.Parameters.AddWithValue("@AracID", aracID);
                    cmd.Parameters.AddWithValue("@OdemeMiktari", ucret);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ödeme kaydı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Şirketler Butonu ve Gösterimi
        private void BtnSirketler_Click(object sender, RoutedEventArgs e)
        {
            // Tüm içerikleri gizle
            dataGridAracListesi.Visibility = Visibility.Collapsed;
            dataGridKullanicilar.Visibility = Visibility.Collapsed;
            dataGridYetkiliPlakalar.Visibility = Visibility.Collapsed;
            dataGridOdemeler.Visibility = Visibility.Collapsed;

            // Şirketler içerik alanını göster
            SirketlerContentArea.Visibility = Visibility.Visible;
            chartPanel.Visibility = Visibility.Visible;

            // Şirket verilerini yükle ve grafikleri çiz
            LoadSirketler();
            DrawCharts();
        }

        // Kullanıcılar Butonu ve Gösterimi
        private void BtnKullanicilar_Click(object sender, RoutedEventArgs e)
        {
            // Tüm içerikleri gizle
            dataGridAracListesi.Visibility = Visibility.Collapsed;
            SirketlerContentArea.Visibility = Visibility.Collapsed;
            dataGridYetkiliPlakalar.Visibility = Visibility.Collapsed;
            dataGridOdemeler.Visibility = Visibility.Collapsed;

            // Kullanıcılar DataGrid'i göster
            dataGridKullanicilar.Visibility = Visibility.Visible;

            // Kullanıcı verilerini yükle
            LoadKullanicilar();
        }

        // Yetkili Plakalar Butonu ve Gösterimi
        private void BtnYetkiliPlakalar_Click(object sender, RoutedEventArgs e)
        {
            // Tüm içerikleri gizle
            dataGridAracListesi.Visibility = Visibility.Collapsed;
            SirketlerContentArea.Visibility = Visibility.Collapsed;
            dataGridKullanicilar.Visibility = Visibility.Collapsed;
            dataGridOdemeler.Visibility = Visibility.Collapsed;

            // Yetkili Plakalar DataGrid'i göster
            dataGridYetkiliPlakalar.Visibility = Visibility.Visible;

            // Yetkili Plakalar verilerini yükle
            LoadYetkiliPlakalar();
        }

        // Ödemeler Butonu ve Gösterimi
        private void BtnOdemeler_Click(object sender, RoutedEventArgs e)
        {
            // Tüm içerikleri gizle
            dataGridAracListesi.Visibility = Visibility.Collapsed;
            SirketlerContentArea.Visibility = Visibility.Collapsed;
            dataGridKullanicilar.Visibility = Visibility.Collapsed;
            dataGridYetkiliPlakalar.Visibility = Visibility.Collapsed;

            // Ödemeler DataGrid'i göster
            dataGridOdemeler.Visibility = Visibility.Visible;

            // Ödemeler verilerini yükle
            LoadOdemeler();
        }

        // Şirketler Verisini Yükleme
        private void LoadSirketler()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            s.SirketAdi AS [Şirket Adı],
                            s.OtoparkKapasitesi AS [Kapasite],
                            (SELECT COUNT(*) FROM AracGirisCikis WHERE SirketID = s.SirketID AND Durum = 'GirisYapildi') AS [İçerideki Araç],
                            (
                                s.OtoparkKapasitesi 
                                - (SELECT COUNT(*) FROM AracGirisCikis WHERE SirketID = s.SirketID AND Durum = 'GirisYapildi')
                            ) AS [Boş Alan]
                        FROM Sirketler s";

                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    dataGridSirketler.ItemsSource = dataTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Şirket Pasta Grafikleri
        private void DrawCharts()
        {
            // Mevcut chartPanel içeriğini temizle
            chartPanel.Children.Clear();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            s.SirketAdi,
                            (SELECT COUNT(*) FROM AracGirisCikis WHERE SirketID = s.SirketID AND Durum = 'GirisYapildi') AS IceridekiArac,
                            s.OtoparkKapasitesi
                        FROM Sirketler s";

                    SqlCommand command = new SqlCommand(query, connection);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sirketAdi = reader["SirketAdi"].ToString();
                            int iceridekiArac = (int)reader["IceridekiArac"];
                            int kapasite = (int)reader["OtoparkKapasitesi"];

                            double dolulukOrani = 0;
                            if (kapasite > 0)
                                dolulukOrani = (double)iceridekiArac / kapasite;

                            // Grid oluştur
                            Grid grafikGrid = new Grid
                            {
                                Width = 200,
                                Height = 250,
                                Margin = new Thickness(10)
                            };

                            // Pasta grafiği
                            var doluDilim = CreatePieSlice(100, 100, 80, 0, dolulukOrani * 360, Brushes.Blue);
                            var bosDilim = CreatePieSlice(100, 100, 80, dolulukOrani * 360, 360 - dolulukOrani * 360, Brushes.Gray);

                            grafikGrid.Children.Add(bosDilim);
                            grafikGrid.Children.Add(doluDilim);

                            // Şirket adı ve doluluk oranı
                            TextBlock sirketText = new TextBlock
                            {
                                Text = $"{sirketAdi}\nDoluluk: %{dolulukOrani * 100:F2}",
                                FontSize = 14,
                                FontWeight = FontWeights.Bold,
                                TextAlignment = TextAlignment.Center,
                                Margin = new Thickness(0, 180, 0, 0),
                                VerticalAlignment = VerticalAlignment.Top
                            };
                            grafikGrid.Children.Add(sirketText);

                            // StackPanel'e ekle
                            chartPanel.Children.Add(grafikGrid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Yardımcı: Pasta Grafik Dilimi
        private System.Windows.Shapes.Path CreatePieSlice(double centerX, double centerY, double radius, double startAngle, double sweepAngle, Brush fillBrush)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            double x1 = centerX + radius * Math.Cos(startRad);
            double y1 = centerY + radius * Math.Sin(startRad);
            double x2 = centerX + radius * Math.Cos(endRad);
            double y2 = centerY + radius * Math.Sin(endRad);

            bool isLargeArc = sweepAngle > 180.0;

            var figure = new System.Windows.Media.PathFigure
            {
                StartPoint = new System.Windows.Point(centerX, centerY),
                IsClosed = true
            };
            figure.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(x1, y1), true));
            figure.Segments.Add(
                new System.Windows.Media.ArcSegment(
                    new System.Windows.Point(x2, y2),
                    new System.Windows.Size(radius, radius),
                    0,
                    isLargeArc,
                    System.Windows.Media.SweepDirection.Clockwise,
                    true
                )
            );
            figure.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(centerX, centerY), true));

            var geometry = new System.Windows.Media.PathGeometry();
            geometry.Figures.Add(figure);

            return new System.Windows.Shapes.Path
            {
                Data = geometry,
                Fill = fillBrush
            };
        }

        // Kullanıcılar Verisini Yükleme
        private void LoadKullanicilar()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT KullaniciID, AdSoyad, Email, Sifre, SirketID FROM Kullanicilar";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridKullanicilar.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata (Kullanıcılar): " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Yetkili Plakalar Verisini Yükleme
        private void LoadYetkiliPlakalar()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT YetkiliID, Plaka, SirketID FROM YetkiliPlakalar";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridYetkiliPlakalar.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata (YetkiliPlakalar): " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Ödemeler Verisini Yükleme
        private void LoadOdemeler()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT OdemeID, AracID, OdemeTarihi, OdemeMiktari FROM Odemeler ORDER BY OdemeTarihi DESC";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridOdemeler.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata (Odemeler): " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
