using Microsoft.AspNetCore.Mvc;
using TaskScheduler.Application.Dtos;
using TaskScheduler.Application.Services;

namespace TaskScheduler.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(ScheduledTaskService taskService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaskResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TaskResponse>> GetTasks(CancellationToken cancellationToken)
    {
        var items = taskService.GetAll(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<TaskResponse> CreateTask(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var created = taskService.Create(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Activate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            taskService.Activate(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            taskService.Delete(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
