using System.Security.Cryptography;

namespace RentManager.Api.Security;

// PBKDF2-HMACSHA256. Everything used here is in the BCL, no extra package.
// Stored as: v1.{iterations}.{base64 salt}.{base64 subkey}
// The iteration count is stored with the hash so it can be raised later
// without invalidating existing passwords.
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int SubkeySizeBytes = 32;
    private const int DefaultIterations = 210_000;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            SubkeySizeBytes);

        return string.Join(
            '.',
            "v1",
            DefaultIterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(subkey));
    }

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('.');

        if (parts.Length != 4 || parts[0] != "v1")
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedSubkey;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
