using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Mappers;

namespace ProductsMicroservice.Tests;

public class MappingProfilesTests
{
    private readonly IMapper _mapper;

    public MappingProfilesTests()
    {
        var configuration = new MapperConfiguration(config =>
        {
            config.AddProfile<ProductAddRequestToProductMappingProfile>();
            config.AddProfile<ProductUpdateRequestToProductMappingProfile>();
            config.AddProfile<ProductToProductResponseMappingProfile>();
        }, NullLoggerFactory.Instance);

        configuration.AssertConfigurationIsValid();
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void Map_ShouldMapProductAddRequest_ToProduct()
    {
        var request = new ProductAddRequest
        {
            DisplayName = "Keyboard",
            UnitPrice = 89.99m,
            QuantityInStock = 12
        };

        var result = _mapper.Map<Product>(request);

        result.Should().BeEquivalentTo(new
        {
            request.DisplayName,
            request.UnitPrice,
            request.QuantityInStock
        });
        result.ProductId.Should().BeEmpty();
        result.Version.Should().Be(0);
    }

    [Fact]
    public void Map_ShouldMapProductUpdateRequest_ToProduct()
    {
        var request = new ProductUpdateRequest
        {
            ProductId = Guid.NewGuid(),
            DisplayName = "Mouse",
            UnitPrice = 49.99m,
            QuantityInStock = 20,
            Version = 4
        };

        var result = _mapper.Map<Product>(request);

        result.Should().BeEquivalentTo(new
        {
            request.ProductId,
            request.DisplayName,
            request.UnitPrice,
            request.QuantityInStock
        });
        result.Version.Should().Be(request.Version);
    }

    [Fact]
    public void Map_ShouldMapProduct_ToProductResponse()
    {
        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            DisplayName = "Monitor",
            UnitPrice = 299.99m,
            QuantityInStock = 5,
            Version = 3
        };

        var result = _mapper.Map<ProductResponse>(product);

        result.Should().BeEquivalentTo(new ProductResponse(
            product.ProductId,
            product.DisplayName,
            product.UnitPrice,
            product.QuantityInStock,
            product.Version));
    }
}
