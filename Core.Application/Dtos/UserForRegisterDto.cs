namespace NetCoreBackend.NArchitecture.Core.Application.Dtos;

public class UserForRegisterDto : IDto
{
    public required string Email { get; set; }

    public required string Password { get; set; }

    public UserForRegisterDto()
    {
        Email = string.Empty;
        Password = string.Empty;
    }

    public UserForRegisterDto(string email, string password)
    {
        Email = email;
        Password = password;
    }
}
