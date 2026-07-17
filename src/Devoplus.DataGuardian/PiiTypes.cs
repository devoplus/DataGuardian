namespace Devoplus.DataGuardian;

/// <summary>
/// Canonical PII entity type identifiers shared by recognizers, scoring weights and redaction rules.
/// </summary>
/// <remarks>
/// Using these constants everywhere prevents key-mismatch defects between a recognizer's output type
/// and the keys used in <see cref="DataGuardianOptions.Weights"/> and
/// <see cref="DataGuardianOptions.RedactTypes"/>. A mismatch would silently drop a type's weight to the
/// default and skip its redaction. The <c>TypeConsistencyTests</c> assert this alignment.
/// </remarks>
public static class PiiTypes
{
    /// <summary>Turkish national identity number (T.C. Kimlik No).</summary>
    public const string Tckn = "TCKN";

    /// <summary>Payment card number (Luhn + known scheme prefixes).</summary>
    public const string CreditCard = "CREDIT_CARD";

    /// <summary>International Bank Account Number (any registered country).</summary>
    public const string Iban = "IBAN";

    /// <summary>Date that may represent a date of birth.</summary>
    public const string Dob = "DOB";

    /// <summary>Postal address (keyword based).</summary>
    public const string Address = "ADDRESS";

    /// <summary>Telephone number.</summary>
    public const string Phone = "PHONE";

    /// <summary>E-mail address.</summary>
    public const string Email = "EMAIL";

    /// <summary>Person name (produced by the optional NER model).</summary>
    public const string Person = "PERSON";
}
