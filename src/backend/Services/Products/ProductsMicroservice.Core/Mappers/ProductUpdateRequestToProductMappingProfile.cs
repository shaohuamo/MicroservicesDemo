using AutoMapper;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.Mappers
{
    public class ProductUpdateRequestToProductMappingProfile : Profile
    {
        public ProductUpdateRequestToProductMappingProfile()
        {
            CreateMap<ProductUpdateRequest, Product>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.QuantityInStock, opt => opt.MapFrom(src => src.QuantityInStock))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.Version, opt => opt.Ignore())
                ;
        }
    }
}
