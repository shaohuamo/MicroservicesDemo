using FluentAssertions;
using ProductsMicroservice.Core.Domain.Services;

namespace ProductsServiceUnitTests;

public sealed class ProductNameNormalizerTests
{
    [Theory]
    [InlineData("Apple iPhone")]
    [InlineData("APPLE IPHONE")]
    [InlineData("APPLEIPHONE")]
    public void Normalize_ShouldUseTheSameKeyForWhitespaceAndCaseVariants(string displayName)
    {
        ProductNameNormalizer.Normalize(displayName).ProductName.Should().Be("APPLEIPHONE");
    }

    [Fact]
    public void Normalize_ShouldPreserveDisplayNameAndKeepChineseVariantsDistinct()
    {
        ProductNameNormalizer.Normalize("  苹果 手机  ").Should()
            .Be(new ProductNames("苹果 手机", "苹果手机"));
        ProductNameNormalizer.Normalize("蘋果手機").ProductName.Should().Be("蘋果手機");
    }

    [Theory]
    [InlineData("@@@")]
    [InlineData("😀 Product")]
    [InlineData("Product<>Name")]
    [InlineData("Apple  iPhone")]
    public void Normalize_ShouldRejectNamesOutsideTheCharacterWhitelist(string displayName)
    {
        FluentActions.Invoking(() => ProductNameNormalizer.Normalize(displayName))
            .Should().Throw<ArgumentException>();
    }
}
