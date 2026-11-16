using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Models;

// Mapping luồng bán hàng & xuất kho (file partial riêng để scaffold lại context không làm mất).
public partial class ProductionManagementDbContext
{
    public DbSet<ExportSlip> ExportSlips => Set<ExportSlip>();
    public DbSet<ExportSlipLine> ExportSlipLines => Set<ExportSlipLine>();

    private void ConfigureSalesExport(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.Grade).HasMaxLength(1).HasColumnName("grade");
            entity.Property(e => e.ApprovedAt).HasColumnType("datetime").HasColumnName("approved_at");
            entity.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
        });

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.Property(e => e.DriverPhone).HasMaxLength(20).HasColumnName("driver_phone");
            entity.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
            entity.Property(e => e.ReturnReason).HasMaxLength(500).HasColumnName("return_reason");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByNavigation).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ExportSlip>(entity =>
        {
            entity.ToTable("ExportSlip");
            entity.HasKey(e => e.SlipId);
            entity.Property(e => e.SlipId).HasColumnName("slip_id");
            entity.Property(e => e.SlipCode).HasMaxLength(50).HasColumnName("slip_code");
            entity.Property(e => e.DeliveryId).HasColumnName("delivery_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue(ExportSlipStatus.Processing).HasColumnName("status");
            entity.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
            entity.Property(e => e.ImagePath).HasMaxLength(300).HasColumnName("image_path");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasColumnName("created_at");
            entity.Property(e => e.CompletedBy).HasColumnName("completed_by");
            entity.Property(e => e.CompletedAt).HasColumnType("datetime").HasColumnName("completed_at");
            entity.HasIndex(e => e.SlipCode).IsUnique();
            entity.HasIndex(e => e.DeliveryId).IsUnique();

            entity.HasOne(e => e.Delivery).WithOne(d => d.ExportSlip)
                .HasForeignKey<ExportSlip>(e => e.DeliveryId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Order).WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CreatedByNavigation).WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CompletedByNavigation).WithMany()
                .HasForeignKey(e => e.CompletedBy)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ExportSlipLine>(entity =>
        {
            entity.ToTable("ExportSlipLine");
            entity.HasKey(e => e.LineId);
            entity.Property(e => e.LineId).HasColumnName("line_id");
            entity.Property(e => e.SlipId).HasColumnName("slip_id");
            entity.Property(e => e.BatchId).HasColumnName("batch_id");
            entity.Property(e => e.Grade).HasMaxLength(1).HasColumnName("grade");
            entity.Property(e => e.Bags).HasColumnName("bags");
            entity.Property(e => e.Quantity).HasColumnType("decimal(12, 2)").HasColumnName("quantity");

            entity.HasOne(e => e.Slip).WithMany(s => s.Lines)
                .HasForeignKey(e => e.SlipId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Batch).WithMany()
                .HasForeignKey(e => e.BatchId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
