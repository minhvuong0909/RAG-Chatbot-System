using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class SettingsModel : PageModel
    {
        private readonly ISystemSettingService _systemSettingService;

        public SettingsModel(ISystemSettingService systemSettingService)
        {
            _systemSettingService = systemSettingService;
        }

        [BindProperty]
        public SettingsInput Input { get; set; } = new();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Input.ChunkSize = await _systemSettingService.GetChunkSizeAsync();
            Input.ChunkOverlap = await _systemSettingService.GetChunkOverlapAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Input.ChunkOverlap > Input.ChunkSize / 2)
            {
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.ChunkOverlap)}",
                    $"Chunk Overlap không được vượt quá {Input.ChunkSize / 2} (ChunkSize / 2).");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Vui lòng kiểm tra lại thông tin.";
                return Page();
            }

            try
            {
                await _systemSettingService.UpdateSettingsAsync(Input.ChunkSize, Input.ChunkOverlap);
                SuccessMessage = "Cài đặt đã được cập nhật thành công.";
            }
            catch (System.ArgumentOutOfRangeException ex)
            {
                ErrorMessage = ex.Message;
            }

            return Page();
        }

        public class SettingsInput
        {
            [Required]
            [Range(300, 700, ErrorMessage = "Chunk Size phải từ 300 đến 700.")]
            [Display(Name = "Chunk Size")]
            public int ChunkSize { get; set; } = 500;

            [Required]
            [Range(100, 350, ErrorMessage = "Chunk Overlap phải từ 100 đến 350.")]
            [Display(Name = "Chunk Overlap")]
            public int ChunkOverlap { get; set; } = 100;
        }
    }
}
