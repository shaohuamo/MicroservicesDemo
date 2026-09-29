using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroService.API.Security;

/// <summary>
/// Reads only the gateway-authenticated headers. Product request bodies can never
/// select the notification recipient.
/// </summary>
public sealed class HttpProductOperationContextAccessor : IProductOperationContextAccessor
{
    private const int MaxUserIdLength = 256;
    private const int MaxEmailLength = 320;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Creates an accessor for the current gateway-authenticated request.</summary>
    public HttpProductOperationContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Returns trusted recipient and correlation metadata for an operation event.</summary>
    public ProductOperationContext GetCurrent()
    {
        HttpContext context = _httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("An authenticated request context is required.");

        string userId = context.Request.Headers["X-User-Id"].ToString().Trim();
        string email = context.Request.Headers["X-User-Email"].ToString().Trim();
        string emailVerifiedValue = context.Request.Headers["X-User-Email-Verified"].ToString();

        if (string.IsNullOrWhiteSpace(userId) || userId.Length > MaxUserIdLength ||
            string.IsNullOrWhiteSpace(email) || email.Length > MaxEmailLength ||
            !MailAddress.TryCreate(email, out _) ||
            !bool.TryParse(emailVerifiedValue, out bool emailVerified) || !emailVerified)
        {
            throw new UnauthorizedAccessException(
                "A gateway-authenticated user with a verified email is required.");
        }

        string correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        return new ProductOperationContext(
            userId,
            email,
            GetSafeCulture(context.Request.Headers.AcceptLanguage.ToString()),
            correlationId[..Math.Min(correlationId.Length, 128)]);
    }

    private static string GetSafeCulture(string acceptLanguage)
    {
        string candidate = acceptLanguage
            .Split(',', 2, StringSplitOptions.TrimEntries)[0]
            .Split(';', 2, StringSplitOptions.TrimEntries)[0];

        if (candidate.Length is < 2 or > 16)
        {
            return "en-US";
        }

        try
        {
            return CultureInfo.GetCultureInfo(candidate).Name;
        }
        catch (CultureNotFoundException)
        {
            return "en-US";
        }
    }
}
