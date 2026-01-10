using Hagoplant.Models;

namespace Hagoplant.ViewModels
{
    public class AdminDashboardVm
    {
        public List<Product> Products { get; set; } = new();
        // Blog posts cho trang Admin
        public List<BlogPost> BlogPosts { get; set; } = new();

        // THÊM DÒNG NÀY
        public List<User> Users { get; set; } = new();
    }
}
