using System.Text.Json.Serialization;

namespace NetCoreBackend.NArchitecture.Core.Application.Dtos;

public class UserForLoginDto : IDto
{
    public required string Email { get; set; }


    public required string Password { get; set; }
    /// <summary>
    /// 2FA kodu. İlk login denemesinde boş gönderin.
    /// Eğer response'da RequiredAuthenticatorType dönerse, ikinci denemede bu kodu gönderin.
    /// </summary>
    [JsonIgnore]
    public string? AuthenticatorCode { get; set; }

    public UserForLoginDto()
    {
        Email = string.Empty;
        Password = string.Empty;
    }

    public UserForLoginDto(string email, string password)
    {
        Email = email;
        Password = password;
    }
}
