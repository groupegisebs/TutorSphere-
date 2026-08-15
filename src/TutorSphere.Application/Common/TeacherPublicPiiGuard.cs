using System.Text.Json;

namespace TutorSphere.Application.Common;

/// <summary>
/// Aucune réponse publique ne doit transporter d’adresse, de naissance ou de coordonnées personnelles.
/// </summary>
public static class TeacherPublicPiiGuard
{
    public static readonly string[] ForbiddenPropertyNames =
    [
        "BirthDate", "birthDate", "dateOfBirth", "DateOfBirth",
        "FullAddress", "fullAddress",
        "AddressLine1", "addressLine1", "AddressLine2", "addressLine2",
        "PostalCode", "postalCode", "zipCode", "ZipCode",
        "Address", "address",
        "PersonalEmail", "personalEmail", "OwnerEmail", "ownerEmail", "Email", "email",
        "PersonalPhone", "personalPhone", "Phone", "phone", "PhoneNumber", "phoneNumber",
        "IdentityDocument", "identityDocument",
        "BankInformation", "bankInformation", "Iban", "iban",
        "EmergencyContact", "emergencyContact",
        "InternalUserId", "internalUserId", "OwnerUserId", "ownerUserId",
        "TutorLastName", "tutorLastName", "LastName", "lastName",
        "TutorFullName", "tutorFullName"
    ];

    public static IReadOnlyList<string> FindForbiddenProperties(string json)
    {
        var hits = new List<string>();
        using var doc = JsonDocument.Parse(json);
        Walk(doc.RootElement, hits);
        return hits;
    }

    private static void Walk(JsonElement el, List<string> hits)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    if (ForbiddenPropertyNames.Contains(p.Name, StringComparer.Ordinal))
                        hits.Add(p.Name);
                    Walk(p.Value, hits);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    Walk(item, hits);
                break;
        }
    }
}
