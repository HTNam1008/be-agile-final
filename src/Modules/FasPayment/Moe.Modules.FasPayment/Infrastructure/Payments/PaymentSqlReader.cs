using Microsoft.EntityFrameworkCore;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Payments;

internal static class PaymentSqlReader
{
    public static async Task<OutstandingBillsResponse> ReadOutstandingBillsAsync(
        MoeDbContext dbContext,
        long personId,
        CancellationToken cancellationToken)
    {
        AccountBalanceRow? account = await dbContext.Database
            .SqlQuery<AccountBalanceRow>($"""
                SELECT TOP(1)
                    [EducationAccountId],
                    [CurrentBalance],
                    CAST('SGD' AS nvarchar(10)) AS [CurrencyCode]
                FROM [account].[EducationAccount]
                WHERE [PersonId] = {personId}
                  AND [AccountStatusCode] = 'ACTIVE'
                ORDER BY [EducationAccountId]
                """)
            .SingleOrDefaultAsync(cancellationToken);

        List<OutstandingBillRow> billRows = await dbContext.Database
            .SqlQuery<OutstandingBillRow>($"""
                SELECT
                    bill.[BillId],
                    bill.[BillNumber],
                    bill.[CourseEnrollmentId],
                    course.[CourseId],
                    course.[CourseCode],
                    course.[CourseName],
                    bill.[IssuedAt] AS [IssuedAtUtc],
                    bill.[DueDate],
                    bill.[GrossAmount],
                    bill.[SubsidyAmount],
                    bill.[NetPayableAmount],
                    bill.[PaidAmount],
                    bill.[OutstandingAmount],
                    bill.[BillStatusCode]
                FROM [billing].[Bill] bill
                INNER JOIN [course].[CourseEnrollment] enrollment
                    ON enrollment.[CourseEnrollmentId] = bill.[CourseEnrollmentId]
                INNER JOIN [course].[Course] course
                    ON course.[CourseId] = enrollment.[CourseId]
                WHERE enrollment.[PersonId] = {personId}
                  AND bill.[OutstandingAmount] > 0
                  AND bill.[BillStatusCode] NOT IN ('PAID', 'CANCELLED')
                ORDER BY bill.[DueDate], bill.[IssuedAt]
                """)
            .ToListAsync(cancellationToken);

        List<OutstandingBillLineRow> lineRows = billRows.Count == 0
            ? []
            : await dbContext.Database
                .SqlQuery<OutstandingBillLineRow>($"""
                    SELECT
                        line.[BillLineId],
                        line.[BillId],
                        line.[DescriptionSnapshot] AS [Description],
                        line.[Quantity],
                        line.[UnitAmount],
                        line.[GrossAmount],
                        line.[SubsidyAmount],
                        line.[NetAmount]
                    FROM [billing].[BillLine] line
                    INNER JOIN [billing].[Bill] bill
                        ON bill.[BillId] = line.[BillId]
                    INNER JOIN [course].[CourseEnrollment] enrollment
                        ON enrollment.[CourseEnrollmentId] = bill.[CourseEnrollmentId]
                    WHERE enrollment.[PersonId] = {personId}
                      AND bill.[OutstandingAmount] > 0
                      AND bill.[BillStatusCode] NOT IN ('PAID', 'CANCELLED')
                    ORDER BY line.[BillId], line.[BillLineId]
                    """)
                .ToListAsync(cancellationToken);

        var linesByBill = lineRows
            .GroupBy(x => x.BillId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<OutstandingBillLineDto>)group.Select(x => new OutstandingBillLineDto(
                    x.BillLineId,
                    x.Description,
                    x.Quantity,
                    x.UnitAmount,
                    x.GrossAmount,
                    x.SubsidyAmount,
                    x.NetAmount)).ToArray());

        OutstandingBillDto[] bills = billRows.Select(x => new OutstandingBillDto(
            x.BillId,
            x.BillNumber,
            x.CourseEnrollmentId,
            x.CourseId,
            x.CourseCode,
            x.CourseName,
            x.IssuedAtUtc,
            x.DueDate,
            x.GrossAmount,
            x.SubsidyAmount,
            x.NetPayableAmount,
            x.PaidAmount,
            x.OutstandingAmount,
            x.BillStatusCode,
            linesByBill.GetValueOrDefault(x.BillId, Array.Empty<OutstandingBillLineDto>()))).ToArray();

        return new OutstandingBillsResponse(
            account?.CurrentBalance ?? 0m,
            account?.CurrencyCode ?? "SGD",
            bills);
    }

    private sealed class AccountBalanceRow
    {
        public long EducationAccountId { get; set; }
        [Precision(19, 2)]
        public decimal CurrentBalance { get; set; }
        public string CurrencyCode { get; set; } = "SGD";
    }

    private sealed class OutstandingBillRow
    {
        public long BillId { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public long CourseEnrollmentId { get; set; }
        public long CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime IssuedAtUtc { get; set; }
        public DateOnly DueDate { get; set; }
        [Precision(19, 2)]
        public decimal GrossAmount { get; set; }
        [Precision(19, 2)]
        public decimal SubsidyAmount { get; set; }
        [Precision(19, 2)]
        public decimal NetPayableAmount { get; set; }
        [Precision(19, 2)]
        public decimal PaidAmount { get; set; }
        [Precision(19, 2)]
        public decimal OutstandingAmount { get; set; }
        public string BillStatusCode { get; set; } = string.Empty;
    }

    private sealed class OutstandingBillLineRow
    {
        public long BillLineId { get; set; }
        public long BillId { get; set; }
        public string Description { get; set; } = string.Empty;
        [Precision(19, 4)]
        public decimal Quantity { get; set; }
        [Precision(19, 4)]
        public decimal UnitAmount { get; set; }
        [Precision(19, 2)]
        public decimal GrossAmount { get; set; }
        [Precision(19, 2)]
        public decimal SubsidyAmount { get; set; }
        [Precision(19, 2)]
        public decimal NetAmount { get; set; }
    }
}
