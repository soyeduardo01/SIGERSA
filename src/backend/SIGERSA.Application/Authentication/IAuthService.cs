namespace SIGERSA.Application.Authentication;

public interface IAuthService
{
    Task<AuthTokensResponse> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);

    Task<AuthTokensResponse> RefreshAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutCommand command, CancellationToken cancellationToken = default);

    Task RequestPasswordRecoveryAsync(RequestPasswordRecoveryCommand command, CancellationToken cancellationToken = default);

    Task<PasswordRecoveryTokenResponse> VerifyPasswordRecoveryAsync(VerifyPasswordRecoveryCommand command, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default);
}
