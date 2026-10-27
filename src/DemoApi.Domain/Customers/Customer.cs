namespace DemoApi.Domain.Customers;

/// <summary>Customer profile. The email lives only on the Identity user (A-D5).</summary>
public sealed class Customer : AuditableEntity
{
    public const int NameMaxLength = 100;
    public const int PhoneMaxLength = 20;
    public const string ErasedFirstName = "Erased";
    public const string ErasedLastName = "Customer";

    private Customer()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
    }

    private Customer(Guid id, Guid userId, string firstName, string lastName, string? phone)
        : base(id)
    {
        UserId = userId;
        FirstName = firstName;
        LastName = lastName;
        Phone = phone;
    }

    public Guid UserId { get; private set; }

    [PersonalData]
    public string FirstName { get; private set; }

    [PersonalData]
    public string LastName { get; private set; }

    [PersonalData]
    public string? Phone { get; private set; }

    public uint Version { get; private set; }

    public static Customer Create(Guid userId, string firstName, string lastName, string? phone) =>
        new(Guid.CreateVersion7(), userId, firstName.Trim(), lastName.Trim(), NormalizePhone(phone));

    public void UpdateProfile(string firstName, string lastName, string? phone)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Phone = NormalizePhone(phone);
    }

    /// <summary>Erase personal data (FR-064). The caller soft deletes the row in the same transaction.</summary>
    public void Anonymize()
    {
        FirstName = ErasedFirstName;
        LastName = ErasedLastName;
        Phone = null;
    }

    private static string? NormalizePhone(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}

public static class CustomerErrors
{
    public static readonly Error HasOpenOrders = Error.Conflict(
        "customer-has-open-orders", "Customer has open orders", "The customer has Pending, Confirmed or Shipped orders.");

    public static readonly Error EmailChangeNotAllowed = Error.Conflict(
        "email-change-not-allowed", "Email change not allowed", "The email of a customer cannot be changed.");

    public static readonly Error DuplicateEmail = Error.Conflict(
        "duplicate-email", "Duplicate email", "The email address is already in use.");
}