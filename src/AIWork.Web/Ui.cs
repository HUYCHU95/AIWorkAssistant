using AIWork.Application;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace AIWork.Web;

public static class Ui
{
    public static IEnumerable<string> RecordLinks(string content) => System.Text.RegularExpressions.Regex.Matches(content,@"/(?:Tasks|Projects|Workstreams|Sources|Memory|Actions|Tracking)/Details/[0-9a-fA-F-]{36}").Select(m=>m.Value).Where(v=>Guid.TryParse(v.Split('/').Last(),out _)).Distinct();
    public static string TrackingName(AIWork.Domain.TrackingKind? kind) => kind switch { AIWork.Domain.TrackingKind.Issue => "Vấn đề", AIWork.Domain.TrackingKind.Commitment => "Cam kết", AIWork.Domain.TrackingKind.Instruction => "Chỉ thị", AIWork.Domain.TrackingKind.Decision => "Quyết định", _ => "Theo dõi chuyên sâu" };
    public static IEnumerable<SelectListItem> Options<T>() where T : struct, Enum => Enum.GetValues<T>().Select(v => new SelectListItem(WorkRules.Label(v), Convert.ToInt32(v).ToString()));
}
