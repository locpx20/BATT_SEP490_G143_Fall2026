using Microsoft.EntityFrameworkCore;
using BATT_PPMS.Models;

namespace BATT_PPMS.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets - ánh xạ tới các bảng trong DB
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<QCInspection> QCInspections { get; set; }
        public DbSet<EndOfShiftReport> EndOfShiftReports { get; set; }
        public DbSet<PersonnelAssignment> PersonnelAssignments { get; set; }
        public DbSet<BrokenEquipmentReport> BrokenEquipmentReports { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<MaterialPurchaseRequest> MaterialPurchaseRequests { get; set; }
        public DbSet<MaterialPurchaseRecord> MaterialPurchaseRecords { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình tên bảng (dùng tên có ngoặc vuông cho reserved keywords)
            modelBuilder.Entity<Role>().ToTable("Role");
            modelBuilder.Entity<User>().ToTable("User");
            modelBuilder.Entity<Order>().ToTable("Order");

            // Giải quyết multiple cascade paths
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Approver)
                .WithMany()
                .HasForeignKey(o => o.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Creator)
                .WithMany()
                .HasForeignKey(o => o.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QCInspection>()
                .HasOne(q => q.Requester)
                .WithMany()
                .HasForeignKey(q => q.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QCInspection>()
                .HasOne(q => q.Inspector)
                .WithMany()
                .HasForeignKey(q => q.InspectedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PersonnelAssignment>()
                .HasOne(p => p.Staff)
                .WithMany()
                .HasForeignKey(p => p.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PersonnelAssignment>()
                .HasOne(p => p.Assigner)
                .WithMany()
                .HasForeignKey(p => p.AssignedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MaintenanceRequest>()
                .HasOne(m => m.Requester)
                .WithMany()
                .HasForeignKey(m => m.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MaintenanceRequest>()
                .HasOne(m => m.Assignee)
                .WithMany()
                .HasForeignKey(m => m.AssignedTo)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MaterialPurchaseRequest>()
                .HasOne(m => m.Requester)
                .WithMany()
                .HasForeignKey(m => m.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MaterialPurchaseRequest>()
                .HasOne(m => m.Approver)
                .WithMany()
                .HasForeignKey(m => m.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(p => p.Creator)
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(p => p.Approver)
                .WithMany()
                .HasForeignKey(p => p.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
