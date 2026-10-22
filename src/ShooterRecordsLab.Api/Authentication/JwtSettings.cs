namespace ShooterRecordsLab.Api.Authentication;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKeyBase64 { get; set; } = "";
    public bool ValidationOnly { get; set; }

    public bool IsEmpty => string.IsNullOrEmpty(Issuer) && string.IsNullOrEmpty(Audience) && string.IsNullOrEmpty(SigningKeyBase64);

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(SigningKeyBase64).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
