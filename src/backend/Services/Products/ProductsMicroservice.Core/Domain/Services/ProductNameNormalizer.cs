using System.Buffers;
using System.Text;

namespace ProductsMicroservice.Core.Domain.Services;

/// <summary>
/// Produces the public display name and the canonical key used to identify a product name.
/// </summary>
public static class ProductNameNormalizer
{
    public const int MaximumLength = 50;

    private static readonly SearchValues<char> AllowedSymbols =
        SearchValues.Create(" .'’&()+/_-");

    public static ProductNames Normalize(string? displayName)
    {
        if (displayName is null)
        {
            throw new ArgumentNullException(nameof(displayName));
        }

        string normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length == 0)
        {
            throw new ArgumentException("Display name cannot be blank.", nameof(displayName));
        }

        if (normalizedDisplayName.Length > MaximumLength)
        {
            throw new ArgumentException($"Display name cannot exceed {MaximumLength} characters.", nameof(displayName));
        }

        ValidateCharacters(normalizedDisplayName);

        string normalizedForKey = normalizedDisplayName.Normalize(NormalizationForm.FormKC);
        var keyBuilder = new StringBuilder(normalizedForKey.Length);
        foreach (char character in normalizedForKey)
        {
            if (!char.IsWhiteSpace(character))
            {
                keyBuilder.Append(character);
            }
        }

        string productName = keyBuilder.ToString().ToUpperInvariant();
        if (productName.Length == 0)
        {
            throw new ArgumentException("Display name cannot contain only whitespace.", nameof(displayName));
        }

        if (productName.Length > MaximumLength)
        {
            throw new ArgumentException($"Normalized product name cannot exceed {MaximumLength} characters.", nameof(displayName));
        }

        return new ProductNames(normalizedDisplayName, productName);
    }

    private static void ValidateCharacters(string displayName)
    {
        if (!char.IsLetterOrDigit(displayName[0]) ||
            !char.IsLetterOrDigit(displayName[^1]))
        {
            throw new ArgumentException(
                "Product name must start and end with a letter or digit.",
                nameof(displayName));
        }

        bool hasLetterOrDigit = false;
        bool previousWasSpace = false;

        foreach (char character in displayName)
        {
            if (char.IsLetterOrDigit(character))
            {
                hasLetterOrDigit = true;
                previousWasSpace = false;
                continue;
            }

            if (character == ' ')
            {
                if (previousWasSpace)
                {
                    throw new ArgumentException(
                        "Product name cannot contain consecutive spaces.",
                        nameof(displayName));
                }

                previousWasSpace = true;
                continue;
            }

            if (char.IsControl(character) || !AllowedSymbols.Contains(character))
            {
                throw new ArgumentException(
                    "Product name contains unsupported characters.",
                    nameof(displayName));
            }
        }

        if (!hasLetterOrDigit)
        {
            throw new ArgumentException(
                "Product name must contain a letter or digit.",
                nameof(displayName));
        }
    }
}

public readonly record struct ProductNames(string DisplayName, string ProductName);
