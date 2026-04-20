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
    
    public List<TriviaQuestion> GetCustomQuizAsTriviaQuestions(int quizId, int userId)
    {
        var quiz = GetCustomQuizById(quizId, userId);
        if (quiz == null)
            return new List<TriviaQuestion>();

        return quiz.Questions
            .OrderBy(q => q.QuestionOrder)
            .Select(q =>
            {
                var answers = new List<string> { q.Answer1, q.Answer2 };

                if (!string.IsNullOrWhiteSpace(q.Answer3))
                    answers.Add(q.Answer3);

                if (!string.IsNullOrWhiteSpace(q.Answer4))
                    answers.Add(q.Answer4);

                var incorrect = answers
                    .Where(a => !string.Equals(a, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return new TriviaQuestion
                {
                    Question = q.QuestionText,
                    CorrectAnswer = q.CorrectAnswer,
                    IncorrectAnswers = incorrect,
                    Type = answers.Count == 2 ? "boolean" : "multiple",
                    Difficulty = "custom",
                    Category = quiz.Title
                };
            })
            .ToList();
    }
    
    public CustomQuizDto? GetCustomQuizByIdAnyOwner(int quizId)
    {
        using var connection = GetConnection();
        connection.Open();

        CustomQuizDto? quiz = null;

        using (var cmd = new SqlCommand(@"
            SELECT Id, UserId, Title, TimePerQuestion, CreatedAt
            FROM CustomQuizzes
            WHERE Id = @QuizId", connection))
        {
            cmd.Parameters.AddWithValue("@QuizId", quizId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                quiz = new CustomQuizDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    UserId = Convert.ToInt32(reader["UserId"]),
                    Title = reader["Title"]?.ToString() ?? "",
                    TimePerQuestion = Convert.ToInt32(reader["TimePerQuestion"]),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                };
            }
        }

        if (quiz == null)
            return null;

        quiz.Questions = GetCustomQuizQuestions(quiz.Id, connection);
        return quiz;
    }

    public List<TriviaQuestion> GetCustomQuizAsTriviaQuestionsAnyOwner(int quizId)
    {
        var quiz = GetCustomQuizByIdAnyOwner(quizId);
        if (quiz == null)
            return new List<TriviaQuestion>();

        return quiz.Questions
            .OrderBy(q => q.QuestionOrder)
            .Select(q =>
            {
                var answers = new List<string> { q.Answer1, q.Answer2 };

                if (!string.IsNullOrWhiteSpace(q.Answer3))
                    answers.Add(q.Answer3!);

                if (!string.IsNullOrWhiteSpace(q.Answer4))
                    answers.Add(q.Answer4!);

                var incorrect = answers
                    .Where(a => !string.Equals(a, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return new TriviaQuestion
                {
                    Question = q.QuestionText,
                    CorrectAnswer = q.CorrectAnswer,
                    IncorrectAnswers = incorrect,
                    Type = answers.Count == 2 ? "boolean" : "multiple",
                    Difficulty = "custom",
                    Category = quiz.Title
                };
            })
            .ToList();
    }
    
    public int CreateCustomQuiz(int userId, CreateCustomQuizRequest req)
    {
        using var connection = GetConnection();
        connection.Open();

        using var tx = connection.BeginTransaction();

        try
        {
            int quizId;

            using (var cmd = new SqlCommand(@"
                INSERT INTO CustomQuizzes (UserId, Title, TimePerQuestion)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @Title, @TimePerQuestion)", connection, tx))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Title", req.Title);
                cmd.Parameters.AddWithValue("@TimePerQuestion", req.TimePerQuestion);

                quizId = Convert.ToInt32(cmd.ExecuteScalar());
            }

            for (int i = 0; i < req.Questions.Count; i++)
            {
                var q = req.Questions[i];

                using var qCmd = new SqlCommand(@"
                    INSERT INTO CustomQuizQuestions
                    (QuizId, QuestionText, Answer1, Answer2, Answer3, Answer4, CorrectAnswer, QuestionOrder)
                    VALUES
                    (@QuizId, @QuestionText, @Answer1, @Answer2, @Answer3, @Answer4, @CorrectAnswer, @QuestionOrder)", connection, tx);

                qCmd.Parameters.AddWithValue("@QuizId", quizId);
                qCmd.Parameters.AddWithValue("@QuestionText", q.QuestionText);
                qCmd.Parameters.AddWithValue("@Answer1", q.Answer1);
                qCmd.Parameters.AddWithValue("@Answer2", q.Answer2);
                qCmd.Parameters.AddWithValue("@Answer3", (object?)q.Answer3 ?? DBNull.Value);
                qCmd.Parameters.AddWithValue("@Answer4", (object?)q.Answer4 ?? DBNull.Value);
                qCmd.Parameters.AddWithValue("@CorrectAnswer", q.CorrectAnswer);
                qCmd.Parameters.AddWithValue("@QuestionOrder", i + 1);

                qCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return quizId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public List<CustomQuizDto> GetCustomQuizzesByUser(int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        var quizzes = new List<CustomQuizDto>();

        using (var cmd = new SqlCommand(@"
            SELECT Id, UserId, Title, TimePerQuestion, CreatedAt
            FROM CustomQuizzes
            WHERE UserId = @UserId
            ORDER BY Id DESC", connection))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                quizzes.Add(new CustomQuizDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    UserId = Convert.ToInt32(reader["UserId"]),
                    Title = reader["Title"]?.ToString() ?? "",
                    TimePerQuestion = Convert.ToInt32(reader["TimePerQuestion"]),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                });
            }
        }

        foreach (var quiz in quizzes)
        {
            quiz.Questions = GetCustomQuizQuestions(quiz.Id, connection);
        }

        return quizzes;
    }

    public CustomQuizDto? GetCustomQuizById(int quizId, int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        CustomQuizDto? quiz = null;

        using (var cmd = new SqlCommand(@"
            SELECT Id, UserId, Title, TimePerQuestion, CreatedAt
            FROM CustomQuizzes
            WHERE Id = @QuizId AND UserId = @UserId", connection))
        {
            cmd.Parameters.AddWithValue("@QuizId", quizId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                quiz = new CustomQuizDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    UserId = Convert.ToInt32(reader["UserId"]),
                    Title = reader["Title"]?.ToString() ?? "",
                    TimePerQuestion = Convert.ToInt32(reader["TimePerQuestion"]),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                };
            }
        }

        if (quiz == null)
            return null;

        quiz.Questions = GetCustomQuizQuestions(quiz.Id, connection);
        return quiz;
    }

    public bool DeleteCustomQuiz(int quizId, int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
            DELETE FROM CustomQuizzes
            WHERE Id = @QuizId AND UserId = @UserId", connection);

        cmd.Parameters.AddWithValue("@QuizId", quizId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private List<CustomQuizQuestionDto> GetCustomQuizQuestions(int quizId, SqlConnection connection)
    {
        var questions = new List<CustomQuizQuestionDto>();

        using var cmd = new SqlCommand(@"
            SELECT Id, QuizId, QuestionText, Answer1, Answer2, Answer3, Answer4, CorrectAnswer, QuestionOrder
            FROM CustomQuizQuestions
            WHERE QuizId = @QuizId
            ORDER BY QuestionOrder ASC", connection);

        cmd.Parameters.AddWithValue("@QuizId", quizId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            questions.Add(new CustomQuizQuestionDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                QuestionText = reader["QuestionText"]?.ToString() ?? "",
                Answer1 = reader["Answer1"]?.ToString() ?? "",
                Answer2 = reader["Answer2"]?.ToString() ?? "",
                Answer3 = reader["Answer3"] == DBNull.Value ? null : reader["Answer3"]?.ToString(),
                Answer4 = reader["Answer4"] == DBNull.Value ? null : reader["Answer4"]?.ToString(),
                CorrectAnswer = reader["CorrectAnswer"]?.ToString() ?? "",
                QuestionOrder = Convert.ToInt32(reader["QuestionOrder"])
            });
        }

        return questions;
    }

    public int? RegisterUser(string username, string password)
    {
        string hash = PasswordHasher.Hash(password);

        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
    INSERT INTO Users (Username, PasswordHash, AvatarKey)
    OUTPUT INSERTED.Id
    VALUES (@u, @p, @a)", connection);

        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", hash);
        cmd.Parameters.AddWithValue("@a", "default");

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
    
    public DbUser? GetUserByLogin(string username, string password)
    {
        using var connection = GetConnection();
        connection.Open();

        string hashedPassword = PasswordHasher.Hash(password);

        var cmd = new SqlCommand(@"
        SELECT Id, Username, AvatarKey
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
            Username = reader["Username"]?.ToString() ?? "",
            AvatarKey = reader["AvatarKey"]?.ToString() ?? "default"
        };
    }
    
    public DbUser? GetUserById(int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
    SELECT Id, Username, AvatarKey
    FROM Users
    WHERE Id = @UserId", connection);

        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return new DbUser
        {
            Id = Convert.ToInt32(reader["Id"]),
            Username = reader["Username"]?.ToString() ?? "",
            AvatarKey = reader["AvatarKey"]?.ToString() ?? "default"
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
    
    public RankedProfileResponse? GetRankedProfile(int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        SELECT 
            u.Id,
            u.Username,
            u.AvatarKey,
            r.SingleElo,
            r.MultiElo,
            r.SingleRankedPlayed,
            r.SingleRankedWins,
            r.MultiRankedPlayed,
            r.MultiRankedWins
        FROM Users u
        INNER JOIN UserRankings r ON u.Id = r.UserId
        WHERE u.Id = @UserId", connection);

        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return new RankedProfileResponse
        {
            UserId = Convert.ToInt32(reader["Id"]),
            Username = reader["Username"]?.ToString() ?? "",
            AvatarKey = reader["AvatarKey"]?.ToString() ?? "default",
            SingleElo = Convert.ToInt32(reader["SingleElo"]),
            MultiElo = Convert.ToInt32(reader["MultiElo"]),
            SingleRankedPlayed = Convert.ToInt32(reader["SingleRankedPlayed"]),
            SingleRankedWins = Convert.ToInt32(reader["SingleRankedWins"]),
            MultiRankedPlayed = Convert.ToInt32(reader["MultiRankedPlayed"]),
            MultiRankedWins = Convert.ToInt32(reader["MultiRankedWins"])
        };
    }
    
    public void EnsureRankingExists(int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var checkCmd = new SqlCommand(
            "SELECT COUNT(*) FROM UserRankings WHERE UserId = @UserId", connection);

        checkCmd.Parameters.AddWithValue("@UserId", userId);

        int count = (int)checkCmd.ExecuteScalar()!;

        if (count > 0)
            return;

        using var insertCmd = new SqlCommand(@"
        INSERT INTO UserRankings
        (UserId, SingleElo, MultiElo, SingleRankedPlayed, SingleRankedWins, MultiRankedPlayed, MultiRankedWins)
        VALUES
        (@UserId, 1000, 1000, 0, 0, 0, 0)", connection);

        insertCmd.Parameters.AddWithValue("@UserId", userId);
        insertCmd.ExecuteNonQuery();
    }
    
    public List<LeaderboardEntryResponse> GetSingleLeaderboard(int top = 20)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        SELECT TOP (@Top)
            u.Id,
            u.Username,
            r.SingleElo,
            r.SingleRankedPlayed,
            r.SingleRankedWins
        FROM Users u
        INNER JOIN UserRankings r ON u.Id = r.UserId
        ORDER BY r.SingleElo DESC, r.SingleRankedWins DESC, r.SingleRankedPlayed ASC", connection);

        cmd.Parameters.AddWithValue("@Top", top);

        using var reader = cmd.ExecuteReader();

        var result = new List<LeaderboardEntryResponse>();
        int rank = 1;

        while (reader.Read())
        {
            result.Add(new LeaderboardEntryResponse
            {
                Rank = rank++,
                UserId = Convert.ToInt32(reader["Id"]),
                Username = reader["Username"]?.ToString() ?? "",
                SingleElo = Convert.ToInt32(reader["SingleElo"]),
                SingleRankedPlayed = Convert.ToInt32(reader["SingleRankedPlayed"]),
                SingleRankedWins = Convert.ToInt32(reader["SingleRankedWins"])
            });
        }

        return result;
    }
    
    public int GetMultiRating(int userId)
    {
        EnsureRankingExists(userId);

        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(
            "SELECT MultiElo FROM UserRankings WHERE UserId = @UserId", connection);

        cmd.Parameters.AddWithValue("@UserId", userId);

        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 1000;
    }

    public void UpdateMultiRating(int userId, int newRating, bool win)
    {
        EnsureRankingExists(userId);

        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
            UPDATE UserRankings
            SET MultiElo = @Rating,
            MultiRankedPlayed = MultiRankedPlayed + 1,
            MultiRankedWins = MultiRankedWins + @WinAdd
            WHERE UserId = @UserId", connection);

        cmd.Parameters.AddWithValue("@Rating", newRating);
        cmd.Parameters.AddWithValue("@WinAdd", win ? 1 : 0);
        cmd.Parameters.AddWithValue("@UserId", userId);

        cmd.ExecuteNonQuery();
    }

    public List<LeaderboardEntryResponse> GetMultiLeaderboard(int top = 20)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        SELECT TOP (@Top)
            u.Id,
            u.Username,
            r.MultiElo,
            r.MultiRankedPlayed,
            r.MultiRankedWins
        FROM Users u
        INNER JOIN UserRankings r ON u.Id = r.UserId
        ORDER BY r.MultiElo DESC, r.MultiRankedWins DESC, r.MultiRankedPlayed ASC", connection);

        cmd.Parameters.AddWithValue("@Top", top);

        using var reader = cmd.ExecuteReader();

        var result = new List<LeaderboardEntryResponse>();
        int rank = 1;

        while (reader.Read())
        {
            result.Add(new LeaderboardEntryResponse
            {
                Rank = rank++,
                UserId = Convert.ToInt32(reader["Id"]),
                Username = reader["Username"]?.ToString() ?? "",
                MultiElo = Convert.ToInt32(reader["MultiElo"]),
                MultiRankedPlayed = Convert.ToInt32(reader["MultiRankedPlayed"]),
                MultiRankedWins = Convert.ToInt32(reader["MultiRankedWins"])
            });
        }

        return result;
    }

    public int CreateRankedMatch(string mode)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        INSERT INTO RankedMatches (Mode)
        OUTPUT INSERTED.Id
        VALUES (@Mode)", connection);

        cmd.Parameters.AddWithValue("@Mode", mode);

        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    public void CreateRankedMatchResult(
        int matchId,
        int userId,
        int score,
        int placement,
        int eloBefore,
        int eloAfter)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
    INSERT INTO RankedMatchResults
    (MatchId, UserId, Score, Placement, EloBefore, EloAfter)
    VALUES
    (@MatchId, @UserId, @Score, @Placement, @EloBefore, @EloAfter)", connection);

        cmd.Parameters.AddWithValue("@MatchId", matchId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Score", score);
        cmd.Parameters.AddWithValue("@Placement", placement);
        cmd.Parameters.AddWithValue("@EloBefore", eloBefore);
        cmd.Parameters.AddWithValue("@EloAfter", eloAfter);

        cmd.ExecuteNonQuery();
    }
    
    public bool UsernameExists(string username, int? excludeUserId = null)
    {
        using var connection = GetConnection();
        connection.Open();

        string sql = @"
        SELECT COUNT(*)
        FROM Users
        WHERE LOWER(Username) = LOWER(@Username)";

        if (excludeUserId.HasValue)
            sql += " AND Id <> @ExcludeUserId";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Username", username);

        if (excludeUserId.HasValue)
            cmd.Parameters.AddWithValue("@ExcludeUserId", excludeUserId.Value);

        int count = (int)cmd.ExecuteScalar()!;
        return count > 0;
    }

    public AccountSettingsResponse? GetAccountSettings(int userId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        SELECT Id, Username, AvatarKey
        FROM Users
        WHERE Id = @UserId", connection);

        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return new AccountSettingsResponse
        {
            UserId = Convert.ToInt32(reader["Id"]),
            Username = reader["Username"]?.ToString() ?? "",
            AvatarKey = reader["AvatarKey"]?.ToString() ?? "default"
        };
    }

    public bool UpdateUsername(int userId, string newUsername)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        UPDATE Users
        SET Username = @Username
        WHERE Id = @UserId", connection);

        cmd.Parameters.AddWithValue("@Username", newUsername);
        cmd.Parameters.AddWithValue("@UserId", userId);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateAvatar(int userId, string avatarKey)
    {
        using var connection = GetConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
        UPDATE Users
        SET AvatarKey = @AvatarKey
        WHERE Id = @UserId", connection);

        cmd.Parameters.AddWithValue("@AvatarKey", avatarKey);
        cmd.Parameters.AddWithValue("@UserId", userId);

        return cmd.ExecuteNonQuery() > 0;
    }
}