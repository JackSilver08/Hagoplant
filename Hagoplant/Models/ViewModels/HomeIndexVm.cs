using Hagoplant.Models;

namespace Hagoplant.ViewModels
{
    public class HomeIndexVm
    {
        public List<Product> FeaturedProducts { get; set; } = new();
        // Blog posts cho trang Admin
        public List<BlogPost> BlogPosts { get; set; } = new();
    }
}
