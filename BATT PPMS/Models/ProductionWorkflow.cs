using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Models;

public class ProductionQualityRequest
{
    public byte[]? ResultImage { get; set; }
    public string? ResultImageType { get; set; }
    public int RequestId { get; set; }
    public int AssignmentId { get; set; }
    public int InspectionId { get; set; }
    public int? ReportId { get; set; }
    public DateTime CreatedAt { get; set; }
    public PersonnelAssignment Assignment { get; set; } = null!;
    public Qcinspection Inspection { get; set; } = null!;
    public EndOfShiftReport? Report { get; set; }
    public FinishedGoodsReceipt? Receipt { get; set; }
}

public class FinishedGoodsReceipt
{
    public string? RequestNote { get; set; }
    public int ReceiptId { get; set; }
    public int QualityRequestId { get; set; }
    public int? RequestedBy { get; set; }
    public int? ReceivedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public string Status { get; set; } = "Pending";
    public string? RejectionReason { get; set; }
    public string? DiscrepancyNote { get; set; }
    public DateTime? StaffConfirmedAt { get; set; }
    public ProductionQualityRequest QualityRequest { get; set; } = null!;
    public User? Requester { get; set; }
    public User? Receiver { get; set; }
}

public class ProductionMaterialRequest
{
    public int RequestId { get; set; }
    public int AssignmentId { get; set; }
    public int MaterialId { get; set; }
    public int? RequestedBy { get; set; }
    public int? IssuedBy { get; set; }
    public int? ExportId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public PersonnelAssignment Assignment { get; set; } = null!;
    public RawMaterial Material { get; set; } = null!;
    public User? Requester { get; set; }
    public User? Issuer { get; set; }
    public RawMaterialExport? Export { get; set; }
}

public partial class ProductionManagementDbContext
{
    public DbSet<DailyMaterialPlan> DailyMaterialPlans => Set<DailyMaterialPlan>();
    public DbSet<ShiftProductionPlan> ShiftProductionPlans => Set<ShiftProductionPlan>();
    public DbSet<ShiftMaterialUsage> ShiftMaterialUsages => Set<ShiftMaterialUsage>();
    public DbSet<MaterialUsageReview> MaterialUsageReviews => Set<MaterialUsageReview>();
    public DbSet<ProductionQualityRequest> ProductionQualityRequests => Set<ProductionQualityRequest>();
    public DbSet<FinishedGoodsReceipt> FinishedGoodsReceipts => Set<FinishedGoodsReceipt>();
    public DbSet<ProductionMaterialRequest> ProductionMaterialRequests => Set<ProductionMaterialRequest>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyMaterialPlan>(e=>{
            e.ToTable("DailyMaterialPlan");e.HasKey(x=>x.PlanDate);
            e.Property(x=>x.ExpectedKg).HasPrecision(12,2);
            e.HasOne(x=>x.Planner).WithMany().HasForeignKey(x=>x.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ShiftProductionPlan>(e =>
        {
            e.ToTable("ShiftProductionPlan");
            e.HasKey(x => new { x.PlanDate, x.Shift });
            e.Property(x => x.Shift).HasMaxLength(50);
            e.Property(x => x.ExpectedMaterialKg).HasPrecision(12, 2);
            e.Property(x => x.ExpectedFinishedKg).HasPrecision(12, 2);
            e.Property(x => x.Note).HasMaxLength(1000);
        });
        modelBuilder.Entity<MaterialUsageReview>(e => {
            e.ToTable("MaterialUsageReview"); e.HasKey(x => x.ReportId);
            e.Property(x => x.ReportId).ValueGeneratedNever();
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.RejectionReason).HasMaxLength(1000);
            e.HasOne(x => x.Report).WithOne().HasForeignKey<MaterialUsageReview>(x => x.ReportId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ShiftMaterialUsage>(e => {
            e.ToTable("ShiftMaterialUsage"); e.HasKey(x => x.UsageId);
            e.HasIndex(x => new { x.ReportId, x.MaterialId }).IsUnique();
            e.Property(x => x.Quantity).HasPrecision(12, 2);
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Export).WithMany().HasForeignKey(x => x.ExportId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ProductionQualityRequest>(e => {
            e.ToTable("ProductionQualityRequest"); e.HasKey(x => x.RequestId);
            e.Property(x => x.ResultImageType).HasMaxLength(30);
            e.HasIndex(x => x.InspectionId).IsUnique();
            e.HasIndex(x => x.ReportId).IsUnique().HasFilter("[ReportId] IS NOT NULL");
            e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Inspection).WithMany().HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<FinishedGoodsReceipt>(e => {
            e.ToTable("FinishedGoodsReceipt"); e.HasKey(x => x.ReceiptId);
            e.Property(x => x.RequestNote).HasMaxLength(2000);
            e.Property(x => x.ExpectedQuantity).HasPrecision(12, 2); e.Property(x => x.ActualQuantity).HasPrecision(12, 2);
            e.Property(x => x.Status).HasMaxLength(30); e.Property(x => x.RejectionReason).HasMaxLength(1000); e.Property(x => x.DiscrepancyNote).HasMaxLength(2000);
            e.HasOne(x => x.QualityRequest).WithOne(x => x.Receipt).HasForeignKey<FinishedGoodsReceipt>(x => x.QualityRequestId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Receiver).WithMany().HasForeignKey(x => x.ReceivedBy).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ProductionMaterialRequest>(e => {
            e.ToTable("ProductionMaterialRequest"); e.HasKey(x => x.RequestId);
            e.Property(x => x.Quantity).HasPrecision(12, 2); e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(2000); e.Property(x => x.RejectionReason).HasMaxLength(1000);
            e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Issuer).WithMany().HasForeignKey(x => x.IssuedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Export).WithMany().HasForeignKey(x => x.ExportId).OnDelete(DeleteBehavior.NoAction);
        });
        ConfigureReceiptsAndMachine(modelBuilder);
    }
}
