using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Validation;

/// <summary>
/// Localizes ASP.NET Identity error messages (password policy, duplicate user/email, etc.) through
/// SharedResources so they surface in the user's language (FR/NL/EN) instead of the built-in English
/// text. Registered via <c>.AddErrorDescriber&lt;LocalizedIdentityErrorDescriber&gt;()</c> on the
/// Identity builder; the localizer is resolved from DI. Each error keeps its original Identity
/// <see cref="IdentityError.Code"/>; only the Description is localized (key <c>Identity.&lt;Code&gt;</c>).
/// </summary>
public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResources> localizer) =>
        _localizer = localizer;

    private IdentityError Err(string code, string key, params object[] args) =>
        new() { Code = code, Description = _localizer[key, args].Value };

    public override IdentityError DefaultError() =>
        Err(nameof(DefaultError), "Identity.DefaultError");

    public override IdentityError DuplicateUserName(string userName) =>
        Err(nameof(DuplicateUserName), "Identity.DuplicateUserName", userName);

    public override IdentityError DuplicateEmail(string email) =>
        Err(nameof(DuplicateEmail), "Identity.DuplicateEmail", email);

    public override IdentityError InvalidUserName(string? userName) =>
        Err(nameof(InvalidUserName), "Identity.InvalidUserName", userName ?? string.Empty);

    public override IdentityError InvalidEmail(string? email) =>
        Err(nameof(InvalidEmail), "Identity.InvalidEmail", email ?? string.Empty);

    public override IdentityError PasswordTooShort(int length) =>
        Err(nameof(PasswordTooShort), "Identity.PasswordTooShort", length);

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Err(nameof(PasswordRequiresNonAlphanumeric), "Identity.PasswordRequiresNonAlphanumeric");

    public override IdentityError PasswordRequiresDigit() =>
        Err(nameof(PasswordRequiresDigit), "Identity.PasswordRequiresDigit");

    public override IdentityError PasswordRequiresLower() =>
        Err(nameof(PasswordRequiresLower), "Identity.PasswordRequiresLower");

    public override IdentityError PasswordRequiresUpper() =>
        Err(nameof(PasswordRequiresUpper), "Identity.PasswordRequiresUpper");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Err(nameof(PasswordRequiresUniqueChars), "Identity.PasswordRequiresUniqueChars", uniqueChars);

    public override IdentityError PasswordMismatch() =>
        Err(nameof(PasswordMismatch), "Identity.PasswordMismatch");

    public override IdentityError UserAlreadyHasPassword() =>
        Err(nameof(UserAlreadyHasPassword), "Identity.UserAlreadyHasPassword");

    public override IdentityError DuplicateRoleName(string role) =>
        Err(nameof(DuplicateRoleName), "Identity.DuplicateRoleName", role);
}
