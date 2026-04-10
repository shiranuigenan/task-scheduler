using Microsoft.AspNetCore.Mvc;
using TaskScheduler.Application.Jobs;

namespace TaskScheduler.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(JobFactory jobFactory) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetJobs()
    {
        var names = jobFactory.GetRegisteredJobNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return Ok(names);
    }
}
