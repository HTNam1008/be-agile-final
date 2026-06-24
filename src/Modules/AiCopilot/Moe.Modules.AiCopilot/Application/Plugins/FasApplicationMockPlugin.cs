using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Moe.Modules.AiCopilot.Application.Plugins;

public class FasApplicationMockPlugin
{
    [KernelFunction, Description("Evaluates FAS eligibility based on provided household income, members, and other criteria.")]
    public string EvaluateFasEligibilityMock(
        [Description("Monthly household income in SGD")] decimal income,
        [Description("Number of household members")] int members,
        [Description("Whether the applicant is a resident of a welfare home")] bool isWelfareResident)
    {
        if (isWelfareResident)
        {
            return "Eligible for 100% subsidy (Welfare Home Tier).";
        }

        if (members <= 0) return "Need more information about household members.";

        var pci = income / members;
        if (pci <= 1000)
        {
            return $"Eligible for 100% subsidy. PCI is ${pci}.";
        }
        else if (pci <= 2000)
        {
            return $"Eligible for 50% subsidy. PCI is ${pci}.";
        }

        return $"Not eligible for standard FAS. PCI is ${pci}, which exceeds the $2000 threshold.";
    }
}
