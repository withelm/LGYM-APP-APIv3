namespace LgymApi.Infrastructure.Services;

public static class LegacyPasswordConstants
{
    public const int Iterations = 25000;
    public const int KeyLength = 512;
    public const string Digest = "sha256";
    public const int SaltLengthBytes = 32;
}
