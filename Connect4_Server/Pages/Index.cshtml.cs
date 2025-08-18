using Microsoft.AspNetCore.Mvc.RazorPages;
// using Microsoft.AspNetCore.Mvc; // Not used here; removed to keep usings minimal

namespace Connect4_Server.Pages
{
    // Index page model: simple landing page, no special logic on GET.
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        // GET: / (landing page)
        public void OnGet()
        {
            // Intentionally empty – page is static.
            // TempData["StatusMessage"] may be set by other pages and is rendered in the view.
        }
    }
}
