# Requirements Document

## Introduction

Move navigation menu filtering from the Blazor Server frontend to the backend API. Currently, the Web project fetches raw page permissions and applies a two-pass filter (accessibility + orphan decoration removal) in NavMenu to compute visible items. The proposed change makes the API the single source of truth for navigation visibility: the API combines the nav structure (from DefaultNavigationProvider) with the user's authentication state and page permissions, returning a pre-filtered, ready-to-render navigation tree. The Web project simply renders what it receives.

## Glossary

- **Navigation_Service**: The API-side service responsible for combining the full navigation structure with user permissions and authentication state to produce a filtered navigation tree.
- **Navigation_Controller**: The thin API controller that exposes the filtered navigation tree endpoint.
- **Api_Navigation_Client**: The typed HttpClient service in the Web project that calls the navigation endpoint and returns the filtered tree.
- **NavItem**: The shared navigation model (in Core) representing a header, link, divider, or group.
- **System_Page**: A page path defined in SystemPageDefaults that always bypasses permission checks (login, register, error, etc.).
- **Page_Permission**: A database record granting a role access to a specific page path. Absence of a record means access denied (whitelist model).
- **Decoration_Item**: A NavItem of type Header or Divider that provides visual structure but does not represent navigable content.
- **Content_Item**: A NavItem of type Link or Group that represents navigable content.
- **Orphan_Decoration**: A Header with no following content before the next Header/end-of-list, or a Divider with no content on both sides.

## Requirements

### Requirement 1: Filtered Navigation Endpoint

**User Story:** As an authenticated user, I want the API to return my personalized navigation tree, so that the frontend renders only items I am permitted to see.

#### Acceptance Criteria

1. WHEN an authenticated user sends a GET request to the navigation endpoint, THE Navigation_Controller SHALL return an HTTP 200 response containing a JSON array of NavItem objects filtered to include only items the user has permission to access, preserving the hierarchical structure (groups with their children).
2. IF an unauthenticated request is received at the navigation endpoint, THEN THE Navigation_Controller SHALL return HTTP 401 Unauthorized with no response body.
3. THE Navigation_Controller SHALL expose the endpoint at route `GET /api/navigation`.
4. THE Navigation_Controller SHALL delegate all permission-based filtering logic to the Navigation_Service, performing no filtering or business logic itself.
5. IF a Group-type NavItem has zero permitted children after filtering, THEN THE Navigation_Service SHALL exclude that group entirely from the returned navigation tree.
6. IF the authenticated user has no permitted pages, THEN THE Navigation_Controller SHALL return an HTTP 200 response containing an empty JSON array.

### Requirement 2: Authentication-Based Filtering

**User Story:** As an authenticated user, I want items marked AuthorizedOnly to appear only when I am authenticated, so that anonymous-only items are hidden from me.

#### Acceptance Criteria

1. WHEN the user is authenticated, THE Navigation_Service SHALL exclude NavItem objects where NotAuthorizedOnly is true from the filtered tree.
2. WHEN the user is authenticated, THE Navigation_Service SHALL not exclude NavItem objects where AuthorizedOnly is true during the authentication filtering pass (these items remain candidates for subsequent permission-based filtering defined in Requirement 3).
3. WHEN the user is unauthenticated, THE Navigation_Service SHALL exclude NavItem objects where AuthorizedOnly is true from the filtered tree.
4. WHEN the user is unauthenticated, THE Navigation_Service SHALL include NavItem objects where NotAuthorizedOnly is true in the filtered tree.
5. WHEN a NavItem has both AuthorizedOnly and NotAuthorizedOnly set to false, THE Navigation_Service SHALL not exclude that item during the authentication filtering pass regardless of authentication state.
6. IF a NavItem has both AuthorizedOnly and NotAuthorizedOnly set to true, THEN THE Navigation_Service SHALL exclude that item from the filtered tree regardless of authentication state.

### Requirement 3: Permission-Based Filtering

**User Story:** As an authenticated user, I want navigation links filtered by my page permissions, so that I only see links to pages I can access.

#### Acceptance Criteria

1. WHEN a NavItem of type Link has an Href that maps to a path present in the user's page permissions, THE Navigation_Service SHALL include that item in the filtered tree.
2. WHEN a NavItem of type Link has an Href that maps to a path NOT present in the user's page permissions and the path is NOT a System_Page, THE Navigation_Service SHALL exclude that item from the filtered tree.
3. THE Navigation_Service SHALL always include NavItem objects of type Link whose Href maps to a System_Page path, regardless of page permissions.
4. WHEN a NavItem of type Link has a null Href, THE Navigation_Service SHALL include that item (non-navigable items are never blocked).
5. THE Navigation_Service SHALL apply permission-based filtering only to NavItem objects of type Link that have already passed authentication-based filtering (Requirement 2).

### Requirement 4: Group Visibility

**User Story:** As a user, I want groups to automatically hide when all their children are inaccessible, so that I do not see empty collapsible sections.

#### Acceptance Criteria

