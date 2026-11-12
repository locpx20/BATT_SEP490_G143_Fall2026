using BATT_PPMS.Models;

namespace BATT_PPMS.Helpers;

public sealed record ProductionScheduleViewModel(
    string PageName,
    Dictionary<string, string> Filters,
    DateOnly? DisplayDate,
    string? DateInput,
    bool AllShifts,
    IReadOnlyList<string?> Shifts,
    IReadOnlyList<PersonnelAssignment> Assignments,
    PageWindow Pagination,
    bool InvalidDate,
    int? CurrentWorkerId = null,
    IReadOnlyDictionary<(DateOnly Date, string? Shift), string>? ShiftStatuses = null,
    IReadOnlyDictionary<string, ShiftProductionPlan>? ShiftPlans = null,
    IReadOnlyList<ShiftProductionPlan>? DaysOff = null);
