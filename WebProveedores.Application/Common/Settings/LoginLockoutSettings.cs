namespace WebProveedores.Application.Common.Settings;

/// <summary>Bloqueo temporal de la cuenta tras varios intentos fallidos.</summary>
public sealed record LoginLockoutSettings(int MaxFailedLogins, int LockoutMinutes);