1. WHEN all content children (items of type Link or nested Group) of a Group are excluded by the authentication and permission filtering pipeline, THE Navigation_Service SHALL exclude that Group from the filtered tree.
2. WHEN at least one content child (Link or nested Group) of a Group passes the authentication and permission filtering pipeline, THE Navigation_Service SHALL include that Group containing only its children that passed filtering.
3. WHEN a Group contains nested Groups, THE Navigation_Service SHALL evaluate child Groups before their parent so that a nested Group already determined to be empty is not counted as a visible content child of the parent Group.
4. WHEN a Group is included in the filtered tree, THE Navigation_Service SHALL remove any decorative children (Header or Divider items) that have no adjacent content sibling within that Group.

### Requirement 5: Orphan Decoration Removal

**User Story:** As a user, I want headers and dividers to automatically hide when their associated content section is empty, so that I do not see meaningless visual separators.

#### Acceptance Criteria

1. WHEN a Header has no following Content_Item before the next Header or end of the sibling list at the same level, THE Navigation_Service SHALL exclude that Header from the filtered tree.
2. WHEN a Divider has no Content_Item preceding it within the same sibling list (i.e., it is at the start or all preceding siblings are Decoration_Items), THE Navigation_Service SHALL exclude that Divider from the filtered tree.
3. WHEN a Divider has no Content_Item following it within the same sibling list (i.e., it is at the end or all following siblings are Decoration_Items), THE Navigation_Service SHALL exclude that Divider from the filtered tree.
4. THE Navigation_Service SHALL apply orphan decoration removal at each level of the tree (top-level items and within each Group's Children list independently).
5. THE Navigation_Service SHALL apply decoration removal as a final pass after authentication-based filtering, permission-based filtering, and Group visibility resolution.

### Requirement 6: Web Project Integration

**User Story:** As a developer, I want the Web project to consume the pre-filtered tree from the API, so that NavMenu no longer performs any filtering logic.

#### Acceptance Criteria

1. THE Api_Navigation_Client SHALL call `GET /api/navigation` and return the deserialized `List<NavItem>` wrapped in the project's standard `ApiResult<List<NavItem>>` pattern (success with data or failure with error message).
2. WHEN the Api_Navigation_Client returns a successful result, THE NavMenu SHALL render the received tree directly without applying authentication filtering, permission filtering, or orphaned-decoration removal.
3. WHILE the Api_Navigation_Client call is in-flight, THE NavMenu SHALL display a loading skeleton placeholder (consistent with the existing skeleton pattern) until the response is received.
4. IF the Api_Navigation_Client returns a failure result (HTTP error response, network failure, or deserialization failure), THEN THE NavMenu SHALL display an empty navigation state with zero items rendered.
5. THE Api_Navigation_Client SHALL propagate user identity via the existing UserIdentityDelegatingHandler mechanism.

### Requirement 7: Filtering Pipeline Equivalence

**User Story:** As a developer, I want the API-side filtering to produce identical results to the current client-side filtering, so that moving the logic does not change user-visible behavior.

#### Acceptance Criteria

1. FOR ALL valid combinations of NavItem trees (up to 5 levels deep, up to 50 items per level), authentication states (authenticated or unauthenticated), and page permission sets (including empty sets), THE Navigation_Service filtering output SHALL be structurally equal to the output of NavMenu's ComputeVisibleNavItems method given the same inputs, where structural equality means identical item count at each tree level, identical property values on each corresponding item, and identical Children lists on Group items (compared recursively).
2. THE Navigation_Service SHALL preserve the original ordering of NavItem objects within each level of the tree, such that the index position of any item relative to its siblings in the output matches the relative order those items had in the input.
3. THE Navigation_Service SHALL preserve all NavItem properties (Type, Text, Href, Title, Match, Icon, AuthorizedOnly, NotAuthorizedOnly, DividerClass, Expanded) unchanged on items that pass filtering.
4. WHEN a Group item passes filtering, THE Navigation_Service SHALL set its Children property to contain only the recursively-filtered visible children (preserving their order and properties), not the original unfiltered children.

### Requirement 8: Href-to-Path Normalization

**User Story:** As a developer, I want consistent path comparison between NavItem Href values and page permission paths, so that permission checks are accurate.

#### Acceptance Criteria

1. WHEN a NavItem has an Href value without a leading slash (e.g., "admin/audit-log"), THE Navigation_Service SHALL prepend "/" before comparing against page permission paths.
2. WHEN a NavItem has an empty string Href, THE Navigation_Service SHALL treat it as the root path "/" for permission comparison.
3. WHEN a NavItem has a null Href, THE Navigation_Service SHALL skip permission comparison and treat the item as always visible.
4. WHEN a NavItem Href contains a trailing slash (e.g., "admin/audit-log/"), THE Navigation_Service SHALL strip the trailing slash before comparing against page permission paths, so that "/admin/audit-log/" and "/admin/audit-log" resolve to the same path.
5. THE Navigation_Service SHALL perform path comparisons using case-insensitive ordinal string comparison (OrdinalIgnoreCase).
