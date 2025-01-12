using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tesseract;
using System.Windows.Shapes;
using OpenCvSharp;
using Microsoft.VisualBasic; // Interaction.InputBox vb. için
using Newtonsoft.Json;
using System.Collections.Generic; // List<(int,int)> için
using Window = System.Windows.Window;

namespace VeriTabaniProjesi
{
    public partial class MainWindow : Window
    {
        // MSSQL
        private readonly string connectionString = "Server=localhost;Database=OtoparkSistemi;Trusted_Connection=True;";

        // Şu an giriş yapmış olan şirket (null => giriş yok)
        private int? loggedInSirketID = null;
        private string loggedInSirketAdi = null;

        public MainWindow()
        {
            InitializeComponent();
            StyleDataGrid();
            ShowAracListesi(); // Açılışta genel liste
        }

        private void StyleDataGrid()
        {
            // dataGridAracListesi stilleri
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
        }

        // ========== GİRİŞ / ÇIKIŞ ==========
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // E-posta ve Şifre al
                string email = Interaction.InputBox("E-posta adresinizi girin:", "Giriş Yap", "");
                if (string.IsNullOrWhiteSpace(email))
                {
                    MessageBox.Show("E-posta girmelisiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string sifre = Interaction.InputBox("Şifrenizi girin:", "Giriş Yap", "");
                if (string.IsNullOrWhiteSpace(sifre))
                {
                    MessageBox.Show("Şifre girmelisiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT SirketID, SirketAdi
                        FROM Sirketler
                        WHERE Email = @Email
                          AND Sifre = @Sifre";

                    SqlCommand cmd = new SqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Sifre", sifre);

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            loggedInSirketID = dr.GetInt32(0);
                            loggedInSirketAdi = dr.GetString(1);

                            // Üst tarafta şirket adını göster
                            TxtLoggedCompany.Text = $"[ {loggedInSirketAdi} ]";

                            // Giriş yapıldıktan sonra logout butonu görünsün, login butonu gizlensin
                            BtnLogin.Visibility = Visibility.Collapsed;
                            BtnLogout.Visibility = Visibility.Visible;

                            MessageBox.Show($"Hoşgeldiniz, {loggedInSirketAdi}.", "Giriş Başarılı",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            // Şimdi araç listesini, sadece bu şirkete göre filtreleyelim
                            ShowAracListesi();
                        }
                        else
                        {
                            MessageBox.Show("Geçersiz e-posta veya şifre!", "Hata",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Giriş hatası: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            // Değişkenleri sıfırla
            loggedInSirketID = null;
            loggedInSirketAdi = null;

            // Üstteki şirket adını boşalt
            TxtLoggedCompany.Text = "";

            // Butonların görünümü eski haline döndür
            BtnLogout.Visibility = Visibility.Collapsed;
            BtnLogin.Visibility = Visibility.Visible;

            // Araç listesini tekrar tüm şirketleri gösterecek hale getirelim
            ShowAracListesi();

            MessageBox.Show("Çıkış yapıldı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ========== OCR ile Plaka Okuma (Python) ==========
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

                    dynamic result = JsonConvert.DeserializeObject(output);

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
                            AutoAracGirisCikis(plate);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========== OpenCV & Tesseract ile manuel OCR ==========
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
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(edged, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                foreach (var contour in contours)
                {
                    var rect = Cv2.BoundingRect(contour);

                    // Basit boyut kriteri
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

        // ========== Araç Girişi ==========
        private void BtnAracGirisi_Click(object sender, RoutedEventArgs e)
        {
 
            InsertAracGirisCikis(null);


        }


        private void AracGirisiYap(string plaka, int sirketId)
        {
            try
            {
                bool yetkiliPlaka = IsAuthorizedPlate(plaka, sirketId);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO AracGirisCikis (Plaka, GirisTarihi, SirketID, Durum)
                        VALUES (@Plaka, @GirisTarihi, @SirketID, 'GirisYapildi')";

                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Plaka", plaka);
                    command.Parameters.AddWithValue("@GirisTarihi", DateTime.Now);
                    command.Parameters.AddWithValue("@SirketID", sirketId);
                    command.ExecuteNonQuery();
                }

                if (yetkiliPlaka)
                {
                    // Ek bilgi mesajı
                    if (loggedInSirketID.HasValue && loggedInSirketID.Value == sirketId)
                    {
                        MessageBox.Show(
                            $"Sizin şirketinizin yetkili plakası ({plaka}) giriş yaptı!", 
                            "Bilgi",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Yetkili plaka ({plaka}) giriş yaptı.",
                            "Bilgi",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show($"Plaka ({plaka}) giriş yaptı.", "Başarılı", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AracGirisi hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AracCikisiYap(int aracId, string plaka)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string updateQuery = @"
                        UPDATE AracGirisCikis
                        SET CikisTarihi = @CikisTarihi,
                            Durum = 'CikisYapildi'
                        WHERE AracID = @AracID";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@CikisTarihi", DateTime.Now);
                        cmd.Parameters.AddWithValue("@AracID", aracId);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show($"Plaka ({plaka}) için çıkış yapıldı.", "Başarılı", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AracCikisi hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void AutoAracGirisCikis(string plaka)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plaka))
                {
                    plaka = Interaction.InputBox("Lütfen aracın plakasını girin:", "Plaka Girişi/Çıkışı", "");
                    if (string.IsNullOrWhiteSpace(plaka))
                    {
                        MessageBox.Show("Geçerli bir plaka girmelisiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                int aracID = -1;

                // Plakadan AracID'yi al
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT AracID FROM Arac WHERE Plaka = @Plaka";
                    SqlCommand cmd = new SqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@Plaka", plaka);

                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        MessageBox.Show("Bu plakaya ait bir araç bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    aracID = (int)result;
                }

                bool aracinIciKaydiVar = false;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT TOP 1 AracID
                        FROM AracGirisCikis
                        WHERE AracID = @AracID
                        AND Durum = 'GirisYapildi'
                        ORDER BY GirisTarihi DESC";

                    SqlCommand cmd = new SqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@AracID", aracID);
                    object result = cmd.ExecuteScalar();
                    aracinIciKaydiVar = result != null;
                }

                if (aracinIciKaydiVar)
                {
                    // Çıkış işlemi
                    AracCikisiYap(aracID,plaka);
                }
                else
                {
                    // Giriş işlemi
                    InsertAracGirisCikis(plaka);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AutoAracGirisCikis hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private void InsertAracGirisCikis(string plaka)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plaka))
                {
                    plaka = Interaction.InputBox("Lütfen aracın plakasını girin:", "Plaka Girişi", "");
                    if (string.IsNullOrWhiteSpace(plaka))
                    {
                        MessageBox.Show("Geçerli bir plaka girmelisiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                int aracID = -1;
                bool yetkiliArac = false;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    
                    // Önce plakayı Arac tablosunda kontrol et
                    string checkQuery = "SELECT AracID FROM Arac WHERE Plaka = @Plaka";
                    SqlCommand checkCmd = new SqlCommand(checkQuery, connection);
                    checkCmd.Parameters.AddWithValue("@Plaka", plaka);

                    object result = checkCmd.ExecuteScalar();
                    
                    if (result == null)
                    {
                        // Araç yoksa, önce Arac tablosuna ekle
                        string insertAracQuery = "INSERT INTO Arac (Plaka) VALUES (@Plaka); SELECT SCOPE_IDENTITY();";
                        SqlCommand insertAracCmd = new SqlCommand(insertAracQuery, connection);
                        insertAracCmd.Parameters.AddWithValue("@Plaka", plaka);
                        
                        aracID = Convert.ToInt32(insertAracCmd.ExecuteScalar());
                    }
                    else
                    {
                        aracID = Convert.ToInt32(result);
                    }

                    // Yetkili plaka kontrolü
                    string yetkiliKontrolQuery = "SELECT COUNT(1) FROM YetkiliPlakalar WHERE Plaka = @Plaka";
                    SqlCommand yetkiliKontrolCmd = new SqlCommand(yetkiliKontrolQuery, connection);
                    yetkiliKontrolCmd.Parameters.AddWithValue("@Plaka", plaka);
                    yetkiliArac = Convert.ToInt32(yetkiliKontrolCmd.ExecuteScalar()) > 0;

                    // Şirket ID'sini belirle
                    int sirketId = 0;
                    if (!loggedInSirketID.HasValue)
                    {
                        string sirketIdInput = Interaction.InputBox("Lütfen şirket ID'sini girin:", "Şirket ID Girişi", "");
                        if (string.IsNullOrWhiteSpace(sirketIdInput) || !int.TryParse(sirketIdInput, out sirketId))
                        {
                            MessageBox.Show("Geçerli bir Şirket ID girmelisiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        sirketId = loggedInSirketID.Value;
                    }

                    // AracGirisCikis tablosuna giriş kaydı ekle
                    string insertGirisQuery = @"
                        INSERT INTO AracGirisCikis (AracID, Plaka, GirisTarihi, SirketID, Durum)
                        VALUES (@AracID, @Plaka, @GirisTarihi, @SirketID, 'GirisYapildi')";

                    SqlCommand insertGirisCmd = new SqlCommand(insertGirisQuery, connection);
                    insertGirisCmd.Parameters.AddWithValue("@AracID", aracID);
                    insertGirisCmd.Parameters.AddWithValue("@Plaka", plaka);
                    insertGirisCmd.Parameters.AddWithValue("@GirisTarihi", DateTime.Now);
                    insertGirisCmd.Parameters.AddWithValue("@SirketID", sirketId);
                    insertGirisCmd.ExecuteNonQuery();
                }

                string mesaj = yetkiliArac 
                    ? $"Yetkili araç ({plaka}) giriş kaydı başarıyla oluşturuldu."
                    : $"Araç ({plaka}) giriş kaydı başarıyla oluşturuldu.";

                MessageBox.Show(mesaj, "Başarılı", MessageBoxButton.OK, 
                    yetkiliArac ? MessageBoxImage.Information : MessageBoxImage.Information);
            
                ShowAracListesi(); // Listeyi güncelle
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private bool IsAuthorizedPlate(string plaka, int sirketId)
        {
            bool result = false;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string checkQuery = @"
                        SELECT COUNT(*) 
                        FROM YetkiliPlakalar 
                        WHERE Plaka = @Plaka
                          AND SirketID = @SirketID";

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

        // ========== Araç Listesi ==========
        private void BtnAracListesi_Click(object sender, RoutedEventArgs e)
        {
            ShowAracListesi();
        }

        private void ShowAracListesi()
        {
            // Tüm içerikleri gizle
            dataGridSirketler.Visibility = Visibility.Collapsed;
            chartPanel.Visibility = Visibility.Collapsed;
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

                    string query;
                    if (loggedInSirketID.HasValue)
                    {
                        // Giriş yapılmışsa sadece o şirkete ait araçlar
                        query = @"
                            SELECT 
                                AracID, 
                                Plaka, 
                                GirisTarihi, 
                                CikisTarihi, 
                                SirketID,
                                Durum 
                            FROM AracGirisCikis
                            WHERE SirketID = @SirketID";
                    }
                    else
                    {
                        // Giriş yapılmamışsa tüm araçlar
                        query = @"
                            SELECT 
                                AracID, 
                                Plaka, 
                                GirisTarihi, 
                                CikisTarihi, 
                                SirketID,
                                Durum 
                            FROM AracGirisCikis";
                    }

                    SqlCommand cmd = new SqlCommand(query, connection);
                    if (loggedInSirketID.HasValue)
                    {
                        cmd.Parameters.AddWithValue("@SirketID", loggedInSirketID.Value);
                    }

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
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

        // Programı kapatma butonu
        private void BtnCikis_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ========== Araç Çıkışı ==========
        private void BtnAracCikisi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string plaka = Interaction.InputBox("Lütfen çıkış yapacak aracın plakasını girin:", "Araç Çıkışı", "");
                if (string.IsNullOrWhiteSpace(plaka))
                {
                    MessageBox.Show("Lütfen geçerli bir plaka girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // GirisYapildi olan son kaydı bul
                    string selectQuery = @"
                        SELECT TOP 1 AracID
                        FROM AracGirisCikis
                        WHERE Plaka = @Plaka
                          AND Durum = 'GirisYapildi'
                        ORDER BY GirisTarihi DESC";

                    SqlCommand selectCommand = new SqlCommand(selectQuery, connection);
                    selectCommand.Parameters.AddWithValue("@Plaka", plaka);

                    object aracIdObj = selectCommand.ExecuteScalar();
                    if (aracIdObj == null)
                    {
                        MessageBox.Show("Bu plakaya ait çıkış yapabilecek bir kayıt bulunamadı.", "Bilgi", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int aracId = (int)aracIdObj;

                    // AracGirisCikis tablosunda çıkış işlemi
                    string updateQuery = @"
                        UPDATE AracGirisCikis
                        SET CikisTarihi = @CikisTarihi,
                            Durum = 'CikisYapildi'
                        WHERE AracID = @AracID";

                    SqlCommand updateCommand = new SqlCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@CikisTarihi", DateTime.Now);
                    updateCommand.Parameters.AddWithValue("@AracID", aracId);
                    updateCommand.ExecuteNonQuery();

                    MessageBox.Show("Araç çıkışı başarıyla gerçekleşti.", "Başarılı", 
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // İsteğe bağlı: otomatik ücret hesaplama
                    // AutoCalculateAndSavePayment(aracId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========== Ücret Hesaplama ==========
        private void BtnUcretHesapla_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string plaka = Interaction.InputBox("Lütfen ücretini hesaplayacağınız aracın plakasını girin:", "Ücret Hesaplama", "");
                if (string.IsNullOrWhiteSpace(plaka))
                {
                    MessageBox.Show("Lütfen geçerli bir plaka girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Son çıkış yapan kaydı bul
                    string query = @"
                        SELECT TOP 1 
                            AracID, 
                            GirisTarihi, 
                            CikisTarihi, 
                            SirketID
                        FROM AracGirisCikis
                        WHERE Plaka = @Plaka
                          AND Durum = 'CikisYapildi'
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

                            // Süre hesabı
                            TimeSpan sure = cikisTarihi - girisTarihi;
                            double saat = sure.TotalHours;
                            if (saat < 1) saat = 1; // ilk saat

                            // Şirketin saatlik ücreti
                            decimal saatlikUcret = GetSirketSaatlikUcret(sirketID);

                            // Toplam ücret
                            double ucret = Math.Ceiling(saat) * (double)saatlikUcret;

                            MessageBox.Show(
                                $"Aracın otoparkta kaldığı süre: {Math.Ceiling(saat)} saat\n" +
                                $"Toplam Ücret: {ucret} TL",
                                "Ücret Hesaplama",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );

                            // Odemeler tablosuna kaydet
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

        // ========== Şirketler ==========
        private void BtnSirketler_Click(object sender, RoutedEventArgs e)
        {
            // Tüm içerikleri gizle
            dataGridAracListesi.Visibility = Visibility.Collapsed;
            dataGridYetkiliPlakalar.Visibility = Visibility.Collapsed;
            dataGridOdemeler.Visibility = Visibility.Collapsed;

            SirketlerContentArea.Visibility = Visibility.Visible;
            chartPanel.Visibility = Visibility.Visible;

            LoadSirketler();
            DrawCharts(); // Artık DrawCharts() giriş varsa tek şirket, yoksa tüm şirket
        }

        private void LoadSirketler()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            s.SirketAdi AS [SirketAdi],
                            s.OtoparkKapasitesi AS [Kapasite],
                            s.SaatlikUcret AS [SaatlikÜcret],
                            (SELECT COUNT(*) FROM AracGirisCikis 
                                WHERE SirketID = s.SirketID AND Durum = 'GirisYapildi') AS [IceridekiArac],
                            (
                                s.OtoparkKapasitesi -
                                (SELECT COUNT(*) FROM AracGirisCikis 
                                    WHERE SirketID = s.SirketID AND Durum = 'GirisYapildi')
                            ) AS [BosAlan]
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

        /// <summary>
        /// Giriş yoksa tüm şirketler, giriş varsa tek şirketin grafiklerini çizer.
        /// </summary>
        private void DrawCharts()
        {
            chartPanel.Children.Clear();

            if (loggedInSirketID.HasValue)
            {
                // 1) pasta 
                DrawSingleCompanyPieChart(loggedInSirketID.Value);

                // 2) çizgi grafiği
                DrawDailyLineChart(loggedInSirketID.Value);
            }
            else
            {
                
                DrawAllCompaniesCharts();
            }
        }

        /// <summary>
        /// Tüm şirketler için pasta grafiğini (doluluk oranı) çizer.
        /// </summary>
        private void DrawAllCompaniesCharts()
        {
            chartPanel.Children.Clear();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            s.SirketAdi,
                            s.SaatlikUcret,
                            (SELECT COUNT(*) FROM AracGirisCikis 
                                WHERE SirketID = s.SirketID 
                                AND Durum = 'GirisYapildi') AS IceridekiArac,
                            s.OtoparkKapasitesi
                        FROM Sirketler s";

                    SqlCommand command = new SqlCommand(query, connection);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sirketAdi = reader["SirketAdi"].ToString();
                            decimal saatlikUcret = (decimal)reader["SaatlikUcret"];
                            int iceridekiArac = (int)reader["IceridekiArac"];
                            int kapasite = (int)reader["OtoparkKapasitesi"];

                            double dolulukOrani = 0;
                            if (kapasite > 0)
                                dolulukOrani = (double)iceridekiArac / kapasite;

                            // Saatlik ücreti de CreatePieChartGrid'e gönderiyoruz
                            Grid grafikGrid = CreatePieChartGrid(sirketAdi, dolulukOrani, saatlikUcret);
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


        /// <summary>
        /// Sadece tek şirkete ait pasta grafiği çizer.
                /// </summary>
        private void DrawSingleCompanyPieChart(int sirketID)
        {
            chartPanel.Children.Clear();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            s.SirketAdi,
                            s.SaatlikUcret,
                            (SELECT COUNT(*) FROM AracGirisCikis 
                                WHERE SirketID = s.SirketID 
                                AND Durum = 'GirisYapildi') AS IceridekiArac,
                            s.OtoparkKapasitesi
                        FROM Sirketler s
                        WHERE s.SirketID = @sID";

                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@sID", sirketID);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string sirketAdi = reader["SirketAdi"].ToString();
                            decimal saatlikUcret = (decimal)reader["SaatlikUcret"];
                            int iceridekiArac = (int)reader["IceridekiArac"];
                            int kapasite = (int)reader["OtoparkKapasitesi"];

                            double dolulukOrani = 0;
                            if (kapasite > 0)
                                dolulukOrani = (double)iceridekiArac / kapasite;

                            Grid grafikGrid = CreatePieChartGrid(sirketAdi, dolulukOrani, saatlikUcret);
                            chartPanel.Children.Add(grafikGrid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pasta Grafik Hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// Basit bir Canvas üzerinde aylık araç girişini çizgi grafiği olarak çizer.
        /// (Bu yıl bazlı örnek)
        /// </summary>
        private void DrawDailyLineChart(int sirketID)
        {
            // Canvas oluştur
            Canvas lineChartCanvas = new Canvas
            {
                Width = 400,
                Height = 300,
                Margin = new Thickness(20)
            };

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Örnek sorgu: Son 7 güne bakalım
                    string query = @"
                        SELECT CAST(GirisTarihi as DATE) AS Gun,
                            COUNT(*) AS AracCount
                        FROM AracGirisCikis
                        WHERE SirketID = @SirketID
                        AND GirisTarihi >= DATEADD(DAY, -7, CAST(GETDATE() as date))
                        GROUP BY CAST(GirisTarihi as DATE)
                        ORDER BY CAST(GirisTarihi as DATE)";

                    SqlCommand cmd = new SqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@SirketID", sirketID);

                    List<(DateTime gun, int count)> dataPoints = new List<(DateTime, int)>();

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DateTime gun = dr.GetDateTime(0);
                            int c = dr.GetInt32(1);
                            dataPoints.Add((gun, c));
                        }
                    }

                    if (dataPoints.Count == 0)
                    {
                        TextBlock noDataText = new TextBlock
                        {
                            Text = "Son 7 günde bu şirkete ait giriş kaydı yok.",
                            Foreground = Brushes.White,
                            FontSize = 14,
                            Margin = new Thickness(10)
                        };
                        lineChartCanvas.Children.Add(noDataText);
                    }
                    else
                    {
                        // En büyük count
                        int maxCount = 0;
                        foreach (var dp in dataPoints)
                            if (dp.count > maxCount) maxCount = dp.count;

                        // Basit koordinat hesaplaması
                        double chartWidth = lineChartCanvas.Width - 40;
                        double chartHeight = lineChartCanvas.Height - 40;

                        Polyline polyline = new Polyline
                        {
                            Stroke = Brushes.LightGreen,
                            StrokeThickness = 2
                        };

                        // Veriyi zamana göre sıraladık, x: 0..6 (7 gün), y: 0..maxCount
                        for (int i = 0; i < dataPoints.Count; i++)
                        {
                            double x = 20 + (i / (double)(dataPoints.Count - 1)) * chartWidth;
                            double y = 20 + (1.0 - dataPoints[i].count / (double)maxCount) * chartHeight;

                            polyline.Points.Add(new System.Windows.Point(x, y));

                            // Label
                            TextBlock label = new TextBlock
                            {
                                Text = dataPoints[i].count.ToString(),
                                Foreground = Brushes.White,
                                FontSize = 12
                            };
                            Canvas.SetLeft(label, x + 5);
                            Canvas.SetTop(label, y - 20);
                            lineChartCanvas.Children.Add(label);

                            // Tarih bilgisini de eklemek isterseniz
                            TextBlock dayLabel = new TextBlock
                            {
                                Text = dataPoints[i].gun.ToString("MM/dd"),
                                Foreground = Brushes.White,
                                FontSize = 10
                            };
                            Canvas.SetLeft(dayLabel, x);
                            Canvas.SetTop(dayLabel, lineChartCanvas.Height - 20); 
                            lineChartCanvas.Children.Add(dayLabel);
                        }

                        lineChartCanvas.Children.Add(polyline);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Günlük Çizgi Grafik Hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartPanel.Children.Add(lineChartCanvas);
        }


        /// <summary>
        /// Basit bir pasta grafiği Grid'i (tek dilim) oluşturan yardımcı metod.
        /// </summary>
        private Grid CreatePieChartGrid(string sirketAdi, double dolulukOrani, decimal saatlikUcret)
        {
            Grid grafikGrid = new Grid
            {
                Width = 200,
                Height = 250,
                Margin = new Thickness(10)
            };

            // Pasta dilimleri
            var doluDilim = CreatePieSlice(100, 100, 80, 0, dolulukOrani * 360, Brushes.DeepPink);
            var bosDilim = CreatePieSlice(100, 100, 80, dolulukOrani * 360, 360 - dolulukOrani * 360, Brushes.White);

            grafikGrid.Children.Add(bosDilim);
            grafikGrid.Children.Add(doluDilim);

            // TextBlock
            TextBlock sirketText = new TextBlock
            {
                Text = $"{sirketAdi}\nDoluluk: %{dolulukOrani * 100:F2}\nSaatlik Ücret: {saatlikUcret} TL",
                FontSize = 14,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 180, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            grafikGrid.Children.Add(sirketText);

            return grafikGrid;
        }


        private System.Windows.Shapes.Path CreatePieSlice(double centerX, double centerY, double radius,
                                                         double startAngle, double sweepAngle, Brush fillBrush)
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

        // ========== Yetkili Plakalar ==========
        private void BtnYetkiliPlakalar_Click(object sender, RoutedEventArgs e)
        {
            // Tüm içerikleri gizle
            dataGridAracListesi.Visibility = Visibility.Collapsed;
            SirketlerContentArea.Visibility = Visibility.Collapsed;
            dataGridOdemeler.Visibility = Visibility.Collapsed;

            dataGridYetkiliPlakalar.Visibility = Visibility.Visible;

            // Eğer giriş yapılmışsa "LoadYetkiliPlakalar(loggedInSirketID.Value)"
            // yapılmamışsa "LoadYetkiliPlakalar(null)" (tümünü göster) diyebiliriz:
            if (loggedInSirketID.HasValue)
                LoadYetkiliPlakalar(loggedInSirketID.Value);
            else
                LoadYetkiliPlakalar(null);
        }

        private void LoadYetkiliPlakalar(int? sirketID)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query;
                    if (sirketID.HasValue)
                    {
                        // Şirket bazlı
                        query = @"
                            SELECT YetkiliID, Plaka, SirketID
                            FROM YetkiliPlakalar
                            WHERE SirketID = @SirketID";
                    }
                    else
                    {
                        // Tümü
                        query = @"
                            SELECT YetkiliID, Plaka, SirketID
                            FROM YetkiliPlakalar";
                    }

                    SqlCommand cmd = new SqlCommand(query, connection);
                    if (sirketID.HasValue)
                    {
                        cmd.Parameters.AddWithValue("@SirketID", sirketID.Value);
                    }

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
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


        // ========== Ödemeler ==========
        private void BtnOdemeler_Click(object sender, RoutedEventArgs e)
        {
            dataGridAracListesi.Visibility = Visibility.Collapsed;
            SirketlerContentArea.Visibility = Visibility.Collapsed;
            dataGridYetkiliPlakalar.Visibility = Visibility.Collapsed;

            dataGridOdemeler.Visibility = Visibility.Visible;
            LoadOdemeler();
        }

        private void LoadOdemeler()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT OdemeID, AracID, OdemeTarihi, OdemeMiktari 
                                     FROM Odemeler
                                     ORDER BY OdemeTarihi DESC";

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
