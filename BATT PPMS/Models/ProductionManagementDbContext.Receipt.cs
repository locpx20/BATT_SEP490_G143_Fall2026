using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Models;

// Mapping mở rộng (file partial riêng để scaffold lại context không làm mất).
public partial class ProductionManagementDbContext
{
    private void ConfigureReceiptsAndMachine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Active")
                .HasColumnName("status");
        });

        modelBuilder.Entity<RawMaterialImport>(entity =>
        {
            entity.Property(e => e.ImportCode)
                .HasMaxLength(50)
                .HasColumnName("import_code");
            entity.Property(e => e.ReceiptId)
                .HasColumnName("receipt_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.ApprovedBy)
                .HasColumnName("approved_by");
            entity.Property(e => e.ApprovedAt)
                .HasColumnType("datetime")
                .HasColumnName("approved_at");

            entity.HasOne(d => d.Receipt)
                .WithMany()
                .HasForeignKey(d => d.ReceiptId);

            entity.HasOne(d => d.ApprovedByNavigation)
                .WithMany()
                .HasForeignKey(d => d.ApprovedBy);
        });

        modelBuilder.Entity<PurchaseReceipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId);
            entity.ToTable("PurchaseReceipt");

            entity.Property(e => e.ReceiptId).HasColumnName("receipt_id");
            entity.Property(e => e.ReceiptCode).HasMaxLength(50).HasColumnName("receipt_code");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.WeighedQuantity).HasColumnType("decimal(12, 2)").HasColumnName("weighed_quantity");
            entity.Property(e => e.ActualPrice).HasColumnType("decimal(12, 2)").HasColumnName("actual_price");
            entity.Property(e => e.ScalePhotoPath).HasMaxLength(300).HasColumnName("scale_photo_path");
            entity.Property(e => e.InvoicePhotoPath).HasMaxLength(300).HasColumnName("invoice_photo_path");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Pending").HasColumnName("status");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasColumnName("created_at");

            entity.HasOne(d => d.Invoice).WithMany(p => p.PurchaseReceipts)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Supplier).WithMany()
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.CreatedByNavigation).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }
}
