using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;
namespace BATT_PPMS.Services;

public sealed class DailyMaterialPlanning(ProductionManagementDbContext db)
{
    public static bool ValidQuantity(decimal kg)=>kg>0&&kg<=9999999999.99m&&decimal.Round(kg,2)==kg;
    // Caller owns the transaction, allowing plan assignments and daily estimate to commit together.
    public async Task Set(int manager,DateOnly date,decimal kg) {
        if(!ValidQuantity(kg)||date.Year<2000||date.Year>2100)throw new ProductionFlowException("Nhập khối lượng nguyên liệu dự kiến lớn hơn 0 kg, tối đa 2 chữ số thập phân, và ngày hợp lệ.");
        if(!await db.Users.AnyAsync(x=>x.UserId==manager&&x.Status=="Active"&&x.Role.RoleName==RoleConstants.Manager))throw new ProductionFlowException("Chỉ Manager được cập nhật nguyên liệu dự kiến.");
        var plan=await db.DailyMaterialPlans.SingleOrDefaultAsync(x=>x.PlanDate==date);
        if(plan==null){plan=new(){PlanDate=date};db.DailyMaterialPlans.Add(plan);}
        plan.ExpectedKg=kg;plan.UpdatedBy=manager;plan.UpdatedAt=ProductionFlow.Now;
        var keepers=await db.Users.Where(x=>x.Status=="Active"&&x.Role.RoleName==RoleConstants.WarehouseStaff).Select(x=>x.UserId).ToListAsync();
        foreach(var keeper in keepers)Notifier.Add(db,keeper,"Kế hoạch nguyên liệu đã cập nhật",$"Ngày {date:dd/MM/yyyy}: dự kiến {kg:N2} kg.",$"/Warehouse/Index?tab=plans&PlanDate={date:yyyy-MM-dd}");
    }
}
