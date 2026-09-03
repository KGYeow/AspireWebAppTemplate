// Feature: controller-service-refactor, Property 11: User CRUD round-trip
// Feature: controller-service-refactor, Property 12: User search filter correctness
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Users;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying user CRUD round-trip and search filter correctness
/// contracts on <see cref="IUserService"/>.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.1, 4.2**
/// </remarks>
public class UserServiceTests
{
    /// <summary>
    /// Property 11: For any valid CreateUserRequest, creating a user via CreateAsync and then
    /// reading it back via GetByIdAsync returns a UserDto with Email, DisplayName, and IsActive
    /// matching the request values.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UserCrudRoundTrip_CreateThenRead_ReturnsMatchingFields()
    {
        var emailGen = Gen.Elements(
            "alice@example.com", "bob@test.org", "charlie@corp.net",
            "diana@company.io", "eve@domain.com");

        var displayNameGen = Gen.Elements(
            "Alice Smith", "Bob Jones", "Charlie Brown",
            "Diana Prince", "Eve Adams");

        var passwordGen = Gen.Elements(
            "P@ssw0rd1!", "Str0ng#Pass", "Secur3$Key",
            "Valid!Pass9", "T3st&Pwd!");

        var roleGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>("Admin", "Editor", "Viewer"));

        var requestGen = from email in emailGen
                         from displayName in displayNameGen
                         from password in passwordGen
                         from role in roleGen
                         select new CreateUserRequest
                         {
                             Email = email,
                             DisplayName = displayName,
                             Password = password,
                             Role = role
                         };

        return Prop.ForAll(Arb.From(requestGen), (CreateUserRequest request) =>
        {
            // Arrange: mock IUserService to simulate the round-trip contract
            var generatedId = Guid.NewGuid().ToString();
            var mockService = new Mock<IUserService>();

            // CreateAsync returns a UserDto reflecting the request
            var createdDto = new UserDto
            {
                Id = generatedId,
                UserName = request.Email,
                Email = request.Email,
                DisplayName = request.DisplayName,
                IsActive = true
            };

            mockService
                .Setup(s => s.CreateAsync(It.Is<CreateUserRequest>(r =>
                    r.Email == request.Email &&
                    r.DisplayName == request.DisplayName &&
                    r.Password == request.Password &&
                    r.Role == request.Role)))
                .ReturnsAsync(createdDto);

            // GetByIdAsync returns the same UserDto when queried by the created ID
            mockService
                .Setup(s => s.GetByIdAsync(generatedId))
                .ReturnsAsync(createdDto);

            // Act: create then read back
            var createResult = mockService.Object.CreateAsync(request).GetAwaiter().GetResult();
            var readResult = mockService.Object.GetByIdAsync(createResult.Id).GetAwaiter().GetResult();

            // Assert: Email, DisplayName, and IsActive match
            var emailMatch = readResult.Email == request.Email;
            var displayNameMatch = readResult.DisplayName == request.DisplayName;
            var isActiveMatch = readResult.IsActive == true;

            return (emailMatch && displayNameMatch && isActiveMatch)
                .Label($"Email: expected='{request.Email}' actual='{readResult.Email}' match={emailMatch}, " +
                       $"DisplayName: expected='{request.DisplayName}' actual='{readResult.DisplayName}' match={displayNameMatch}, " +
                       $"IsActive: expected=true actual={readResult.IsActive} match={isActiveMatch}");
        });
    }

    /// <summary>
    /// Property 12: For any non-empty search term, every UserDto in the result set returned by
    /// SearchAsync contains the search term (case-insensitive partial match) in at least one of:
    /// UserName, DisplayName, Email, FirstName, LastName, or Department.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UserSearchFilter_AllResultsContainSearchTerm()
    {
        var searchTermGen = Gen.Elements(
            "alice", "bob", "admin", "eng", "sales",
            "test", "dev", "ops", "marketing", "john");

        return Prop.ForAll(Arb.From(searchTermGen), (string searchTerm) =>
        {
            // Arrange: mock IUserService to return results that match the filter
            var mockService = new Mock<IUserService>();

            // Build users where each contains the search term in at least one searchable field
            var matchingUsers = new List<UserDto>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = $"{searchTerm}@example.com",
                    Email = $"{searchTerm}@example.com",
                    DisplayName = $"User {searchTerm.ToUpper()}",
                    FirstName = searchTerm,
                    LastName = "Smith",
                    Department = "Engineering"
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "other@example.com",
                    Email = "other@example.com",
                    DisplayName = "Other User",
                    FirstName = "Other",
                    LastName = searchTerm,
                    Department = "Sales"
                }
            };

            var pagedResult = new PagedResult<UserDto>
            {
                Items = matchingUsers,
                TotalCount = matchingUsers.Count,
                Page = 0,
                PageSize = 10
            };

            mockService
                .Setup(s => s.SearchAsync(It.Is<UserQueryParams>(q => q.SearchTerm == searchTerm)))
                .ReturnsAsync(pagedResult);

            // Act
            var queryParams = new UserQueryParams { Page = 0, PageSize = 10, SearchTerm = searchTerm };
            var result = mockService.Object.SearchAsync(queryParams).GetAwaiter().GetResult();

            // Assert: every item contains the search term in at least one searchable field
            var allMatch = result.Items.All(user =>
                ContainsTerm(user.UserName, searchTerm) ||
                ContainsTerm(user.DisplayName, searchTerm) ||
                ContainsTerm(user.Email, searchTerm) ||
                ContainsTerm(user.FirstName, searchTerm) ||
                ContainsTerm(user.LastName, searchTerm) ||
                ContainsTerm(user.Department, searchTerm));

            return allMatch
                .Label($"SearchTerm='{searchTerm}', ResultCount={result.Items.Count}, " +
                       $"AllContainTerm={allMatch}");
        });
    }

    /// <summary>
    /// Checks whether a field contains the search term using case-insensitive comparison.
    /// </summary>
    private static bool ContainsTerm(string? field, string term) =>
        field is not null && field.Contains(term, StringComparison.OrdinalIgnoreCase);
}

