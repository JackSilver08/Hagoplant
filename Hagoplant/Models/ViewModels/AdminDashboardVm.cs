using Hagoplant.Models;

namespace Hagoplant.ViewModels
{
    public class AdminDashboardVm
    {
        public List<Product> Products { get; set; } = new();
        public List<BlogPost> BlogPosts { get; set; } = new();
        public List<User> Users { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<AuditLog> RecentAuditLogs { get; set; } = new();

        // Dashboard statistics
        public int TotalUsers { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
    }

    public class AdminUserListVm
    {
        public List<User> Users { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public string? SearchQuery { get; set; }
        public string? RoleFilter { get; set; }
        public bool? IsActiveFilter { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class AdminOrderListVm
    {
        public List<Order> Orders { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public string? StatusFilter { get; set; }
        public string? SearchQuery { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class ChangeUserRoleVm
    {
        public Guid UserId { get; set; }
        public string NewRole { get; set; } = UserRoles.User;
    }

    public class UpdateOrderStatusVm
    {
        public Guid OrderId { get; set; }
        public string NewStatus { get; set; } = "";
        public string? CancelReason { get; set; }
        public string? AdminNotes { get; set; }
    }
}
