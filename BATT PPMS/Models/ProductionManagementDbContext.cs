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

    public virtual DbSet<Machine> Machines { get; set; }

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
            entity.HasKey(e => e.ReportId).HasName("PK__BrokenEq__779B7C5899066451");

            entity.ToTable("BrokenEquipmentReport");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.MachineId).HasColumnName("machine_id");
            entity.Property(e => e.ReportDate)
                .HasColumnType("datetime")
                .HasColumnName("report_date");
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
            entity.Property(e => e.Severity)
                .HasMaxLength(20)
                .HasColumnName("severity");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Reported")
                .HasColumnName("status");

            entity.HasOne(d => d.Machine).WithMany(p => p.BrokenEquipmentReports)
                .HasForeignKey(d => d.MachineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BrokenEqu__machi__6CD828CA");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.BrokenEquipmentReports)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BrokenEqu__repor__6DCC4D03");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__CD65CB8559A941D7");

            entity.ToTable("Customer");

            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(100)
                .HasColumnName("contact_person");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(100)
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
                .HasDefaultValue("Active")
                .HasColumnName("status");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Customers)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Customer__create__367C1819");
        });

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasKey(e => e.DeliveryId).HasName("PK__Delivery__1C5CF4F56F12E9F9");

            entity.ToTable("Delivery");

            entity.Property(e => e.DeliveryId).HasColumnName("delivery_id");
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by");
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Prepared")
                .HasColumnName("delivery_status");
            entity.Property(e => e.DriverName)
                .HasMaxLength(100)
                .HasColumnName("driver_name");
            entity.Property(e => e.LicensePlate)
                .HasMaxLength(20)
                .HasColumnName("license_plate");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PickupDate)
                .HasColumnType("datetime")
                .HasColumnName("pickup_date");

            entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.Deliveries)
                .HasForeignKey(d => d.ConfirmedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Delivery__confir__09746778");

            entity.HasOne(d => d.Order).WithMany(p => p.Deliveries)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Delivery__order___0880433F");
        });

        modelBuilder.Entity<EndOfShiftReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__EndOfShi__779B7C58F92EDD2C");

            entity.ToTable("EndOfShiftReport");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ProductionQuantity).HasColumnName("production_quantity");
            entity.Property(e => e.ShiftDate).HasColumnName("shift_date");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Submitted")
                .HasColumnName("status");

            entity.HasOne(d => d.Assignment).WithMany(p => p.EndOfShiftReports)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EndOfShif__assig__6442E2C9");

            entity.HasOne(d => d.Staff).WithMany(p => p.EndOfShiftReports)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EndOfShif__staff__65370702");
        });

        modelBuilder.Entity<FinishedGoodBatch>(entity =>
        {
            entity.HasKey(e => e.BatchId).HasName("PK__Finished__DBFC0431F7A0934E");

            entity.ToTable("FinishedGoodBatch");

            entity.HasIndex(e => e.BagNumber, "UQ__Finished__E71695E49F010973").IsUnique();

            entity.Property(e => e.BatchId).HasColumnName("batch_id");
            entity.Property(e => e.BagNumber)
                .HasMaxLength(50)
                .HasColumnName("bag_number");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ProductionDate).HasColumnName("production_date");
            entity.Property(e => e.Quantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("quantity");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("In Stock")
                .HasColumnName("status");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FinishedGoodBatches)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FinishedG__creat__540C7B00");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.InventoryId).HasName("PK__Inventor__B59ACC49F4F5A1A0");

            entity.ToTable("InventoryItem");

            entity.HasIndex(e => e.ItemCode, "UQ__Inventor__4A67201E81398A55").IsUnique();

            entity.Property(e => e.InventoryId).HasColumnName("inventory_id");
            entity.Property(e => e.ItemCode)
                .HasMaxLength(50)
                .HasColumnName("item_code");
            entity.Property(e => e.ItemName)
                .HasMaxLength(100)
                .HasColumnName("item_name");
            entity.Property(e => e.MinQuantity).HasColumnName("min_quantity");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Available")
                .HasColumnName("status");
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .HasColumnName("unit");
        });

        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(e => e.MachineId).HasName("PK__Machine__7B75BEA9C637DBF7");

            entity.ToTable("Machine");

            entity.HasIndex(e => e.MachineCode, "UQ__Machine__FDCF4D73A268A568").IsUnique();

            entity.Property(e => e.MachineId).HasColumnName("machine_id");
            entity.Property(e => e.Brand)
                .HasMaxLength(100)
                .HasColumnName("brand");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.MachineCode)
                .HasMaxLength(50)
                .HasColumnName("machine_code");
            entity.Property(e => e.MachineName)
                .HasMaxLength(100)
                .HasColumnName("machine_name");
            entity.Property(e => e.ProcessName)
                .HasMaxLength(100)
                .HasColumnName("process_name");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Machines)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Machine__created__690797E6");
        });

        modelBuilder.Entity<MaintenanceRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Maintena__18D3B90FBA9323A5");

            entity.ToTable("MaintenanceRequest");

            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.BrokenReportId).HasColumnName("broken_report_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasColumnName("priority");
            entity.Property(e => e.RequestDate)
                .HasColumnType("datetime")
                .HasColumnName("request_date");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasColumnName("status");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.MaintenanceRequestAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("FK__Maintenan__assig__73852659");

            entity.HasOne(d => d.BrokenReport).WithMany(p => p.MaintenanceRequests)
                .HasForeignKey(d => d.BrokenReportId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Maintenan__broke__719CDDE7");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.MaintenanceRequestRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Maintenan__reque__72910220");
        });

        modelBuilder.Entity<MaterialPurchaseRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Material__18D3B90FD345AB60");

            entity.ToTable("MaterialPurchaseRequest");

            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ItemName)
                .HasMaxLength(100)
                .HasColumnName("item_name");
            entity.Property(e => e.MaintenanceRequestId).HasColumnName("maintenance_request_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RequestDate)
                .HasColumnType("datetime")
                .HasColumnName("request_date");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasColumnName("status");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.MaterialPurchaseRequestApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__MaterialP__appro__793DFFAF");

            entity.HasOne(d => d.MaintenanceRequest).WithMany(p => p.MaterialPurchaseRequests)
                .HasForeignKey(d => d.MaintenanceRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialP__maint__7755B73D");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.MaterialPurchaseRequestRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialP__reque__7849DB76");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__465962292D296CCB");

            entity.ToTable("Order");

            entity.HasIndex(e => e.OrderCode, "UQ__Order__99D12D3F861860DE").IsUnique();

            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.InventoryId).HasColumnName("inventory_id");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .HasColumnName("order_code");
            entity.Property(e => e.OrderDate)
                .HasColumnType("datetime")
                .HasColumnName("order_date");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
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
                .HasConstraintName("FK__Order__approved___7FEAFD3E");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.OrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__created_b__00DF2177");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__customer___7E02B4CC");

            entity.HasOne(d => d.Inventory).WithMany(p => p.Orders)
                .HasForeignKey(d => d.InventoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__inventory__7EF6D905");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__3C5A40800C9EB3D2");

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
                .HasConstraintName("FK__OrderDeta__batch__04AFB25B");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderDeta__order__03BB8E22");
        });

        modelBuilder.Entity<PersonnelAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__Personne__DA891814077A8438");

            entity.ToTable("PersonnelAssignment");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssignmentDate).HasColumnName("assignment_date");
            entity.Property(e => e.Shift)
                .HasMaxLength(50)
                .HasColumnName("shift");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Assigned")
                .HasColumnName("status");
            entity.Property(e => e.TaskDescription).HasColumnName("task_description");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.PersonnelAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Personnel__assig__5F7E2DAC");

            entity.HasOne(d => d.Staff).WithMany(p => p.PersonnelAssignmentStaffs)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Personnel__staff__5E8A0973");
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Purchase__F58DFD497A2BBA7C");

            entity.ToTable("PurchaseInvoice");

            entity.HasIndex(e => e.InvoiceCode, "UQ__Purchase__5ED70A3535712598").IsUnique();

            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(50)
                .HasColumnName("invoice_code");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.PurchaseDate)
                .HasColumnType("datetime")
                .HasColumnName("purchase_date");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_amount");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.PurchaseInvoiceApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__PurchaseI__appro__10216507");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseInvoiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseI__creat__0F2D40CE");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseInvoices)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseI__suppl__0E391C95");
        });

        modelBuilder.Entity<PurchaseInvoiceDetail>(entity =>
        {
            entity.HasKey(e => e.InvoiceDetailId).HasName("PK__Purchase__84908DB613C0EB1C");

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
                .HasConstraintName("FK__PurchaseI__invoi__12FDD1B2");

            entity.HasOne(d => d.Material).WithMany(p => p.PurchaseInvoiceDetails)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseI__mater__13F1F5EB");
        });

        modelBuilder.Entity<Qcinspection>(entity =>
        {
            entity.HasKey(e => e.InspectionId).HasName("PK__QCInspec__C3C4E743006CC6A2");

            entity.ToTable("QCInspection");

            entity.HasIndex(e => e.InspectionCode, "UQ__QCInspec__D84165E616C545DA").IsUnique();

            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.BatchId).HasColumnName("batch_id");
            entity.Property(e => e.InspectDate)
                .HasColumnType("datetime")
                .HasColumnName("inspect_date");
            entity.Property(e => e.InspectedBy).HasColumnName("inspected_by");
            entity.Property(e => e.InspectionCode)
                .HasMaxLength(50)
                .HasColumnName("inspection_code");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.RequestDate)
                .HasColumnType("datetime")
                .HasColumnName("request_date");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.Result)
                .HasMaxLength(20)
                .HasColumnName("result");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasColumnName("status");

            entity.HasOne(d => d.Batch).WithMany(p => p.Qcinspections)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QCInspect__batch__58D1301D");

            entity.HasOne(d => d.InspectedByNavigation).WithMany(p => p.QcinspectionInspectedByNavigations)
                .HasForeignKey(d => d.InspectedBy)
                .HasConstraintName("FK__QCInspect__inspe__5AB9788F");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.QcinspectionRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QCInspect__reque__59C55456");
        });

        modelBuilder.Entity<RawMaterial>(entity =>
        {
            entity.HasKey(e => e.MaterialId).HasName("PK__RawMater__6BFE1D28887BBE9A");

            entity.ToTable("RawMaterial");

            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.CurrentQuantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("current_quantity");
            entity.Property(e => e.MaterialName)
                .HasMaxLength(100)
                .HasDefaultValue("Nguyên liệu sản xuất chính")
                .HasColumnName("material_name");
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .HasDefaultValue("kg")
                .HasColumnName("unit");
        });

        modelBuilder.Entity<RawMaterialExport>(entity =>
        {
            entity.HasKey(e => e.ExportId).HasName("PK__RawMater__323057CF4AB86C41");

            entity.ToTable("RawMaterialExport");

            entity.Property(e => e.ExportId).HasColumnName("export_id");
            entity.Property(e => e.ExportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("export_date");
            entity.Property(e => e.ExportedBy).HasColumnName("exported_by");
            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.Quantity)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("quantity");

            entity.HasOne(d => d.ExportedByNavigation).WithMany(p => p.RawMaterialExports)
                .HasForeignKey(d => d.ExportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__expor__4F47C5E3");

            entity.HasOne(d => d.Material).WithMany(p => p.RawMaterialExports)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__mater__4E53A1AA");
        });

        modelBuilder.Entity<RawMaterialImport>(entity =>
        {
            entity.HasKey(e => e.ImportId).HasName("PK__RawMater__F3E6B05F25E1D87C");

            entity.ToTable("RawMaterialImport");

            entity.Property(e => e.ImportId).HasColumnName("import_id");
            entity.Property(e => e.ImportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("import_date");
            entity.Property(e => e.ImportedBy).HasColumnName("imported_by");
            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.Note).HasColumnName("note");
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
                .HasConstraintName("FK__RawMateri__impor__4A8310C6");

            entity.HasOne(d => d.Material).WithMany(p => p.RawMaterialImports)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__mater__489AC854");

            entity.HasOne(d => d.Supplier).WithMany(p => p.RawMaterialImports)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RawMateri__suppl__498EEC8D");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__760965CC8B9AB9BF");

            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleName, "UQ__Role__783254B13E9D29C4").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__6EE594E87944E9E6");

            entity.ToTable("Supplier");

            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(100)
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
                .HasDefaultValue("Active")
                .HasColumnName("status");
            entity.Property(e => e.SupplierName)
                .HasMaxLength(100)
                .HasColumnName("supplier_name");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Suppliers)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Supplier__create__3A4CA8FD");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__B9BE370FC707D735");

            entity.ToTable("User");

            entity.HasIndex(e => e.Username, "UQ__User__F3DBC572FE197A53").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
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
                .HasDefaultValue("Active")
                .HasColumnName("status");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User__role_id__32AB8735");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
