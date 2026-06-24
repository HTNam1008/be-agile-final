using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Moe.Modules.AiCopilot.Application.Plugins;

public class StudentFinanceMockPlugin
{
    [KernelFunction, Description("Gets a snapshot of the student's finance details including balances and outstanding charges.")]
    public string GetStudentFinanceSnapshotMock(
        [Description("The student's ID or NRIC")] string studentId)
    {
        return $$"""
        {
            "studentId": "{{studentId}}",
            "educationAccountBalance": 1250.00,
            "outstandingCharges": [
                {
                    "type": "Course Fee",
                    "courseName": "Advanced Mathematics",
                    "amount": 200.00,
                    "dueDate": "2026-07-01"
                }
            ],
            "totalOutstanding": 200.00
        }
        """;
    }

    [KernelFunction, Description("Explains the breakdown of a specific bill for a student.")]
    public string ExplainBillMock(
        [Description("The student's ID or NRIC")] string studentId,
        [Description("The course ID or name")] string courseId)
    {
        return $$"""
        {
            "studentId": "{{studentId}}",
            "courseId": "{{courseId}}",
            "baseFee": 500.00,
            "subsidiesApplied": [
                {
                    "name": "MOE FAS Tier 1",
                    "amount": 300.00
                }
            ],
            "netPayable": 200.00
        }
        """;
    }
}
