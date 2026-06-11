using Cedeva.Core.Entities;
using Cedeva.Core.Enums;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// Factory helpers that build valid entity graphs (all required scalars + FKs) for tests.
/// Navigation properties are wired so EF Core infers foreign keys on save.
/// </summary>
public static class TestData
{
    public static Address Address() => new()
    {
        Street = "Rue de Test",
        City = "Bruxelles",
        PostalCode = "1000",
        Country = Country.Belgium
    };

    public static Organisation Organisation(string name = "Test Org") => new()
    {
        Name = name,
        Description = "Organisation de test",
        Address = Address(),
        BankAccountNumber = "BE68539007547034",
        BankAccountName = name
    };

    public static Parent Parent(Organisation organisation) => new()
    {
        FirstName = "Paul",
        LastName = "Parent",
        Email = "paul.parent@test.be",
        MobilePhoneNumber = "0470000000",
        NationalRegisterNumber = "85010112345",
        Address = Address(),
        Organisation = organisation
    };

    public static Child Child(Parent parent) => new()
    {
        FirstName = "Chloé",
        LastName = "Enfant",
        BirthDate = new DateTime(2016, 5, 20),
        NationalRegisterNumber = "16052012345",
        Parent = parent
    };

    public static Activity Activity(Organisation organisation, string name = "Stage Test") => new()
    {
        Name = name,
        Description = "Activité de test",
        IsActive = true,
        PricePerDay = 20m,
        StartDate = new DateTime(2026, 7, 1),
        EndDate = new DateTime(2026, 7, 5),
        Organisation = organisation
    };

    public static ActivityGroup Group(Activity activity, string label = "Groupe A") => new()
    {
        Label = label,
        Activity = activity
    };

    public static Booking Booking(Child child, Activity activity, ActivityGroup? group,
        decimal totalAmount, decimal paidAmount) => new()
    {
        Child = child,
        Activity = activity,
        Group = group,
        BookingDate = new DateTime(2026, 6, 1),
        IsConfirmed = true,
        TotalAmount = totalAmount,
        PaidAmount = paidAmount,
        PaymentStatus = PaymentStatus.NotPaid
    };

    public static Excursion Excursion(Activity activity, decimal cost, bool isActive = true) => new()
    {
        Name = "Excursion Test",
        ExcursionDate = new DateTime(2026, 7, 3),
        Cost = cost,
        Type = ExcursionType.Other,
        IsActive = isActive,
        Activity = activity
    };

    public static ExcursionGroup ExcursionGroup(Excursion excursion, ActivityGroup group) => new()
    {
        Excursion = excursion,
        ActivityGroup = group
    };

    public static ActivityQuestion Question(Activity activity, string text,
        bool isRequired = false, bool isActive = true, int displayOrder = 1) => new()
    {
        Activity = activity,
        QuestionText = text,
        QuestionType = QuestionType.Text,
        IsRequired = isRequired,
        IsActive = isActive,
        DisplayOrder = displayOrder
    };
}
