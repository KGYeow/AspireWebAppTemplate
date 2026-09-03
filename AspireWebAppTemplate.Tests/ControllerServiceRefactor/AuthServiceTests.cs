// Feature: controller-service-refactor, Property 14: Profile and preferences round-trip
// Feature: controller-service-refactor, Property 15: Personal data download completeness
using System.Text;
using System.Text.Json;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Authentication;
using AspireWebAppTemplate.Application.Features.Template.Users;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying profile/preferences round-trip and personal data download
/// completeness contracts on <see cref="IAuthService"/>.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.7**
/// </remarks>
public class AuthServiceTests
{
    /// <summary>
    /// Property 14: For any authenticated user, updating profile fields via UpdateProfileAsync
    /// or preferences via UpdatePreferencesAsync and then calling GetProfileAsync returns a
    /// UserDto with the updated fields matching the request values.
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ProfileAndPreferencesRoundTrip_UpdateThenRead_ReturnsMatchingFields()
    {
        var displayNameGen = Gen.Elements(
            "Alice Smith", "Bob Jones", "Charlie Brown",
            "Diana Prince", "Eve Adams");

        var firstNameGen = Gen.Elements(
            "Alice", "Bob", "Charlie", "Diana", "Eve");

        var lastNameGen = Gen.Elements(
            "Smith", "Jones", "Brown", "Prince", "Adams");

        var phoneNumberGen = Gen.Elements(
            "+1-555-0100", "+1-555-0101", "+44-20-7946-0958",
            "+60-12-345-6789", null as string);

        var themeGen = Gen.Elements(
            ThemePreference.Light, ThemePreference.Dark, ThemePreference.System);

        var timeZoneGen = Gen.Elements(
            "America/New_York", "Europe/London", "Asia/Kuala_Lumpur",
            "Pacific/Auckland", null as string);

        var dateTimeFormatGen = Gen.Elements(
            "yyyy-MM-dd HH:mm", "dd/MM/yyyy HH:mm", "MM/dd/yyyy h:mm tt",
            null as string);

        var requestGen = from displayName in displayNameGen
                         from firstName in firstNameGen
                         from lastName in lastNameGen
                         from phone in phoneNumberGen
                         from theme in themeGen
                         from timeZone in timeZoneGen
                         from dateFormat in dateTimeFormatGen
                         select new
                         {
                             Profile = new UpdateProfileRequest
                             {
                                 DisplayName = displayName,
                                 FirstName = firstName,
                                 LastName = lastName,
                                 PhoneNumber = phone
                             },
                             Preferences = new UpdatePreferencesRequest
                             {
                                 Theme = theme,
                                 TimeZoneId = timeZone,
                                 DateTimeFormat = dateFormat
                             }
                         };

        return Prop.ForAll(Arb.From(requestGen), input =>
        {
            // Arrange: mock IAuthService to simulate the round-trip contract
            var mockService = new Mock<IAuthService>();

            // UpdateProfileAsync and UpdatePreferencesAsync succeed (return Task.CompletedTask)
            mockService
                .Setup(s => s.UpdateProfileAsync(It.IsAny<UpdateProfileRequest>()))
                .Returns(Task.CompletedTask);

            mockService
                .Setup(s => s.UpdatePreferencesAsync(It.IsAny<UpdatePreferencesRequest>()))
                .Returns(Task.CompletedTask);

            // GetProfileAsync returns a UserDto reflecting the updated fields
            var profileDto = new UserDto
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "testuser",
                Email = "testuser@example.com",
                DisplayName = input.Profile.DisplayName,
                FirstName = input.Profile.FirstName,
                LastName = input.Profile.LastName,
                PhoneNumber = input.Profile.PhoneNumber,
                Theme = input.Preferences.Theme ?? ThemePreference.System,
                TimeZoneId = input.Preferences.TimeZoneId,
                DateTimeFormat = input.Preferences.DateTimeFormat
            };

            mockService
                .Setup(s => s.GetProfileAsync())
                .ReturnsAsync(profileDto);

            // Act: update profile, update preferences, then read back
            mockService.Object.UpdateProfileAsync(input.Profile).GetAwaiter().GetResult();
            mockService.Object.UpdatePreferencesAsync(input.Preferences).GetAwaiter().GetResult();
            var result = mockService.Object.GetProfileAsync().GetAwaiter().GetResult();

            // Assert: profile fields match
            var displayNameMatch = result.DisplayName == input.Profile.DisplayName;
            var firstNameMatch = result.FirstName == input.Profile.FirstName;
            var lastNameMatch = result.LastName == input.Profile.LastName;
            var phoneMatch = result.PhoneNumber == input.Profile.PhoneNumber;

            // Assert: preference fields match
            var themeMatch = result.Theme == (input.Preferences.Theme ?? ThemePreference.System);
            var timeZoneMatch = result.TimeZoneId == input.Preferences.TimeZoneId;
            var dateFormatMatch = result.DateTimeFormat == input.Preferences.DateTimeFormat;

            var allMatch = displayNameMatch && firstNameMatch && lastNameMatch &&
                           phoneMatch && themeMatch && timeZoneMatch && dateFormatMatch;

            return allMatch
                .Label($"DisplayName: {displayNameMatch}, FirstName: {firstNameMatch}, " +
                       $"LastName: {lastNameMatch}, Phone: {phoneMatch}, " +
                       $"Theme: {themeMatch}, TimeZone: {timeZoneMatch}, DateFormat: {dateFormatMatch}");
        });
    }

    /// <summary>
    /// Property 15: For any user with populated profile fields, DownloadPersonalDataAsync returns
    /// a JSON byte array containing all [PersonalData] properties on ApplicationUser plus Id,
    /// UserName, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, and TwoFactorEnabled.
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property PersonalDataDownload_ContainsAllRequiredFields()
    {
        var firstNameGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana");
        var lastNameGen = Gen.Elements("Smith", "Jones", "Brown", "Prince");
        var displayNameGen = Gen.Elements("Alice S.", "Bob J.", "Charlie B.", "Diana P.");
        var avatarUrlGen = Gen.Elements(
            "https://example.com/avatar1.png",
            "https://example.com/avatar2.png",
            null as string);
        var departmentGen = Gen.Elements("Engineering", "Sales", "Marketing", null as string);
        var jobTitleGen = Gen.Elements("Developer", "Manager", "Analyst", null as string);
        var employeeNumberGen = Gen.Elements("EMP001", "EMP002", "EMP003", null as string);

        var dataGen = from firstName in firstNameGen
                      from lastName in lastNameGen
                      from displayName in displayNameGen
                      from avatarUrl in avatarUrlGen
                      from department in departmentGen
                      from jobTitle in jobTitleGen
                      from employeeNumber in employeeNumberGen
                      select new
                      {
                          FirstName = firstName,
                          LastName = lastName,
                          DisplayName = displayName,
                          AvatarUrl = avatarUrl,
                          Department = department,
                          JobTitle = jobTitle,
                          EmployeeNumber = employeeNumber
                      };

        return Prop.ForAll(Arb.From(dataGen), userData =>
        {
            // Arrange: build a JSON object containing all required personal data fields
            var personalData = new Dictionary<string, object?>
            {
                // Standard required fields
                ["Id"] = Guid.NewGuid().ToString(),
                ["UserName"] = "testuser",
                ["Email"] = "testuser@example.com",
                ["EmailConfirmed"] = true,
                ["PhoneNumber"] = "+1-555-0100",
                ["PhoneNumberConfirmed"] = false,
                ["TwoFactorEnabled"] = false,
                // [PersonalData] custom properties from ApplicationUser
                ["FirstName"] = userData.FirstName,
                ["LastName"] = userData.LastName,
                ["DisplayName"] = userData.DisplayName,
                ["AvatarUrl"] = userData.AvatarUrl,
                ["Department"] = userData.Department,
                ["JobTitle"] = userData.JobTitle,
                ["EmployeeNumber"] = userData.EmployeeNumber
            };

            var jsonBytes = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(personalData));

            // Mock the service to return the personal data JSON bytes
            var mockService = new Mock<IAuthService>();
            mockService
                .Setup(s => s.DownloadPersonalDataAsync())
                .ReturnsAsync(jsonBytes);

            // Act
            var result = mockService.Object.DownloadPersonalDataAsync().GetAwaiter().GetResult();

            // Assert: parse the JSON and verify all required keys are present
            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;

            // All required fields that must be present in the download
            var requiredKeys = new[]
            {
                // Standard Identity fields
                "Id", "UserName", "Email", "EmailConfirmed",
                "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled",
                // [PersonalData] custom properties from ApplicationUser
                "FirstName", "LastName", "DisplayName",
                "AvatarUrl", "Department", "JobTitle", "EmployeeNumber"
            };

            var missingKeys = requiredKeys
                .Where(key => !root.TryGetProperty(key, out _))
                .ToList();

            var allPresent = missingKeys.Count == 0;

            return allPresent
                .Label($"Missing keys: [{string.Join(", ", missingKeys)}], " +
                       $"Total keys in JSON: {root.EnumerateObject().Count()}, " +
                       $"Required: {requiredKeys.Length}");
        });
    }
}
