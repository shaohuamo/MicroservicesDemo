using AutoMapper;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.Mappers
{
    public class ProductToProductResponseMappingProfile : Profile
    {
        public ProductToProductResponseMappingProfile()
        {
            CreateMap<Product, ProductResponse>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.QuantityInStock, opt => opt.MapFrom(src => src.QuantityInStock))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId));
        }
    }
}
