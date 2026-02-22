using System.Security.Cryptography;
using System.Text;

namespace QuizAPI.Helpers;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        using var sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        byte[] hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}