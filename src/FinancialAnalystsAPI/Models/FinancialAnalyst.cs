// Models/FinancialAnalyst.cs
namespace FinancialAnalystsAPI.Models
{
    public class FinancialAnalyst
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Expertise { get; set; } = string.Empty;
        public double Rating { get; set; }
    }
}
