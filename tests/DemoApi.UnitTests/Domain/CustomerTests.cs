namespace DemoApi.UnitTests.Domain;

public sealed class CustomerTests
{
    [Fact]
    public void Create_trims_names_and_normalizes_blank_phone()
    {
        var customer = Customer.Create(Guid.NewGuid(), " Ada ", " Lovelace ", "   ");

        customer.FirstName.ShouldBe("Ada");
        customer.LastName.ShouldBe("Lovelace");
        customer.Phone.ShouldBeNull();
    }

    [Fact]
    public void UpdateProfile_replaces_names_and_clears_phone_with_null()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Ada", "Lovelace", "+12025550101");

        customer.UpdateProfile("Grace", "Hopper", null);

        customer.FirstName.ShouldBe("Grace");
        customer.LastName.ShouldBe("Hopper");
        customer.Phone.ShouldBeNull();
    }

    [Fact]
    public void Anonymize_removes_personal_data()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Ada", "Lovelace", "+12025550101");

        customer.Anonymize();

        customer.FirstName.ShouldBe(Customer.ErasedFirstName);
        customer.LastName.ShouldBe(Customer.ErasedLastName);
        customer.Phone.ShouldBeNull();
    }

    [Theory]
    [InlineData(nameof(Customer.FirstName))]
    [InlineData(nameof(Customer.LastName))]
    [InlineData(nameof(Customer.Phone))]
    public void Personal_fields_are_marked_as_personal_data(string property) =>
        typeof(Customer).GetProperty(property)!.IsDefined(typeof(PersonalDataAttribute), true).ShouldBeTrue();
}