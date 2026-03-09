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

    public int? RegisterUser(string username, string password)
    {
        string hash = PasswordHasher.Hash(password);

        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        INSERT INTO Users (Username, PasswordHash)
        OUTPUT INSERTED.Id
        VALUES (@u, @p)", connection);

        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", hash);

        try
        {
            var insertedId = cmd.ExecuteScalar();
            return insertedId != null ? Convert.ToInt32(insertedId) : null;
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return null;
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
    
    public void CreateDefaultRanking(int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        INSERT INTO UserRankings
        (UserId, SingleElo, MultiElo, SingleRankedPlayed, SingleRankedWins, MultiRankedPlayed, MultiRankedWins)
        VALUES
        (@UserId, 1000, 1000, 0, 0, 0, 0)", connection);

        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.ExecuteNonQuery();
    }

    public int GetSingleRating(int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(
            "SELECT SingleElo FROM UserRankings WHERE UserId = @UserId", connection);

        cmd.Parameters.AddWithValue("@UserId", userId);

        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 1000;
    }

    public void UpdateSingleRating(int userId, int newRating, bool win)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        UPDATE UserRankings
        SET SingleElo = @Rating,
            SingleRankedPlayed = SingleRankedPlayed + 1,
            SingleRankedWins = SingleRankedWins + @WinAdd
        WHERE UserId = @UserId", connection);

        cmd.Parameters.AddWithValue("@Rating", newRating);
        cmd.Parameters.AddWithValue("@WinAdd", win ? 1 : 0);
        cmd.Parameters.AddWithValue("@UserId", userId);

        cmd.ExecuteNonQuery();
    }
}