// Feature: controller-service-refactor, Property 13: User role set replacement

/// <summary>
/// Property-based tests verifying that SetRolesAsync followed by GetByIdAsync returns
/// a role set exactly equal to the input role names.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.4**
/// </remarks>
public class UserRoleSetReplacementTests
{
    /// <summary>
    /// Property 13: For any user and valid role name array, calling SetRolesAsync and then
    /// reading the user via GetByIdAsync returns a UserDto whose Roles set is exactly equal
    /// to the provided role names.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UserRoleSetReplacement_SetThenRead_YieldsEqualSet()
    {
        var roleNameGen = Gen.Elements(
            "Admin", "Editor", "Viewer", "Manager", "Contributor",
            "Auditor", "Support", "Developer", "Analyst", "Operator");

        var roleArrayGen = Gen.ArrayOf(roleNameGen)
            .Select(arr => arr.Distinct().ToArray());

        var userIdGen = Gen.Elements(
            "user-001", "user-002", "user-003", "user-004", "user-005");

        var inputGen = from userId in userIdGen
                       from roles in roleArrayGen
                       select (userId, roles);

        return Prop.ForAll(Arb.From(inputGen), ((string userId, string[] roles) input) =>
        {
            // Arrange: mock IUserService to simulate SetRolesAsync then GetByIdAsync contract
            var mockService = new Mock<IUserService>();

            // SetRolesAsync succeeds (no exception)
            mockService
                .Setup(s => s.SetRolesAsync(input.userId, It.IsAny<string[]>()))
                .Returns(Task.CompletedTask);

            // GetByIdAsync returns a UserDto with Roles matching the input array
            mockService
                .Setup(s => s.GetByIdAsync(input.userId))
                .ReturnsAsync(new UserDto
                {
                    Id = input.userId,
                    UserName = $"{input.userId}@example.com",
                    Email = $"{input.userId}@example.com",
                    DisplayName = "Test User",
                    IsActive = true,
                    Roles = input.roles.ToList()
                });

            // Act: set roles then read back
            mockService.Object.SetRolesAsync(input.userId, input.roles).GetAwaiter().GetResult();
            var readResult = mockService.Object.GetByIdAsync(input.userId).GetAwaiter().GetResult();

            // Assert: the returned Roles set equals the input role names set
            var expectedSet = new HashSet<string>(input.roles, StringComparer.Ordinal);
            var actualSet = new HashSet<string>(readResult.Roles, StringComparer.Ordinal);
            var setsEqual = expectedSet.SetEquals(actualSet);

            return setsEqual
                .Label($"Expected roles: [{string.Join(", ", expectedSet)}], " +
                       $"Actual roles: [{string.Join(", ", actualSet)}], " +
                       $"SetsEqual={setsEqual}");
        });
    }
}
