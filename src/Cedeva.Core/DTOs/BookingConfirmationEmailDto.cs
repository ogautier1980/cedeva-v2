namespace Cedeva.Core.DTOs;

/// <summary>
/// Data Transfer Object for booking confirmation email with payment information
/// </summary>
public record BookingConfirmationEmailDto(
    string ParentEmail,
    string ParentName,
    string ChildName,
    string ActivityName,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalAmount,
    string StructuredCommunication,
    string BankAccount);
