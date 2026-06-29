using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class PermissionsModel : PageModel
    {
        private readonly IDatasetService _datasetService;
        private readonly IUserService _userService;

        public PermissionsModel(IDatasetService datasetService, IUserService userService)
        {
            _datasetService = datasetService;
            _userService = userService;
        }

        public DatasetDto Dataset { get; set; } = null!;
        public IReadOnlyList<UserDto> PermittedUsers { get; set; } = new List<UserDto>();
        public IReadOnlyList<UserDto> AvailableUsers { get; set; } = new List<UserDto>();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var dataset = await _datasetService.GetDatasetAsync(id);
            if (dataset == null)
            {
                return NotFound();
            }

            Dataset = dataset;
            PermittedUsers = await _datasetService.GetPermittedUsersAsync(id);
            
            var allUsers = await _userService.GetUsersAsync();
            AvailableUsers = allUsers
                .Where(u => u.Role != "Admin" && u.UserId != dataset.CreatedBy && !PermittedUsers.Any(pu => pu.UserId == u.UserId))
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostGrantPermissionAsync(Guid datasetId, Guid userId)
        {
            await _datasetService.GrantPermissionAsync(datasetId, userId);
            return RedirectToPage(new { id = datasetId });
        }

        public async Task<IActionResult> OnPostRevokePermissionAsync(Guid datasetId, Guid userId)
        {
            await _datasetService.RevokePermissionAsync(datasetId, userId);
            return RedirectToPage(new { id = datasetId });
        }
    }
}
