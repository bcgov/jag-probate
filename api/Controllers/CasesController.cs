using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Probate.Db.Models;

namespace Probate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CasesController : ControllerBase
    {
        private readonly ProbateDbContext _dbContext;
        private readonly ILogger<CasesController> _logger;

        public CasesController(ProbateDbContext dbContext, ILogger<CasesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Case>>> GetAll()
        {
            _logger.LogInformation("Fetching all cases");
            var cases = await _dbContext.Cases.ToListAsync();
            return Ok(cases);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Case>> GetById(int id)
        {
            _logger.LogInformation("Fetching case with ID: {CaseId}", id);
            var caseItem = await _dbContext.Cases.FindAsync(id);

            if (caseItem == null)
            {
                return NotFound(new { message = $"Case with ID {id} not found" });
            }

            return Ok(caseItem);
        }

        [HttpPost]
        public async Task<ActionResult<Case>> Create([FromBody] Case newCase)
        {
            _logger.LogInformation("Creating new case: {CaseNumber}", newCase.CaseNumber);

            _dbContext.Cases.Add(newCase);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = newCase.Id }, newCase);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Case>> Update(int id, [FromBody] Case updatedCase)
        {
            if (id != updatedCase.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            _dbContext.Entry(updatedCase).State = EntityState.Modified;

            try
            {
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Updated case with ID: {CaseId}", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _dbContext.Cases.AnyAsync(c => c.Id == id))
                {
                    return NotFound(new { message = $"Case with ID {id} not found" });
                }
                throw;
            }

            return Ok(updatedCase);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var caseItem = await _dbContext.Cases.FindAsync(id);
            if (caseItem == null)
            {
                return NotFound(new { message = $"Case with ID {id} not found" });
            }

            _dbContext.Cases.Remove(caseItem);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted case with ID: {CaseId}", id);
            return NoContent();
        }
    }
}
