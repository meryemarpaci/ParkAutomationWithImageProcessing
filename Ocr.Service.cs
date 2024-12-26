using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace VeriTabaniProjesi
{
    public class OcrService
    {
        public void CallPythonScript()
        {
            try
            {
                string pythonScript = "plate_detection.py"; // Python script dosya adı

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("Python script dosyası bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = "python", // Python çalıştırıcısı
                    Arguments = pythonScript,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(start))
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string result = reader.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(result))
                        {
                            MessageBox.Show($"Okunan Plaka: {result}", "Plaka Tanıma", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Plaka algılanamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }

                    using (StreamReader errorReader = process.StandardError)
                    {
                        string errors = errorReader.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(errors))
                        {
                            MessageBox.Show($"Script Hatası: {errors}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
