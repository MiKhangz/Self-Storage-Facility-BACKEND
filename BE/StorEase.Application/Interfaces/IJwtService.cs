namespace StorEase.Application.Interfaces;

public interface IJwtService
{
    string CreateToken(int userId, string fullName, string email, string role);
}
