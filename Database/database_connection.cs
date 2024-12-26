using System;
using System.Data.SqlClient;

namespace OtoparkSistemi
{
    public class DatabaseConnection
    {
        private readonly string connectionString = "Server=LENOVO-MERYEM;Database=OtoparkSistemi;Trusted_Connection=True;";

        public SqlConnection GetConnection()
        {
            SqlConnection connection = new SqlConnection(connectionString);
            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Veritabanı bağlantı hatası: " + ex.Message);
            }
            return connection;
        }

        public void CloseConnection(SqlConnection connection)
        {
            if (connection != null && connection.State == System.Data.ConnectionState.Open)
            {
                connection.Close();
            }
        }
    }
}
