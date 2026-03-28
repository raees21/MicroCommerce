namespace MicroCommerce.Identity.Api.Models;

public sealed record RegisterRequest(string Email, string Password, string FullName);

public sealed record LoginRequest(string Email, string Password);
