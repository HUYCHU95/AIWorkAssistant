using AIWork.Domain;
namespace AIWork.Application;

public static class WorkRules
{
    public static TimeZoneInfo Vietnam => TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Vietnam));
    public static string LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Vietnam).ToString("dd/MM/yyyy HH:mm");
    public static string Date(DateOnly? date) => date?.ToString("dd/MM/yyyy") ?? "Chưa xác định";
    public static void Transition(WorkTask task, WorkStatus previous)
    {
        if (task.Status == WorkStatus.Done) task.CompletedAtUtc ??= DateTime.UtcNow;
        else task.CompletedAtUtc = null;
    }
    public static string Progress(IEnumerable<WorkTask> tasks)
    {
        var scoped = tasks.Where(t => t.ArchivedAtUtc == null && t.Status != WorkStatus.Cancelled).ToList();
        return scoped.Count == 0 ? "N/A" : $"{scoped.Count(t => t.Status == WorkStatus.Done)}/{scoped.Count} ({scoped.Count(t => t.Status == WorkStatus.Done) * 100 / scoped.Count}%)";
    }
    public static string Label(object? value) => value?.ToString() switch
    {
        "Open" => "Đang mở", "Resolved" => "Đã xử lý, chờ xác minh", "Verified" => "Đã xác minh", "Reported" => "Nguồn báo cáo", "Conflicted" => "Mâu thuẫn cần kiểm tra", "Superseded" => "Đã thay thế", "Email" => "Email dán thủ công", "Meeting" => "Biên bản họp", "Note" => "Ghi chú", "Instruction" => "Chỉ thị", "File" => "Tệp", "Chat" => "Hội thoại", "Todo" => "Chưa bắt đầu",
        "InProgress" => "Đang thực hiện",
        "Waiting" => "Chờ xác nhận",
        "Blocked" => "Vướng mắc",
        "Done" => "Hoàn thành",
        "Cancelled" => "Đã hủy",
        "Committed" => "Đã cam kết",
        "Planned" => "Kế hoạch",
        "Suggested" => "Gợi ý · cần xác nhận",
        "Active" => "Đang hoạt động",
        "Paused" => "Tạm dừng",
        "Archived" => "Lưu trữ",
        "Completed" => "Hoàn thành",
        "Critical" => "Khẩn cấp",
        "High" => "Cao",
        "Normal" => "Bình thường",
        "Low" => "Thấp",
        "Concept" => "Ý tưởng",
        "Build" => "Chế tạo",
        "Order" => "Đặt hàng",
        "Setup" => "Lắp đặt",
        "Apply" => "Áp dụng",
        "Other" => "Khác",
        "Unknown" => "Chưa xác minh",
        "OnTrack" => "Đúng tiến độ",
        "AtRisk" => "Có rủi ro",
        "Delayed" => "Chậm tiến độ",
        null => "—",
        _ => value.ToString()!
    };
}

