using Microsoft.Data.SqlClient;

namespace WebQuizGame.Classes.Database;

public class DatabaseServices
{
    public bool RegisterUser(string username, string password)
    {
        string passwordHash = PasswordEncryption.Encrypt(password);

        using var connection = DatabaseConnection.GetConnection();
        using var cmd = new SqlCommand("INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)", connection);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", passwordHash);
        
        try
        {
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return false;
        }
    }

    public bool LoginUser(string username, string password)
    {
        string passwordHash = PasswordEncryption.Encrypt(password);
        
        using var connection = DatabaseConnection.GetConnection();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username=@u AND PasswordHash=@p", connection);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", passwordHash);
        
        int count = (int)cmd.ExecuteScalar()!;
        return count > 0;
    }
}