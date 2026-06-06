using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DatasetsController : Controller
    {
        private readonly IDatasetService _datasetService;

        public DatasetsController(IDatasetService datasetService)
        {
            _datasetService = datasetService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, string? description, bool isPublic)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["AdminError"] = "Subject name is required.";
                return RedirectToAction("Index", "Admin");
            }

            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            try
            {
                var request = new CreateDatasetRequest(name, description, currentUserId, isPublic);
                var dataset = await _datasetService.CreateDatasetAsync(request);
                TempData["AdminSuccess"] = $"Subject '{dataset.Name}' created successfully.";
            }
            catch (Exception ex)
            {
                TempData["AdminError"] = ex.Message;
            }

            return RedirectToAction("Index", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, string name, string? description, bool isPublic)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["AdminError"] = "Subject name is required.";
                return RedirectToAction("Index", "Admin");
            }

            try
            {
                var updated = await _datasetService.UpdateDatasetAsync(id, name, description, isPublic);
                TempData[updated ? "AdminSuccess" : "AdminError"] = updated
                    ? "Subject updated successfully."
                    : "Subject was not found.";
            }
            catch (Exception ex)
            {
                TempData["AdminError"] = ex.Message;
            }

            return RedirectToAction("Index", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var deleted = await _datasetService.DeleteDatasetAsync(id);
                TempData[deleted ? "AdminSuccess" : "AdminError"] = deleted
                    ? "Subject deleted successfully."
                    : "Subject was not found.";
            }
            catch (Exception ex)
            {
                TempData["AdminError"] = ex.Message;
            }

            return RedirectToAction("Index", "Admin");
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdString, out userId);
        }
    }
}
