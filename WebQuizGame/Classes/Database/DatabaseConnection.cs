using Microsoft.Data.SqlClient;
using ConfigurationManager = System.Configuration.ConfigurationManager;

namespace WebQuizGame.Classes.Database
{
    public static class DatabaseConnection
    {
        public static SqlConnection GetConnection()
        {
            string? dataSource = ConfigurationManager.AppSettings["DataSource"];
            string? database = ConfigurationManager.AppSettings["Database"];
            string? login = ConfigurationManager.AppSettings["Login"];
            string? password = ConfigurationManager.AppSettings["Password"];
            
            ValidateConfig(dataSource, database, login, password);

            string connectionString =
                $"Server={dataSource};" +
                $"Database={database};" +
                $"User Id={login};" +
                $"Password={password};";
            
            var connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }
        
        private static void ValidateConfig(string? dataSource, string? database, string? login, string? password)
        {
            if (string.IsNullOrWhiteSpace(dataSource) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                throw new ApplicationException("Database configuration is missing or invalid");
            }
        }
    }
}