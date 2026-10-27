namespace DemoApi.BuildingBlocks.Security;

public static class Roles
{
    public const string Admin = "Admin";

    /// <summary>Template strip point (solution-structure §13).</summary>
    public const string Customer = "Customer";
}

public static class Policies
{
    public const string Authenticated = "Authenticated";
    public const string AdminOnly = "AdminOnly";

    /// <summary>Role Customer and a valid customer_id claim. Template strip point.</summary>
    public const string CustomerOnly = "CustomerOnly";
}

public static class AppClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string CustomerId = "customer_id";
    public const string SecurityStamp = "sst";
}