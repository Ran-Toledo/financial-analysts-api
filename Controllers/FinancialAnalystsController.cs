using Microsoft.AspNetCore.Mvc;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinancialAnalystsController : ControllerBase
    {
        private readonly AnalystsDbContext _context;

        public FinancialAnalystsController(AnalystsDbContext context)
        {
            _context = context;
        }

        // GET: api/FinancialAnalysts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FinancialAnalyst>>> GetAnalysts()
        {
            return await _context.FinancialAnalysts.ToListAsync();
        }

        // GET: api/FinancialAnalysts/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<FinancialAnalyst>> GetAnalyst(int id)
        {
            var analyst = await _context.FinancialAnalysts.FindAsync(id);
            if (analyst == null) return NotFound();
            return analyst;
        }

        // POST: api/FinancialAnalysts
        [HttpPost]
        public async Task<ActionResult<FinancialAnalyst>> AddAnalyst(FinancialAnalyst analyst)
        {
            _context.FinancialAnalysts.Add(analyst);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAnalyst), new { id = analyst.Id }, analyst);
        }

        // PUT: api/FinancialAnalysts/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAnalyst(int id, FinancialAnalyst updatedAnalyst)
        {
            if (id != updatedAnalyst.Id) return BadRequest();
            _context.Entry(updatedAnalyst).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/FinancialAnalysts/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAnalyst(int id)
        {
            var analyst = await _context.FinancialAnalysts.FindAsync(id);
            if (analyst == null) return NotFound();
            _context.FinancialAnalysts.Remove(analyst);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
