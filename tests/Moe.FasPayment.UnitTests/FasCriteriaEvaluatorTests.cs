using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Moe.Modules.FasPayment.Application;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public class FasCriteriaEvaluatorTests
{
    private readonly FasCriteriaEvaluator _evaluator = new FasCriteriaEvaluator();

    private static FasTierCriteria CreateCriteria(long id, string type, decimal? from, decimal? to, string? connector, int displayOrder, long groupId = 0)
    {
        var criteria = FasTierCriteria.Create(1, groupId, type, from, to, connector, displayOrder, DateTime.UtcNow);
        var property = typeof(FasTierCriteria).BaseType?.GetProperty("Id");
        property?.SetValue(criteria, id);
        return criteria;
    }

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
    public void Evaluate_MixedAndOr_ShouldTreatAndInsideGroupsAndOrBetweenGroups()
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

        // [A] OR [B AND C]
        var criteriaComplex = new List<FasTierCriteria>
        {
            FasTierCriteria.Create(1, "AGE", 13, 18, "OR", 1, DateTime.UtcNow, 1),
            FasTierCriteria.Create(1, "GDP", 0, 3000, "AND", 2, DateTime.UtcNow, 2),
            FasTierCriteria.Create(1, "NATIONALITY", null, null, null, 3, DateTime.UtcNow, 3)
        };
        var lookupComplex = new[] { new { Id = 3L, Nat = "Singapore Citizen" } }.ToLookup(x => x.Id, x => x.Nat);

        // A fails, B passes, C passes -> [F] OR [T AND T] = True
        bool res1 = _evaluator.Evaluate(criteriaComplex, lookupComplex, age: 12, gdpValue: 1500, pciValue: null, nationality: "Singapore Citizen");
        res1.Should().BeTrue();

        // A passes, so the first group is enough even when the second group fails.
        bool res2 = _evaluator.Evaluate(criteriaComplex, lookupComplex, age: 15, gdpValue: 5000, pciValue: null, nationality: "Foreigner");
        res2.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_GroupedNationalityOrParentNationalityWithSharedPci_ShouldMatchExpectedAlternative()
    {
        var criteria = new List<FasTierCriteria>
        {
            CreateCriteria(1, "NATIONALITY", null, null, "AND", 1, groupId: 10),
            CreateCriteria(2, "PCI", 0, 1000, "OR", 2, groupId: 10),
            CreateCriteria(3, "PARENT_NATIONALITY", null, null, "AND", 3, groupId: 20),
            CreateCriteria(4, "PCI", 0, 1000, null, 4, groupId: 20)
        };
        var lookup = new[]
        {
            new { Id = 1L, Value = "Singapore Citizen" },
            new { Id = 3L, Value = "Permanent Resident" }
        }.ToLookup(x => x.Id, x => x.Value);

        bool studentNationalityPath = _evaluator.Evaluate(
            criteria,
            lookup,
            age: null,
            gdpValue: null,
            pciValue: 900,
            nationality: "Singapore Citizen",
            parentNationalities: ["Foreigner"]);
        bool parentNationalityPath = _evaluator.Evaluate(
            criteria,
            lookup,
            age: null,
            gdpValue: null,
            pciValue: 900,
            nationality: "Foreigner",
            parentNationalities: ["Permanent Resident"]);
        bool noPath = _evaluator.Evaluate(
            criteria,
            lookup,
            age: null,
            gdpValue: null,
            pciValue: 1200,
            nationality: "Singapore Citizen",
            parentNationalities: ["Permanent Resident"]);

        studentNationalityPath.Should().BeTrue();
        parentNationalityPath.Should().BeTrue();
        noPath.Should().BeFalse();
    }
}
