namespace Moe.Modules.AiCopilot.Infrastructure.RAG;

public class MockDocumentChunk
{
    public string DocName { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class MockDocumentIngestor
{
    public static List<MockDocumentChunk> GetChunks()
    {
        return
        [
            new MockDocumentChunk
            {
                DocName = "Mock_Business_Requirement.md",
                Section = "FAS Application Policy",
                Content = "A student must provide household income, number of household members, and parent nationalities to apply for FAS. The system computes PCI (Per Capita Income) as Monthly Household Income divided by Household Members. Welfare home residents do not need to provide income or employment status."
            },
            new MockDocumentChunk
            {
                DocName = "Mock_Business_Requirement.md",
                Section = "Payment and Refund Policy",
                Content = "Outstanding charges must be resolved before proceeding with new enrolments. The Education Account balance can be used to pay course fees. If the account balance is insufficient, external payment (e.g., credit card) is required. Refunds for cancelled enrollments are eligible only if requested within 14 days of enrollment. The refund will be credited back to the original payment method within 7 working days."
            },
            new MockDocumentChunk
            {
                DocName = "Mock_Business_Requirement.md",
                Section = "Withdrawal Policy",
                Content = "Funds can only be withdrawn from the Education Account upon graduation or when leaving the school system. Withdrawals take up to 14 days to process. There is a withdrawal limit of $500 per month unless approved by an administrator."
            }
        ];
    }
}
