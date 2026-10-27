using BATT_PPMS.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Models;

// Lớp chặn cuối cùng: mọi entity Added/Modified đều được validate trước khi gửi xuống SQL Server.
// File partial riêng nên scaffold lại ProductionManagementDbContext.cs không làm mất.
public partial class ProductionManagementDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidatePendingChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidatePendingChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidatePendingChanges()
    {
        ChangeTracker.DetectChanges();   // để FK/navigation được fix-up trước khi validate

        var errors = new List<string>();
        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var name = entry.Entity.GetType().Name;
            errors.AddRange(ModelValidation.Validate(entry.Entity).Select(m => $"[{name}] {m}"));
        }

        if (errors.Count > 0)
            throw new DataValidationException(errors.Distinct());
    }
}
