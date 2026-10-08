namespace WebProveedores.Application.Contracts.Admin.Responses;

public sealed record AdminUserPage(IReadOnlyList<AdminUserSummary> Items, int Total, int Page, int PageSize, AdminUserCounts Counts);
