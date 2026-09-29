namespace ProductsMicroservice.Core.DTO;

public sealed record ProductAddResult(ProductResponse Product, bool IsReplay);
