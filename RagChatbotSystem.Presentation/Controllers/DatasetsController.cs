using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatasetsController : ControllerBase
    {
        private readonly IDatasetService _datasetService;

        public DatasetsController(IDatasetService datasetService)
        {
            _datasetService = datasetService;
        }

        [HttpGet]
        public async Task<IActionResult> GetDatasets([FromQuery] Guid? createdBy, CancellationToken cancellationToken)
        {
            var datasets = await _datasetService.GetDatasetsAsync(createdBy, cancellationToken);
            return Ok(datasets);
        }

        [HttpGet("{datasetId:guid}")]
        public async Task<IActionResult> GetDataset(Guid datasetId, CancellationToken cancellationToken)
        {
            var dataset = await _datasetService.GetDatasetAsync(datasetId, cancellationToken);
            return dataset == null ? NotFound() : Ok(dataset);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDataset([FromBody] CreateDatasetRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var dataset = await _datasetService.CreateDatasetAsync(request, cancellationToken);
                return CreatedAtAction(nameof(GetDataset), new { datasetId = dataset.DatasetId }, dataset);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("{datasetId:guid}")]
        public async Task<IActionResult> DeleteDataset(Guid datasetId, CancellationToken cancellationToken)
        {
            var deleted = await _datasetService.DeleteDatasetAsync(datasetId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}
