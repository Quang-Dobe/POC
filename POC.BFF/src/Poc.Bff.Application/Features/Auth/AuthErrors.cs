using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Auth;

public static class AuthErrors
{
    public static readonly Error StateMismatch = new(
        Code: "Auth.StateMismatch",
        Message: "The OIDC state parameter did not match the expected value in the correlation cookie.",
        Type: ErrorType.Validation);

    public static readonly Error MissingCorrelation = new(
        Code: "Auth.MissingCorrelation",
        Message: "The OIDC correlation cookie was missing or could not be read.",
        Type: ErrorType.Validation);

    public static readonly Error CodeExchangeFailed = new(
        Code: "Auth.CodeExchangeFailed",
        Message: "The OIDC code exchange with the identity provider failed.",
        Type: ErrorType.Unexpected);

    public static readonly Error AccessDenied = new(
        Code: "Auth.AccessDenied",
        Message: "The identity provider returned no roles for the authenticated subject.",
        Type: ErrorType.Validation);
}
