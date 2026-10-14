namespace StorEase.Application.Features.Auth.Dtos;

public sealed record RegisterRequest(string FullName, string Email, string PhoneNumber, string Password);

/// <summary>The account starts <c>Pending</c> and can sign in once the emailed link is opened.</summary>
public sealed record RegisterResponse(int UserId, string Email, string Status);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);
