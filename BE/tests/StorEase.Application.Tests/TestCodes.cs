using StorEase.Infrastructure.Services;

namespace StorEase.Application.Tests;

public static class TestCodes
{
    public static readonly AesCodeProtector Protector = AesCodeProtector.FromSecret("test-secret-for-access-codes");
}
