using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RefactoringTool
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = @"c:\Hagoplant\Hagoplant\Views\Admin\Index.bak.cshtml";
            var text = File.ReadAllText(path);

            // Get Styles
            var styleMatch = Regex.Match(text, @"<style>(.*?)</style>", RegexOptions.Singleline);
            var styles = styleMatch.Success ? styleMatch.Groups[1].Value : "";

            // Get Sections
            var dashMatch = Regex.Match(text, @"<section id=""dashboard"".*?</section>", RegexOptions.Singleline);
            var prodMatch = Regex.Match(text, @"<section id=""products"".*?</section>", RegexOptions.Singleline);
            var blogMatch = Regex.Match(text, @"<section id=""blog"".*?</section>", RegexOptions.Singleline);
            var repoMatch = Regex.Match(text, @"<section id=""reports"".*?</section>", RegexOptions.Singleline);

            // Replace dashboard stats
            var dashContent = dashMatch.Success ? dashMatch.Groups[0].Value : "";
            dashContent = dashContent.Replace(@"<div class=""number"">0 ₫</div>", @"<div class=""number"">@string.Format(""{0:N0} ₫"", Model.TotalRevenue)</div>");
            dashContent = dashContent.Replace(@"<div class=""number"">0</div><div class=""label"">Người dùng</div>", @"<div class=""number"">@Model.TotalUsers</div><div class=""label"">Người dùng</div>");
            dashContent = dashContent.Replace(@"<div class=""number"">0</div><div class=""label"">Sản phẩm</div>", @"<div class=""number"">@Model.TotalProducts</div><div class=""label"">Sản phẩm</div>");
            dashContent = dashContent.Replace(@"<div class=""number"">0</div><div class=""label"">Đơn hàng</div>", @"<div class=""number"">@Model.TotalOrders</div><div class=""label"">Đơn hàng</div>");

            var prodContent = prodMatch.Success ? prodMatch.Groups[0].Value : "";
            var blogContent = blogMatch.Success ? blogMatch.Groups[0].Value : "";
            var repoContent = repoMatch.Success ? repoMatch.Groups[0].Value : "";

            // Modals
            var modalsMatch = Regex.Match(text, @"(<!-- Modal Thêm sản phẩm -->.+?)(?:<script src=""https://cdn\.jsdelivr\.net/npm/bootstrap|</body>|\Z)", RegexOptions.Singleline);
            var modalsContent = modalsMatch.Success ? modalsMatch.Groups[1].Value : "";

            // Scripts
            var scriptMatch = Regex.Match(text, @"<script src=""https://cdn\.jsdelivr\.net/npm/bootstrap@5\.3\.0.*?</script>\s*<script>(.*?)</script>", RegexOptions.Singleline);
            var scriptsContent = scriptMatch.Success ? scriptMatch.Groups[1].Value : "";

            // Edit scripts navigation
            var navRegex = @"// ===== Sidebar sections =====.*?// ===== Product: auto slug";
            var newNav = @"
// ===== Sidebar sections =====
function showSection(sectionId) {
    document.querySelectorAll('.section-content').forEach(s => s.classList.add('d-none'));
    const section = document.getElementById(sectionId);
    if (section) section.classList.remove('d-none');
    
    document.querySelectorAll('.admin-sidebar .nav-link').forEach(l => l.classList.remove('active'));
    const activeLink = document.querySelector(`.admin-sidebar .nav-link[href='/Admin/Index#${sectionId}']`) 
                    || document.querySelector(`.admin-sidebar .nav-link[href='#${sectionId}']`);
    if (activeLink) activeLink.classList.add('active');
}

if (window.location.hash) {
    const h = window.location.hash.replace('#', '');
    if (h && document.getElementById(h)) {
        showSection(h);
    } else {
        showSection('dashboard');
    }
} else {
    showSection('dashboard');
}

// Lắng nghe sự kiện hashchange
window.addEventListener('hashchange', function() {
    const h = window.location.hash.replace('#', '');
    if (h && document.getElementById(h)) {
        showSection(h);
    }
});

// ===== Product: auto slug";
            
            scriptsContent = Regex.Replace(scriptsContent, navRegex, newNav, RegexOptions.Singleline);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("@model Hagoplant.ViewModels.AdminDashboardVm");
            sb.AppendLine("@using System.Linq");
            sb.AppendLine("@{");
            sb.AppendLine("    Layout = \"~/Views/Shared/_AdminLayout.cshtml\";");
            sb.AppendLine("    ViewData[\"Title\"] = \"Tổng quan hệ thống\";");
            sb.AppendLine("}");
            sb.AppendLine("");
            sb.AppendLine("@section Styles {");
            sb.AppendLine("    <link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/easymde@2.20.0/dist/easymde.min.css\">");
            sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/easymde@2.20.0/dist/easymde.min.js\"></script>");
            sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/marked@4.3.0/marked.min.js\"></script>");
            sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/dompurify@3.3.1/dist/purify.min.js\"></script>");
            sb.AppendLine("    <style>");
            sb.AppendLine(styles);
            sb.AppendLine("    </style>");
            sb.AppendLine("}");
            sb.AppendLine("");
            sb.AppendLine("<!-- dashboard -->");
            sb.AppendLine(dashContent);
            sb.AppendLine("<!-- products -->");
            sb.AppendLine(prodContent);
            sb.AppendLine("<!-- blog -->");
            sb.AppendLine(blogContent);
            sb.AppendLine("<!-- reports -->");
            sb.AppendLine(repoContent);
            sb.AppendLine("");
            sb.AppendLine(modalsContent);
            sb.AppendLine("");
            sb.AppendLine("@section Scripts {");
            sb.AppendLine("    <script>");
            sb.AppendLine(scriptsContent);
            sb.AppendLine("    </script>");
            sb.AppendLine("}");

            File.WriteAllText(@"c:\Hagoplant\Hagoplant\Views\Admin\Index.cshtml", sb.ToString());
            Console.WriteLine("Done rewriting Index.cshtml");
        }
    }
}
