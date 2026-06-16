using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers;

[Route("api/form-schemas")]
[ApiController]
[AllowAnonymous]
public class FormSchemasController : ControllerBase
{
    private readonly IFormSchemaService _service;

    public FormSchemasController(IFormSchemaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _service.ListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var dto = await _service.GetAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFormSchemaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");

        var dto = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateFormSchemaRequest request)
    {
        var dto = await _service.UpdateAsync(id, request);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
