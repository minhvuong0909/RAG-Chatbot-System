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
            Input.DailyTokenLimit = await _systemSettingService.GetDailyTokenLimitAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Input.ChunkOverlap > Input.ChunkSize / 2)
            {
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.ChunkOverlap)}",
                    $"Chunk Overlap must not exceed {Input.ChunkSize / 2} (ChunkSize / 2).");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Vui lòng kiểm tra và sửa các trường chưa hợp lệ.";
                return Page();
            }

            try
            {
                await _systemSettingService.UpdateSettingsAsync(Input.ChunkSize, Input.ChunkOverlap, Input.DailyTokenLimit);
                SuccessMessage = "Đã cập nhật cài đặt hệ thống.";
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
            [Range(300, 700, ErrorMessage = "Kích thước chunk phải từ 300 đến 700.")]
            [Display(Name = "Chunk Size")]
            public int ChunkSize { get; set; } = 500;

            [Required]
            [Range(100, 350, ErrorMessage = "Độ chồng lấn chunk phải từ 100 đến 350.")]
            [Display(Name = "Chunk Overlap")]
            public int ChunkOverlap { get; set; } = 100;

            [Required]
            [Range(1000, 10000000, ErrorMessage = "Giới hạn token hằng ngày phải từ 1.000 đến 10.000.000.")]
            [Display(Name = "Daily Token Limit")]
            public int DailyTokenLimit { get; set; } = 50000;
        }
    }
}
