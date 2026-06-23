using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moe.Modules.FasPayment.Application;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public class FasCriteriaEvaluatorTests
{
    private readonly FasCriteriaEvaluator _evaluator = new FasCriteriaEvaluator();

    [Fact]
    public void Evaluate_AgeInRange_ShouldReturnTrue()
    {
        var criteria = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "AGE", 13, 18, null, 1, DateTime.UtcNow, 1)
        };
        var lookup = Enumerable.Empty<string>().ToLookup(x => 1L);

        bool resultMid = _evaluator.Evaluate(criteria, lookup, age: 15, gdpValue: null, pciValue: null, nationality: null);
        bool resultLeft = _evaluator.Evaluate(criteria, lookup, age: 13, gdpValue: null, pciValue: null, nationality: null);
        bool resultRight = _evaluator.Evaluate(criteria, lookup, age: 18, gdpValue: null, pciValue: null, nationality: null);

        resultMid.Should().BeTrue();
        resultLeft.Should().BeTrue();
        resultRight.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_AgeOutRange_ShouldReturnFalse()
    {
        var criteria = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "AGE", 13, 18, null, 1, DateTime.UtcNow, 1)
        };
        var lookup = Enumerable.Empty<string>().ToLookup(x => 1L);

        bool result = _evaluator.Evaluate(criteria, lookup, age: 12, gdpValue: null, pciValue: null, nationality: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_GdpInclusiveBounds_ShouldReturnTrue_AndFalseWhenOutside()
    {
        var criteria = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "GDP", 0, 3000, null, 1, DateTime.UtcNow, 1)
        };
        var lookup = Enumerable.Empty<string>().ToLookup(x => 1L);

        bool result1 = _evaluator.Evaluate(criteria, lookup, age: null, gdpValue: 0, pciValue: null, nationality: null);
        bool result2 = _evaluator.Evaluate(criteria, lookup, age: null, gdpValue: 3000, pciValue: null, nationality: null);
        bool result3 = _evaluator.Evaluate(criteria, lookup, age: null, gdpValue: 3000.01M, pciValue: null, nationality: null);

        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NationalityInList_ShouldReturnTrue_NotInListFalse()
    {
        var criteria = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "NATIONALITY", null, null, null, 1, DateTime.UtcNow, 1)
        };
        var lookup = new[] { new { Id = 1L, Nat = "Singapore Citizen" }, new { Id = 1L, Nat = "PR" } }
            .ToLookup(x => x.Id, x => x.Nat);

        bool result1 = _evaluator.Evaluate(criteria, lookup, age: null, gdpValue: null, pciValue: null, nationality: "Singapore Citizen");
        bool result2 = _evaluator.Evaluate(criteria, lookup, age: null, gdpValue: null, pciValue: null, nationality: "International Student");

        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MixedAndOr_LeftToRight_ShouldEvaluateCorrectly()
    {
        // [AGE AND NATIONALITY]
        var criteriaAnd = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "AGE", 13, 18, "AND", 1, DateTime.UtcNow, 1),
            FasTierCriteria.Create(1, "NATIONALITY", null, null, null, 2, DateTime.UtcNow, 2)
        };
        var lookupAnd = new[] { new { Id = 2L, Nat = "Singapore Citizen" } }.ToLookup(x => x.Id, x => x.Nat);

        bool resultAnd = _evaluator.Evaluate(criteriaAnd, lookupAnd, age: 15, gdpValue: null, pciValue: null, nationality: "Foreigner");
        resultAnd.Should().BeFalse(); // Age passes, Nat fails -> false

        // [AGE OR NATIONALITY]
        var criteriaOr = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "AGE", 13, 18, "OR", 1, DateTime.UtcNow, 1),
            FasTierCriteria.Create(1, "NATIONALITY", null, null, null, 2, DateTime.UtcNow, 2)
        };
        var lookupOr = new[] { new { Id = 2L, Nat = "Singapore Citizen" } }.ToLookup(x => x.Id, x => x.Nat);

        bool resultOr = _evaluator.Evaluate(criteriaOr, lookupOr, age: 12, gdpValue: null, pciValue: null, nationality: "Singapore Citizen");
        resultOr.Should().BeTrue(); // Age fails, Nat passes -> true

        // [A OR B AND C] left to right = ((A OR B) AND C)
        var criteriaComplex = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "AGE", 13, 18, "OR", 1, DateTime.UtcNow, 1),
            FasTierCriteria.Create(1, "GDP", 0, 3000, "AND", 2, DateTime.UtcNow, 2),
            FasTierCriteria.Create(1, "NATIONALITY", null, null, null, 3, DateTime.UtcNow, 3)
        };
        var lookupComplex = new[] { new { Id = 3L, Nat = "Singapore Citizen" } }.ToLookup(x => x.Id, x => x.Nat);

        // A fails, B passes -> (F or T) = T. C passes -> T and T = True
        bool res1 = _evaluator.Evaluate(criteriaComplex, lookupComplex, age: 12, gdpValue: 1500, pciValue: null, nationality: "Singapore Citizen");
        res1.Should().BeTrue();

        // A passes, B fails -> (T or F) = T. C fails -> T and F = False
        bool res2 = _evaluator.Evaluate(criteriaComplex, lookupComplex, age: 15, gdpValue: 5000, pciValue: null, nationality: "Foreigner");
        res2.Should().BeFalse();
    }
}
