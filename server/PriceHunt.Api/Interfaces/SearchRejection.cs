namespace PriceHunt.Api.Interfaces;

public sealed record SearchRejection(int StatusCode, string Error);
