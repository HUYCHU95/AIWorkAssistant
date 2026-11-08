using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace AIWork.Web.Controllers;
public class ActionsController(WorkDb db,ActionService actions):ManagementController(db)
{
    public async Task<IActionResult> Index()=>View(await Db.Actions.OrderByDescending(a=>a.CreatedAtUtc).Take(100).ToListAsync());
    public async Task<IActionResult> Details(Guid id){var receipt=await Db.Actions.FindAsync(id);if(receipt==null)return NotFound();await History(id);return View(receipt);}
    [HttpPost]public async Task<IActionResult> Approve(Guid id,int version)
    {
        try{await actions.Approve(id,version);TempData["Message"]="Đã thực hiện giao dịch và ghi audit.";}catch(Exception ex)when(ex is ValidationException or DbUpdateException){TempData["Message"]="Không thực hiện: đề xuất thiếu thông tin, đã thay đổi, hoặc xung đột. Hãy xem lại dữ liệu và tạo đề xuất mới.";}return RedirectToAction("Details",new{id});
    }
    [HttpPost]public async Task<IActionResult> Reject(Guid id,int version){var r=await Db.Actions.FindAsync(id);if(r==null)return NotFound();if(r.Version!=version||r.Status is not("PendingApproval" or "NeedsReview"))return Conflict();CheckVersion(r,version);r.Status="Rejected";r.ReviewedBy=Db.Actor;r.ReviewedAtUtc=DateTime.UtcNow;await Db.SaveChangesAsync();return RedirectToAction("Details",new{id});}
    [HttpPost]public async Task<IActionResult> Undo(Guid id,int version){try{await actions.Undo(id,version);TempData["Message"]="Đã hoàn tác an toàn, giữ lịch sử.";}catch(Exception ex)when(ex is ValidationException or DbUpdateException){TempData["Message"]="Không thể hoàn tác: dữ liệu đã thay đổi hoặc loại hành động không hỗ trợ.";}return RedirectToAction("Details",new{id});}
}
