using AutoMapper;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.Mappers
{
    public class ProductAddRequestToProductMappingProfile : Profile
    {
        public ProductAddRequestToProductMappingProfile()
        {
            CreateMap<ProductAddRequest, Product>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.QuantityInStock, opt => opt.MapFrom(src => src.QuantityInStock))
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Version, opt => opt.Ignore())
                ;
        }
    }
}
