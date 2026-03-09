using Microsoft.Data.SqlClient;
using QuizAPI.Helpers;
using QuizAPI.Models;

namespace QuizAPI.Services;

public class DatabaseServices
{
    private readonly IConfiguration _configuration;

    public DatabaseServices(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        return new SqlConnection(connectionString);
    }

    public bool RegisterUser(string username, string password)
    {
        string hash = PasswordHasher.Hash(password);

        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(
            "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)", connection);

        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", hash);

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
        string hash = PasswordHasher.Hash(password);

        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM Users WHERE Username=@u AND PasswordHash=@p", connection);

        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", hash);

        int count = (int)cmd.ExecuteScalar()!;
        return count > 0;
    }
    
    public DbUser? GetUserByLogin(string username, string password)
    {
        using var connection = GetConnection();
        connection.Open();

        string hashedPassword = PasswordHasher.Hash(password);

        var cmd = new SqlCommand(@"
        SELECT Id, Username
        FROM Users
        WHERE Username = @Username AND PasswordHash = @PasswordHash", connection);

        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return new DbUser
        {
            Id = Convert.ToInt32(reader["Id"]),
            Username = reader["Username"]?.ToString() ?? ""
        };
    }
}