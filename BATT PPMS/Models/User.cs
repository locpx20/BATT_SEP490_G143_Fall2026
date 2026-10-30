using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class User
{
    public int UserId { get; set; }

    // Stored in UTC; null for legacy accounts with no recorded creation date.
    public DateTime? CreatedAt { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Status { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public int RoleId { get; set; }

    public virtual ICollection<BrokenEquipmentReport> BrokenEquipmentReports { get; set; } = new List<BrokenEquipmentReport>();

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();

    public virtual ICollection<EndOfShiftReport> EndOfShiftReports { get; set; } = new List<EndOfShiftReport>();

    public virtual ICollection<FinishedGoodBatch> FinishedGoodBatches { get; set; } = new List<FinishedGoodBatch>();

    public virtual ICollection<Machine> Machines { get; set; } = new List<Machine>();

    public virtual ICollection<MaintenanceRequest> MaintenanceRequestAssignedToNavigations { get; set; } = new List<MaintenanceRequest>();

    public virtual ICollection<MaintenanceRequest> MaintenanceRequestRequestedByNavigations { get; set; } = new List<MaintenanceRequest>();

    public virtual ICollection<MaterialPurchaseRequest> MaterialPurchaseRequestApprovedByNavigations { get; set; } = new List<MaterialPurchaseRequest>();

    public virtual ICollection<MaterialPurchaseRequest> MaterialPurchaseRequestRequestedByNavigations { get; set; } = new List<MaterialPurchaseRequest>();

    public virtual ICollection<Order> OrderApprovedByNavigations { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderCreatedByNavigations { get; set; } = new List<Order>();

    public virtual ICollection<PersonnelAssignment> PersonnelAssignmentAssignedByNavigations { get; set; } = new List<PersonnelAssignment>();

    public virtual ICollection<PersonnelAssignment> PersonnelAssignmentStaffs { get; set; } = new List<PersonnelAssignment>();

    public virtual ICollection<PurchaseInvoice> PurchaseInvoiceApprovedByNavigations { get; set; } = new List<PurchaseInvoice>();

    public virtual ICollection<PurchaseInvoice> PurchaseInvoiceCreatedByNavigations { get; set; } = new List<PurchaseInvoice>();

    public virtual ICollection<Qcinspection> QcinspectionInspectedByNavigations { get; set; } = new List<Qcinspection>();

    public virtual ICollection<Qcinspection> QcinspectionRequestedByNavigations { get; set; } = new List<Qcinspection>();

    public virtual ICollection<RawMaterialExport> RawMaterialExports { get; set; } = new List<RawMaterialExport>();

    public virtual ICollection<RawMaterialImport> RawMaterialImports { get; set; } = new List<RawMaterialImport>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
}
