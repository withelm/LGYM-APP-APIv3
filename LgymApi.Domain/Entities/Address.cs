namespace LgymApi.Domain.Entities;

public sealed class Address : EntityBase
{
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? District { get; set; }
    public string? FormattedAddress { get; set; }
    public string? IsoCountryCode { get; set; }
    public string? Name { get; set; }
    public string? PostalCode { get; set; }
    public string? Region { get; set; }
    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? Subregion { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
