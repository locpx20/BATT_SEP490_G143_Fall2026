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
            entity.Property(e => e.InboundRequestId)
                .HasColumnName("inbound_request_id");
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

            entity.HasOne(d => d.InboundRequest)
                .WithMany(p => p.RawMaterialImports)
                .HasForeignKey(d => d.InboundRequestId);

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
            entity.Property(e => e.RejectionReason).HasMaxLength(500).HasColumnName("rejection_reason");
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

        modelBuilder.Entity<InboundRequest>(entity =>
        {
            entity.HasKey(e => e.InboundRequestId);
            entity.ToTable("InboundRequest");

            entity.Property(e => e.InboundRequestId).HasColumnName("inbound_request_id");
            entity.Property(e => e.RequestCode).HasMaxLength(50).HasColumnName("request_code");
            entity.Property(e => e.ReceiptId).HasColumnName("receipt_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.DeliveryQuantity).HasColumnType("decimal(12, 2)").HasColumnName("delivery_quantity");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(12, 2)").HasColumnName("unit_price");
            entity.Property(e => e.LicensePlate).HasMaxLength(30).HasColumnName("license_plate");
            entity.Property(e => e.DriverName).HasMaxLength(100).HasColumnName("driver_name");
            entity.Property(e => e.DeliveryTime).HasColumnType("datetime").HasColumnName("delivery_time");
            entity.Property(e => e.ScalePhotoPath).HasMaxLength(300).HasColumnName("scale_photo_path");
            entity.Property(e => e.InvoicePhotoPath).HasMaxLength(300).HasColumnName("invoice_photo_path");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("WaitWarehouse").HasColumnName("status");
            entity.Property(e => e.RejectionReason).HasMaxLength(500).HasColumnName("rejection_reason");
            entity.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasColumnName("created_at");

            entity.HasOne(d => d.Receipt).WithMany(p => p.InboundRequests)
                .HasForeignKey(d => d.ReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Invoice).WithMany(p => p.InboundRequests)
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
