namespace NotificationsMicroservice.Infrastructure.Options;

public sealed class ResendEmailOptions
{
    public const string SectionName = "ResendEmail";

    public string ApiToken { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string EnglishSubject { get; set; } = "Product operation result";
    public string ChineseSubject { get; set; } = "产品操作结果";
}
