using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RagChatbotSystem.Presentation.Pages.Account
{
    public class RegisterModel : PageModel
    {
        public IActionResult OnGet()
        {
            TempData["ErrorMessage"] = "Tai khoan Student va Teacher se do Admin cap. Vui long lien he Admin neu ban can tai khoan.";
            return RedirectToPage("/Account/Login");
        }

        public IActionResult OnPost()
        {
            TempData["ErrorMessage"] = "Public registration is disabled. Accounts are provisioned by Admin.";
            return RedirectToPage("/Account/Login");
        }
    }
}
