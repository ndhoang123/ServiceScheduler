namespace ServiceScheduler.Api.Models;

internal static class AppointmentStateMachine
{
    // single source of truth for all valid state transitions
    private static readonly Dictionary<AppointmentStatus, HashSet<AppointmentStatus>> _allowed = new()
    {
        [AppointmentStatus.Pending]    = [AppointmentStatus.Confirmed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Confirmed]  = [AppointmentStatus.InProgress, AppointmentStatus.Cancelled],
        [AppointmentStatus.InProgress] = [AppointmentStatus.Completed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Completed]  = [],
        [AppointmentStatus.Cancelled]  = [],
    };

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to)
        => _allowed.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static string TransitionError(AppointmentStatus from, AppointmentStatus to)
        => $"Cannot transition appointment from '{from}' to '{to}'.";
}
