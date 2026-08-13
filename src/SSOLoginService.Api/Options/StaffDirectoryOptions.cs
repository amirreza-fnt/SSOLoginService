namespace SSOLoginService.Api.Options;

/// <summary>
/// Optional municipality staff directory: maps national codes to workflow roles/groups
/// when MOI SSO does not return municipal ROLE_* claims. Used by /api/auth/me and JWT.
/// </summary>
public sealed class StaffDirectoryOptions
{
    public const string SectionName = "StaffDirectory";

    public List<StaffEntry> Entries { get; set; } = new();

    public sealed class StaffEntry
    {
        public string MelliCode { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public string? GroupId { get; set; }
    }
}
