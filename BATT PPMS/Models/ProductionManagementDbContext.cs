using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Models;

public partial class ProductionManagementDbContext : DbContext
{
    public ProductionManagementDbContext(DbContextOptions<ProductionManagementDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BrokenEquipmentReport> BrokenEquipmentReports { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Delivery> Deliveries { get; set; }

    public virtual DbSet<EndOfShiftReport> EndOfShiftReports { get; set; }

    public virtual DbSet<FinishedGoodBatch> FinishedGoodBatches { get; set; }

    public virtual DbSet<InventoryItem> InventoryItems { get; set; }

    public virtual DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }

    public virtual DbSet<MaterialPurchaseRequest> MaterialPurchaseRequests { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<PersonnelAssignment> PersonnelAssignments { get; set; }

    public virtual DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }

    public virtual DbSet<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; }

    public virtual DbSet<Qcinspection> Qcinspections { get; set; }

    public virtual DbSet<RawMaterial> RawMaterials { get; set; }

    public virtual DbSet<RawMaterialExport> RawMaterialExports { get; set; }

    public virtual DbSet<RawMaterialImport> RawMaterialImports { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BrokenEquipmentReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__BrokenEq__779B7C586643EB5D");

            entity.ToTable("BrokenEquipmentReport");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.Description)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.EquipmentName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("equipment_name");
            entity.Property(e => e.ReportDate)
                .HasColumnType("datetime")
                .HasColumnName("report_date");
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
            entity.Property(e => e.Severity)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("severity");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Reported")
                .HasColumnName("status");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.BrokenEquipmentReports)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BrokenEqu__repor__39A368DE");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__CD65CB85371A6FC8");

            entity.ToTable("Customer");

            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Address)
                .IsUnicode(false)
                .HasColumnName("address");
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("contact_person");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("customer_name");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active")
                .HasColumnName("status");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Customers)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Customer__create__76E18148");
        });

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasKey(e => e.DeliveryId).HasName("PK__Delivery__1C5CF4F5688A8D9F");

            entity.ToTable("Delivery");

            entity.Property(e => e.DeliveryId).HasColumnName("delivery_id");
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by");
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Prepared")
                .HasColumnName("delivery_status");
            entity.Property(e => e.DriverName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("driver_name");
            entity.Property(e => e.LicensePlate)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("license_plate");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PickupDate)
                .HasColumnType("datetime")
                .HasColumnName("pickup_date");

            entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.Deliveries)
                .HasForeignKey(d => d.ConfirmedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Delivery__confir__35D2D7FA");

            entity.HasOne(d => d.Order).WithMany(p => p.Deliveries)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Delivery__order___34DEB3C1");
        });

        modelBuilder.Entity<EndOfShiftReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__EndOfShi__779B7C58B3F0299E");

            entity.ToTable("EndOfShiftReport");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Note)
                .IsUnicode(false)
                .HasColumnName("note");
            entity.Property(e => e.ProductionQuantity).HasColumnName("production_quantity");
            entity.Property(e => e.ShiftDate).HasColumnName("shift_date");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Submitted")
                .HasColumnName("status");

            entity.HasOne(d => d.Assignment).WithMany(p => p.EndOfShiftReports)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EndOfShif__assig__24A84BF8");

            entity.HasOne(d => d.Staff).WithMany(p => p.EndOfShiftReports)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EndOfShif__staff__259C7031");
        });

        modelBuilder.Entity<FinishedGoodBatch>(entity =>
        {
            entity.HasKey(e => e.BatchId).HasName("PK__Finished__DBFC0431190A0F20");

            entity.ToTable("FinishedGoodBatch");

            entity.HasIndex(e => e.BagNumber, "UQ__Finished__E71695E48169652C").IsUnique();

            entity.Property(e => e.BatchId).HasColumnName("batch_id");
            entity.Property(e => e.BagNumber)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("bag_number");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ProductionDate).HasColumnName("production_date");
            entity.Property(e => e.Quantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("quantity");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("In Stock")
                .HasColumnName("status");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FinishedGoodBatches)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FinishedG__creat__1471E42F");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.InventoryId).HasName("PK__Inventor__B59ACC4959E5281B");

            entity.ToTable("InventoryItem");

            entity.HasIndex(e => e.ItemCode, "UQ__Inventor__4A67201E77AB0E0A").IsUnique();

            entity.Property(e => e.InventoryId).HasColumnName("inventory_id");
            entity.Property(e => e.ItemCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("item_code");
            entity.Property(e => e.ItemName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("item_name");
            entity.Property(e => e.MinQuantity).HasColumnName("min_quantity");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Available")
                .HasColumnName("status");
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("unit");
        });

        modelBuilder.Entity<MaintenanceRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Maintena__18D3B90FEDED129C");

            entity.ToTable("MaintenanceRequest");

            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.BrokenReportId).HasColumnName("broken_report_id");
            entity.Property(e => e.Description)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("priority");
            entity.Property(e => e.RequestDate)
                .HasColumnType("datetime")
                .HasColumnName("request_date");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .HasColumnName("status");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.MaintenanceRequestAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("FK__Maintenan__assig__3F5C4234");

            entity.HasOne(d => d.BrokenReport).WithMany(p => p.MaintenanceRequests)
                .HasForeignKey(d => d.BrokenReportId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Maintenan__broke__3D73F9C2");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.MaintenanceRequestRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Maintenan__reque__3E681DFB");
        });

        modelBuilder.Entity<MaterialPurchaseRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Material__18D3B90F36671B1A");

            entity.ToTable("MaterialPurchaseRequest");

            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ItemName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("item_name");
            entity.Property(e => e.MaintenanceRequestId).HasColumnName("maintenance_request_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RequestDate)
                .HasColumnType("datetime")
                .HasColumnName("request_date");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .HasColumnName("status");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.MaterialPurchaseRequestApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__MaterialP__appro__45151B8A");

            entity.HasOne(d => d.MaintenanceRequest).WithMany(p => p.MaterialPurchaseRequests)
                .HasForeignKey(d => d.MaintenanceRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialP__maint__432CD318");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.MaterialPurchaseRequestRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialP__reque__4420F751");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__4659622981F92C85");

            entity.ToTable("Order");

            entity.HasIndex(e => e.OrderCode, "UQ__Order__99D12D3FCD69B1B0").IsUnique();

            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.InventoryId).HasColumnName("inventory_id");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("order_code");
            entity.Property(e => e.OrderDate)
                .HasColumnType("datetime")
                .HasColumnName("order_date");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RejectionReason)
                .IsUnicode(false)
                .HasColumnName("rejection_reason");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_amount");
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("unit_price");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.OrderApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__Order__approved___2C496DC0");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.OrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__created_b__2D3D91F9");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__customer___2A61254E");

            entity.HasOne(d => d.Inventory).WithMany(p => p.Orders)
                .HasForeignKey(d => d.InventoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__inventory__2B554987");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__3C5A40807CAEE4DD");

            entity.ToTable("OrderDetail");

            entity.Property(e => e.OrderDetailId).HasColumnName("order_detail_id");
            entity.Property(e => e.BatchId).HasColumnName("batch_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Quantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("quantity");

            entity.HasOne(d => d.Batch).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderDeta__batch__310E22DD");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderDeta__order__3019FEA4");
        });

        modelBuilder.Entity<PersonnelAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__Personne__DA8918140E9498F6");

            entity.ToTable("PersonnelAssignment");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssignmentDate).HasColumnName("assignment_date");
            entity.Property(e => e.Shift)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("shift");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Assigned")
                .HasColumnName("status");
            entity.Property(e => e.TaskDescription)
                .IsUnicode(false)
                .HasColumnName("task_description");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.PersonnelAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Personnel__assig__1FE396DB");

            entity.HasOne(d => d.Staff).WithMany(p => p.PersonnelAssignmentStaffs)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Personnel__staff__1EEF72A2");
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Purchase__F58DFD493ED1D0D9");

            entity.ToTable("PurchaseInvoice");

            entity.HasIndex(e => e.InvoiceCode, "UQ__Purchase__5ED70A35BF9A5EC3").IsUnique();

            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("invoice_code");
            entity.Property(e => e.Note)
                .IsUnicode(false)
                .HasColumnName("note");
            entity.Property(e => e.PurchaseDate)
                .HasColumnType("datetime")
                .HasColumnName("purchase_date");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_amount");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.PurchaseInvoiceApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__PurchaseI__appro__4BC21919");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseInvoiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseI__creat__4ACDF4E0");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseInvoices)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseI__suppl__49D9D0A7");
        });

        modelBuilder.Entity<PurchaseInvoiceDetail>(entity =>
        {
            entity.HasKey(e => e.InvoiceDetailId).HasName("PK__Purchase__84908DB6C60A9B9C");

            entity.ToTable("PurchaseInvoiceDetail");

            entity.Property(e => e.InvoiceDetailId).HasColumnName("invoice_detail_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.Quantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Invoice).WithMany(p => p.PurchaseInvoiceDetails)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseI__invoi__4E9E85C4");

            entity.HasOne(d => d.Material).WithMany(p => p.PurchaseInvoiceDetails)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseI__mater__4F92A9FD");
        });

        modelBuilder.Entity<Qcinspection>(entity =>
        {
            entity.HasKey(e => e.InspectionId).HasName("PK__QCInspec__C3C4E743E533FC3D");

            entity.ToTable("QCInspection");

            entity.HasIndex(e => e.InspectionCode, "UQ__QCInspec__D84165E6C0436F27").IsUnique();

            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.BatchId).HasColumnName("batch_id");
            entity.Property(e => e.InspectDate)
                .HasColumnType("datetime")
                .HasColumnName("inspect_date");
            entity.Property(e => e.InspectedBy).HasColumnName("inspected_by");
            entity.Property(e => e.InspectionCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("inspection_code");
            entity.Property(e => e.Note)
                .IsUnicode(false)
                .HasColumnName("note");
            entity.Property(e => e.RequestDate)
                .HasColumnType("datetime")
                .HasColumnName("request_date");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.Result)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("result");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .HasColumnName("status");

            entity.HasOne(d => d.Batch).WithMany(p => p.Qcinspections)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QCInspect__batch__1936994C");

            entity.HasOne(d => d.InspectedByNavigation).WithMany(p => p.QcinspectionInspectedByNavigations)
                .HasForeignKey(d => d.InspectedBy)
                .HasConstraintName("FK__QCInspect__inspe__1B1EE1BE");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.QcinspectionRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QCInspect__reque__1A2ABD85");
        });

        modelBuilder.Entity<RawMaterial>(entity =>
        {
            entity.HasKey(e => e.MaterialId).HasName("PK__RawMater__6BFE1D28B46CC657");

            entity.ToTable("RawMaterial");

            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.CurrentQuantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("current_quantity");
            entity.Property(e => e.MaterialName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValue("Nguyên li?u s?n xu?t chính")
                .HasColumnName("material_name");
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("kg")
                .HasColumnName("unit");
        });

        modelBuilder.Entity<RawMaterialExport>(entity =>
        {
            entity.HasKey(e => e.ExportId).HasName("PK__RawMater__323057CF5707B163");

            entity.ToTable("RawMaterialExport");

            entity.Property(e => e.ExportId).HasColumnName("export_id");
            entity.Property(e => e.ExportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("export_date");
            entity.Property(e => e.ExportedBy).HasColumnName("exported_by");
            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.Note)
                .IsUnicode(false)
                .HasColumnName("note");
            entity.Property(e => e.Quantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("quantity");

            entity.HasOne(d => d.ExportedByNavigation).WithMany(p => p.RawMaterialExports)
                .HasForeignKey(d => d.ExportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__expor__0FAD2F12");

            entity.HasOne(d => d.Material).WithMany(p => p.RawMaterialExports)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__mater__0EB90AD9");
        });

        modelBuilder.Entity<RawMaterialImport>(entity =>
        {
            entity.HasKey(e => e.ImportId).HasName("PK__RawMater__F3E6B05F53C4056E");

            entity.ToTable("RawMaterialImport");

            entity.Property(e => e.ImportId).HasColumnName("import_id");
            entity.Property(e => e.ImportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("import_date");
            entity.Property(e => e.ImportedBy).HasColumnName("imported_by");
            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.Note)
                .IsUnicode(false)
                .HasColumnName("note");
            entity.Property(e => e.Quantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("quantity");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("unit_price");

            entity.HasOne(d => d.ImportedByNavigation).WithMany(p => p.RawMaterialImports)
                .HasForeignKey(d => d.ImportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__impor__0AE879F5");

            entity.HasOne(d => d.Material).WithMany(p => p.RawMaterialImports)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__mater__09003183");

            entity.HasOne(d => d.Supplier).WithMany(p => p.RawMaterialImports)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__suppl__09F455BC");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__760965CCF451A7FF");

            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleName, "UQ__Role__783254B1C432CBC1").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Description)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__6EE594E8CBD7D0C1");

            entity.ToTable("Supplier");

            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Address)
                .IsUnicode(false)
                .HasColumnName("address");
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("contact_person");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active")
                .HasColumnName("status");
            entity.Property(e => e.SupplierName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("supplier_name");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Suppliers)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Supplier__create__7AB2122C");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__B9BE370F773D5C07");

            entity.ToTable("User");

            entity.HasIndex(e => e.Username, "UQ__User__F3DBC572CE8B5CD6").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("full_name");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("password");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active")
                .HasColumnName("status");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("username");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User__role_id__7310F064");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
