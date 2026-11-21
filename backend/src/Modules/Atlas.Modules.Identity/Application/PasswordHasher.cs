using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Atlas.Modules.Identity.Application;

/// <summary>Hashes and verifies passwords.</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a password, generating a fresh random salt.</summary>
    string Hash(string password);

    /// <summary>Verifies a password against a stored hash, in constant time.</summary>
    bool Verify(string password, string storedHash);
}

/// <summary>
/// Argon2id password hashing.
/// </summary>
/// <remarks>
/// Argon2id is OWASP's first recommendation for password storage: it is memory
/// hard, so an attacker with GPUs gains far less than against PBKDF2 or
/// bcrypt. Parameters follow the OWASP minimum of 19 MiB memory, two
/// iterations and one degree of parallelism.
///
/// The output is a PHC-format string that carries its own parameters and
/// salt, so the cost can be raised later without invalidating existing
/// hashes — old hashes keep verifying with their recorded parameters.
/// </remarks>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemoryKib = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Algorithm = "argon2id";
    private const int Version = 19;

    /// <inheritdoc />
    public string Hash(string password)
    {
        // Empty is a programming error here: the request validator enforces a
        // minimum length long before this is reached.
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, MemoryKib, Iterations, Parallelism, HashBytes);

        return string.Create(CultureInfo.InvariantCulture,
            $"${Algorithm}$v={Version}$m={MemoryKib},t={Iterations},p={Parallelism}$" +
            $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    /// <inheritdoc />
    public bool Verify(string password, string storedHash)
    {
        // An empty password is never valid, and the underlying Argon2
        // implementation throws on one rather than returning a non-match.
        // Left unguarded, a blank password field turns a 401 into a 500.
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (!TryParse(storedHash, out var memory, out var iterations, out var parallelism,
                      out var salt, out var expected))
        {
            // A malformed hash must not authenticate anyone, and must not
            // throw either: that would turn a corrupt row into a 500 that
            // distinguishes it from a wrong password.
            return false;
        }

        var actual = Derive(password, salt, memory, iterations, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int parallelism, int length)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon2.GetBytes(length);
    }

    private static bool TryParse(string? stored, out int memory, out int iterations, out int parallelism,
                                 out byte[] salt, out byte[] hash)
    {
        memory = iterations = parallelism = 0;
        salt = hash = [];

        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        // $argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>
        var parts = stored.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        var costs = parts[2].Split(',');
        if (costs.Length != 3) { return false; }

        try
        {
            memory = int.Parse(costs[0].AsSpan(2), CultureInfo.InvariantCulture);
            iterations = int.Parse(costs[1].AsSpan(2), CultureInfo.InvariantCulture);
            parallelism = int.Parse(costs[2].AsSpan(2), CultureInfo.InvariantCulture);
            salt = Convert.FromBase64String(parts[3]);
            hash = Convert.FromBase64String(parts[4]);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            return false;
        }

        return memory > 0 && iterations > 0 && parallelism > 0 && salt.Length > 0 && hash.Length > 0;
    }
}
