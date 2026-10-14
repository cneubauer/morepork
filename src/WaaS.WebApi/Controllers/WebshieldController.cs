using WaaS.Webshield.DesiredState;
using WaaS.Webshield.ViewModel;

namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/stack-instances/{stackInstanceId}/webshield/mappings")]
public class WebshieldController(
    WaasService<WebshieldData> waasService
) : ControllerBase
{
    /// <summary>
    /// Read Webshield mappings
    /// </summary>
    /// <remarks>
    /// Retrieves the Desired State of the Webshield.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <returns>The Webshield mappings</returns>
    [HttpGet]
    public async Task<IActionResult> ReadWebshieldMappings(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId
    )
    {
        var desiredState = await waasService.ReadDesiredState(tenant, stackInstanceId, 0);

        if (desiredState is null)
            return NotFound();

        var mappings = desiredState.Data.Mappings.Select(x => new WebshieldMapping()
        {
            Id = x.Domain,
            Destination = x.Destination,
            IsEnabled = x.IsEnabled,
            // TODO: Add other properties as needed
        });

        return Ok(mappings);
    }

    /// <summary>
    /// Update Webshield mappings
    /// </summary>
    /// <remarks>
    /// Updates the Desired State of the Webshield.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <param name="mappings">The Webshield mappings to update.</param>
    /// <param name="transactionId" example="waas-update-3fa85f64-5717-4562-b3fc-2c963f66afa6">The transaction identifier. Defaults to a generated value when omitted.</param>
    /// <returns>The updated Webshield mappings</returns>
    [HttpPut]
    public async Task<IActionResult> UpdateWebshieldMappings(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId,
        [FromBody] IEnumerable<WebshieldMapping> mappings,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    )
    {
        var waasContext = await waasService.ReadWaasContext(tenant, stackInstanceId, transactionId ?? $"waas-update-{Guid.NewGuid()}");

        if (waasContext is null)
            return NotFound();

        var context = await waasService.Upsert(
            waasContext, 0, [], 
            x => x.Apply(mappings),
            HttpContext.RequestAborted
        );

        if (context is null)
            return NotFound();

        // TODO: Implement Webshield mapping view model
        var updatedMappings = context.DesiredState.Data.Mappings
            .Select(x => new WebshieldMapping()
            {
                Id = x.Domain,
                Destination = x.Destination,
                IsEnabled = x.IsEnabled,
                // TODO: Add other properties as needed
            });

        return Ok(updatedMappings);
    }
}