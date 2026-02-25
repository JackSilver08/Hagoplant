import re

with open(r'c:\Hagoplant\Hagoplant\Views\Admin\Index.bak.cshtml', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Styles
style_match = re.search(r'<style>(.*?)</style>', text, re.DOTALL)
styles = style_match.group(1) if style_match else ""

# 2. Sections
dash_match = re.search(r'<section id="dashboard"[^>]*>.*?</section>', text, re.DOTALL)
prod_match = re.search(r'<section id="products"[^>]*>.*?</section>', text, re.DOTALL)
blog_match = re.search(r'<section id="blog"[^>]*>.*?</section>', text, re.DOTALL)

dash_content = dash_match.group(0) if dash_match else ""
prod_content = prod_match.group(0) if prod_match else ""
blog_content = blog_match.group(0) if blog_match else ""

# Replace dashboard stats
dash_content = dash_content.replace('<div class="number">0 ₫</div>', '<div class="number">@string.Format("{0:N0} ₫", Model.TotalRevenue)</div>')
# The other ones are just 0. We can replace them by context.
dash_content = dash_content.replace('<div class="number">0</div><div class="label">Người dùng</div>', '<div class="number">@Model.TotalUsers</div><div class="label">Người dùng</div>')
dash_content = dash_content.replace('<div class="number">0</div><div class="label">Sản phẩm</div>', '<div class="number">@Model.TotalProducts</div><div class="label">Sản phẩm</div>')
dash_content = dash_content.replace('<div class="number">0</div><div class="label">Đơn hàng</div>', '<div class="number">@Model.TotalOrders</div><div class="label">Đơn hàng</div>')

# 3. Modals
modals = ""
for modal_id in ["addProductModal", "editProductModal", "addBlogModal", "editBlogModal"]:
    # Simplistic extraction: from `<div class="modal fade" id="X"` up to the div closure of the modal.
    # We will just find from `<!-- Modal ...` up to `<script src=`
    pass

# Better approach for modals: from `<!-- Modal Thêm sản phẩm -->` to `<!-- Bootstrap 5 JS -->` or `<script src=`
modals_match = re.search(r'(<!-- Modal Thêm sản phẩm -->.+?)(?:<script src="https://cdn\.jsdelivr\.net/npm/bootstrap@5\.3\.0|\Z)', text, re.DOTALL)
modals = modals_match.group(1) if modals_match else ""

# 4. Scripts
script_match = re.search(r'(<script src="https://cdn\.jsdelivr\.net/npm/bootstrap@5\.3\.0.*?)(?:</body>|\Z)', text, re.DOTALL)
scripts = script_match.group(1) if script_match else ""

# The scripts might contain the old navigation JS. We replace the `// ===== Sidebar sections =====` block
nav_match_regex = r'// ===== Sidebar sections =====.*?// ===== Product: auto slug'
new_nav = '''// ===== Sidebar sections =====
            function showSection(sectionId) {
              $$(".section-content").forEach(s => s.classList.add("d-none"));
              const section = document.getElementById(sectionId);
              if (section) section.classList.remove("d-none");

              $$(".admin-sidebar .nav-link").forEach(l => l.classList.remove("active"));
              const activeLink = $(`.admin-sidebar .nav-link[href="/Admin/Index#${sectionId}"]`) || $(`.admin-sidebar .nav-link[href="#${sectionId}"]`);
              if (activeLink) activeLink.classList.add("active");
            }

            // Check if we should show a section based on hash
            if (window.location.hash) {
                const h = window.location.hash.replace("#", "");
                if (h && document.getElementById(h)) {
                    showSection(h);
                } else {
                    showSection("dashboard");
                }
            } else {
                showSection("dashboard");
            }

            $$(".admin-sidebar .nav-link").forEach(link => {
              link.addEventListener("click", (e) => {
                const href = link.getAttribute("href") || "";
                if (href.includes("#")) {
                    e.preventDefault();
                    const id = href.split("#")[1];
                    if (id && document.getElementById(id)) {
                        showSection(id);
                        history.pushState(null, null, href);
                    } else if (id) {
                        window.location.href = href;
                    }
                }
              });
            });

            // ===== Product: auto slug'''
scripts = re.sub(nav_match_regex, new_nav, scripts, flags=re.DOTALL)

# Header external scripts to keep
head_scripts = """
    <!-- EasyMDE (Markdown Editor) -->
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/easymde@2.20.0/dist/easymde.min.css">
    <script src="https://cdn.jsdelivr.net/npm/easymde@2.20.0/dist/easymde.min.js"></script>

    <!-- Marked: Markdown -> HTML -->
    <script src="https://cdn.jsdelivr.net/npm/marked@4.3.0/marked.min.js"></script>

    <!-- DOMPurify: sanitize HTML để chống XSS -->
    <script src="https://cdn.jsdelivr.net/npm/dompurify@3.3.1/dist/purify.min.js"></script>
"""

# Assemble
out = f"""@model Hagoplant.ViewModels.AdminDashboardVm
@using System.Linq
@{{
    Layout = "~/Views/Shared/_AdminLayout.cshtml";
    ViewData["Title"] = "Tổng quan hệ thống";
}}

@section Styles {{
{head_scripts}
<style>
{styles}
</style>
}}

{dash_content}

{prod_content}

{blog_content}

{modals}

@section Scripts {{
{scripts}
}}
"""

with open(r'c:\Hagoplant\Hagoplant\Views\Admin\Index.cshtml', 'w', encoding='utf-8') as f:
    f.write(out)
