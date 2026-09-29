using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using NotificationsMicroservice.Core.Abstractions;
using NotificationsMicroservice.Core.Domain;
using NotificationsMicroservice.Infrastructure.Options;
using Resend;

namespace NotificationsMicroservice.Infrastructure.Delivery;

public sealed class ResendNotificationEmailSender(
    IResend resend,
    IOptions<ResendEmailOptions> options) : INotificationEmailSender
{
    private readonly ResendEmailOptions _options = options.Value;

    public async Task<string> SendAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException("ResendEmail:From must be configured.");
        }

        if (string.IsNullOrWhiteSpace(notification.UserEmail))
        {
            throw new InvalidOperationException("The notification does not have a recipient email address.");
        }

        var isChinese = notification.Culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        var message = BuildMessage(notification, isChinese);
        var idempotencyKey = $"product-operation/{notification.NotificationId:D}";
        var receipt = await resend.EmailSendAsync(idempotencyKey, message, cancellationToken);
        if (!receipt.Success)
        {
            if (receipt.Exception is not null)
            {
                throw receipt.Exception;
            }

            throw new InvalidOperationException("Resend did not accept the notification email.");
        }

        return receipt.Content.ToString();
    }

    private EmailMessage BuildMessage(Notification notification, bool isChinese)
    {
        var operation = LocalizeOperation(notification.Operation, isChinese);
        var succeeded = string.Equals(notification.Status, "Success", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(notification.Status, "Succeeded", StringComparison.OrdinalIgnoreCase);
        var result = isChinese
            ? succeeded ? "成功" : "失败"
            : succeeded ? "succeeded" : "failed";
        var productName = string.IsNullOrWhiteSpace(notification.ProductName)
            ? notification.ProductId?.ToString("D") ?? (isChinese ? "未知产品" : "unknown product")
            : notification.ProductName;

        var encodedProductName = HtmlEncoder.Default.Encode(productName);
        var encodedErrorCode = HtmlEncoder.Default.Encode(notification.ErrorCode ?? "PRODUCT_OPERATION_FAILED");

        var textBody = isChinese
            ? $"产品 {productName} 的{operation}操作{result}。\n时间：{notification.OccurredAtUtc:O}"
            : $"The {operation} operation for product {productName} {result}.\nTime: {notification.OccurredAtUtc:O}";
        var htmlBody = isChinese
            ? $"<p>产品 <strong>{encodedProductName}</strong> 的{operation}操作{result}。</p><p>时间：{notification.OccurredAtUtc:O}</p>"
            : $"<p>The {operation} operation for product <strong>{encodedProductName}</strong> {result}.</p><p>Time: {notification.OccurredAtUtc:O}</p>";

        if (!succeeded)
        {
            textBody += isChinese ? $"\n错误代码：{notification.ErrorCode ?? "PRODUCT_OPERATION_FAILED"}" : $"\nError code: {notification.ErrorCode ?? "PRODUCT_OPERATION_FAILED"}";
            htmlBody += isChinese ? $"<p>错误代码：{encodedErrorCode}</p>" : $"<p>Error code: {encodedErrorCode}</p>";
        }

        var message = new EmailMessage
        {
            From = _options.From,
            Subject = isChinese ? _options.ChineseSubject : _options.EnglishSubject,
            TextBody = textBody,
            HtmlBody = htmlBody
        };
        message.To.Add(notification.UserEmail);
        return message;
    }

    private static string LocalizeOperation(string operation, bool isChinese)
    {
        if (!isChinese)
        {
            return operation.ToLowerInvariant();
        }

        return operation.ToLowerInvariant() switch
        {
            "add" => "新增",
            "update" => "更新",
            "delete" => "删除",
            _ => "处理"
        };
    }
}
