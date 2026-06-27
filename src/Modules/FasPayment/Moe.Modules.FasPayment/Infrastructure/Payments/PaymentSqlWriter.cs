using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Payments;

internal static class PaymentSqlWriter
{
    private const string EducationAccountMethod = "EDUCATION_ACCOUNT";
    private const string OnlineTenderMethod = "ONLINE_TENDER";

    public static async Task<Result<PayBillResponse>> PayBillAsync(
        MoeDbContext dbContext,
        long personId,
        long? userAccountId,
        PayBillRequest request,
        CancellationToken cancellationToken)
    {
        string paymentMethod = NormalizePaymentMethod(request.PaymentMethodCode);
        if (paymentMethod is not EducationAccountMethod and not OnlineTenderMethod)
        {
            return Result<PayBillResponse>.Failure(PaymentDomainErrors.InvalidPaymentMethod);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            DbConnection connection = dbContext.Database.GetDbConnection();
            DbTransaction dbTransaction = transaction.GetDbTransaction();

            PayableBillRow? bill = await ReadPayableBillAsync(
                connection,
                dbTransaction,
                request.BillId,
                personId,
                cancellationToken);

            if (bill is null)
            {
                return Result<PayBillResponse>.Failure(PaymentDomainErrors.BillNotFound);
            }

            if (bill.OutstandingAmount <= 0m || bill.BillStatusCode is "PAID" or "CANCELLED")
            {
                return Result<PayBillResponse>.Failure(PaymentDomainErrors.BillAlreadySettled);
            }

            AccountRow? account = paymentMethod == EducationAccountMethod
                ? await ReadEducationAccountAsync(connection, dbTransaction, personId, cancellationToken)
                : null;

            if (paymentMethod == EducationAccountMethod)
            {
                if (account is null)
                {
                    return Result<PayBillResponse>.Failure(PaymentDomainErrors.AccountNotFound);
                }

                if (account.CurrentBalance < bill.OutstandingAmount)
                {
                    return Result<PayBillResponse>.Failure(PaymentDomainErrors.InsufficientBalance);
                }
            }

            DateTime utcNow = DateTime.UtcNow;
            decimal paymentAmount = bill.OutstandingAmount;
            string paymentNumber = CreatePaymentNumber(utcNow);
            string receiptNumber = CreateReceiptNumber(utcNow);
            string idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? $"PAY:{bill.BillId}:{Guid.NewGuid():N}"
                : request.IdempotencyKey.Trim();

            long paymentId = await ExecuteScalarAsync<long>(
                connection,
                dbTransaction,
                """
                INSERT INTO [payment].[Payment]
                    ([BillId], [PayerPersonId], [PaymentNumber], [PaymentAmount], [SuccessfulAmount],
                     [PaymentStatusCode], [ReceiptNumber], [IdempotencyKey], [InitiatedAt], [CompletedAt])
                OUTPUT INSERTED.[PaymentId]
                VALUES
                    (@BillId, @PayerPersonId, @PaymentNumber, @PaymentAmount, @SuccessfulAmount,
                     'SUCCESSFUL', @ReceiptNumber, @IdempotencyKey, @InitiatedAt, @CompletedAt);
                """,
                cancellationToken,
                Param("@BillId", bill.BillId),
                Param("@PayerPersonId", personId),
                Param("@PaymentNumber", paymentNumber),
                Param("@PaymentAmount", paymentAmount),
                Param("@SuccessfulAmount", paymentAmount),
                Param("@ReceiptNumber", receiptNumber),
                Param("@IdempotencyKey", idempotencyKey),
                Param("@InitiatedAt", utcNow),
                Param("@CompletedAt", utcNow));

            long paymentPartId = await ExecuteScalarAsync<long>(
                connection,
                dbTransaction,
                """
                INSERT INTO [payment].[PaymentPart]
                    ([PaymentId], [SequenceNumber], [PaymentMethodCode], [EducationAccountId],
                     [AccountTransactionId], [PartAmount], [PartStatusCode], [ProviderCode],
                     [ProviderReference], [AuthorizedAt], [SettledAt], [FailureReason])
                OUTPUT INSERTED.[PaymentPartId]
                VALUES
                    (@PaymentId, 1, @PaymentMethodCode, @EducationAccountId,
                     NULL, @PartAmount, 'SETTLED', @ProviderCode,
                     @ProviderReference, @AuthorizedAt, @SettledAt, NULL);
                """,
                cancellationToken,
                Param("@PaymentId", paymentId),
                Param("@PaymentMethodCode", paymentMethod),
                Param("@EducationAccountId", (object?)account?.EducationAccountId ?? DBNull.Value),
                Param("@PartAmount", paymentAmount),
                Param("@ProviderCode", paymentMethod == OnlineTenderMethod ? "LOCAL_ONLINE" : "EDUCATION_ACCOUNT"),
                Param("@ProviderReference", paymentMethod == OnlineTenderMethod ? $"LOCAL-{paymentNumber}" : $"EA-{paymentPartIdPlaceholder(paymentId)}"),
                Param("@AuthorizedAt", utcNow),
                Param("@SettledAt", utcNow));

            decimal? balanceAfter = null;
            if (paymentMethod == EducationAccountMethod && account is not null)
            {
                balanceAfter = account.CurrentBalance - paymentAmount;
                long accountTransactionId = await ExecuteScalarAsync<long>(
                    connection,
                    dbTransaction,
                    """
                    INSERT INTO [account].[AccountTransaction]
                        ([EducationAccountId], [TransactionTypeCode], [Amount], [TransactionAt],
                         [ReferenceTypeCode], [ReferenceId], [IdempotencyKey], [ReversalOfTransactionId],
                         [BalanceAfter], [Description], [CreatedByLoginAccountId])
                    OUTPUT INSERTED.[AccountTransactionId]
                    VALUES
                        (@EducationAccountId, 'DEBIT', @Amount, @TransactionAt,
                         'PAYMENT_PART', @ReferenceId, @IdempotencyKey, NULL,
                         @BalanceAfter, @Description, @CreatedByLoginAccountId);
                    """,
                    cancellationToken,
                    Param("@EducationAccountId", account.EducationAccountId),
                    Param("@Amount", -paymentAmount),
                    Param("@TransactionAt", utcNow),
                    Param("@ReferenceId", paymentPartId),
                    Param("@IdempotencyKey", $"PAYMENT-PART:{paymentPartId}"),
                    Param("@BalanceAfter", balanceAfter.Value),
                    Param("@Description", $"Payment for {bill.BillNumber}"),
                    Param("@CreatedByLoginAccountId", (object?)userAccountId ?? DBNull.Value));

                await ExecuteNonQueryAsync(
                    connection,
                    dbTransaction,
                    """
                    UPDATE [payment].[PaymentPart]
                    SET [AccountTransactionId] = @AccountTransactionId,
                        [ProviderReference] = @ProviderReference
                    WHERE [PaymentPartId] = @PaymentPartId;

                    UPDATE [account].[EducationAccount]
                    SET [CurrentBalance] = @BalanceAfter
                    WHERE [EducationAccountId] = @EducationAccountId;
                    """,
                    cancellationToken,
                    Param("@AccountTransactionId", accountTransactionId),
                    Param("@ProviderReference", $"EATX-{accountTransactionId}"),
                    Param("@PaymentPartId", paymentPartId),
                    Param("@BalanceAfter", balanceAfter.Value),
                    Param("@EducationAccountId", account.EducationAccountId));
            }

            await ExecuteNonQueryAsync(
                connection,
                dbTransaction,
                """
                UPDATE [billing].[Bill]
                SET [PaidAmount] = [PaidAmount] + @PaymentAmount,
                    [OutstandingAmount] = CASE
                        WHEN [OutstandingAmount] - @PaymentAmount < 0 THEN 0
                        ELSE [OutstandingAmount] - @PaymentAmount
                    END,
                    [BillStatusCode] = CASE
                        WHEN [OutstandingAmount] - @PaymentAmount <= 0 THEN 'PAID'
                        ELSE [BillStatusCode]
                    END
                WHERE [BillId] = @BillId;

                UPDATE enrollment
                SET [EnrollmentStatusCode] = 'COMPLETED'
                FROM [course].[CourseEnrollment] enrollment
                INNER JOIN [billing].[Bill] bill
                    ON bill.[CourseEnrollmentId] = enrollment.[CourseEnrollmentId]
                WHERE bill.[BillId] = @BillId
                  AND bill.[OutstandingAmount] = 0;
                """,
                cancellationToken,
                Param("@PaymentAmount", paymentAmount),
                Param("@BillId", bill.BillId));

            BillPaymentStateRow state = await ReadPaymentStateAsync(
                connection,
                dbTransaction,
                bill.BillId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PayBillResponse>.Success(new PayBillResponse(
                paymentId,
                paymentPartId,
                bill.BillId,
                bill.BillNumber,
                paymentNumber,
                receiptNumber,
                paymentMethod,
                paymentAmount,
                "SUCCESSFUL",
                state.OutstandingAmount,
                state.BillStatusCode,
                balanceAfter));
        });
    }

    private static string NormalizePaymentMethod(string? value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "EDUCATION_ACCOUNT" or "ACCOUNT" or "EA" => EducationAccountMethod,
            "ONLINE_TENDER" or "ONLINE" or "CARD" => OnlineTenderMethod,
            _ => string.Empty
        };

    private static string CreatePaymentNumber(DateTime utcNow)
        => $"PAY-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();

    private static string CreateReceiptNumber(DateTime utcNow)
        => $"RCT-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();

    private static string paymentPartIdPlaceholder(long paymentId) => $"PAY-{paymentId}";

    private static async Task<PayableBillRow?> ReadPayableBillAsync(
        DbConnection connection,
        DbTransaction transaction,
        long billId,
        long personId,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(
            connection,
            transaction,
            """
            SELECT TOP(1)
                bill.[BillId],
                bill.[BillNumber],
                bill.[OutstandingAmount],
                bill.[BillStatusCode]
            FROM [billing].[Bill] bill
            INNER JOIN [course].[CourseEnrollment] enrollment
                ON enrollment.[CourseEnrollmentId] = bill.[CourseEnrollmentId]
            WHERE bill.[BillId] = @BillId
              AND enrollment.[PersonId] = @PersonId;
            """,
            Param("@BillId", billId),
            Param("@PersonId", personId));

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PayableBillRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetString(3));
    }

    private static async Task<AccountRow?> ReadEducationAccountAsync(
        DbConnection connection,
        DbTransaction transaction,
        long personId,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(
            connection,
            transaction,
            """
            SELECT TOP(1)
                [EducationAccountId],
                [CurrentBalance]
            FROM [account].[EducationAccount]
            WHERE [PersonId] = @PersonId
              AND [AccountStatusCode] = 'ACTIVE'
            ORDER BY [EducationAccountId];
            """,
            Param("@PersonId", personId));

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AccountRow(reader.GetInt64(0), reader.GetDecimal(1));
    }

    private static async Task<BillPaymentStateRow> ReadPaymentStateAsync(
        DbConnection connection,
        DbTransaction transaction,
        long billId,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(
            connection,
            transaction,
            """
            SELECT [OutstandingAmount], [BillStatusCode]
            FROM [billing].[Bill]
            WHERE [BillId] = @BillId;
            """,
            Param("@BillId", billId));

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new BillPaymentStateRow(reader.GetDecimal(0), reader.GetString(1));
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params SqlParameterValue[] parameters)
    {
        await using DbCommand command = CreateCommand(connection, transaction, sql, parameters);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params SqlParameterValue[] parameters)
    {
        await using DbCommand command = CreateCommand(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params SqlParameterValue[] parameters)
    {
        DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        foreach (SqlParameterValue parameterValue in parameters)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = parameterValue.Name;
            parameter.Value = parameterValue.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static SqlParameterValue Param(string name, object? value)
        => new(name, value);

    private sealed record SqlParameterValue(string Name, object? Value);

    private sealed record PayableBillRow(
        long BillId,
        string BillNumber,
        decimal OutstandingAmount,
        string BillStatusCode);

    private sealed record AccountRow(
        long EducationAccountId,
        decimal CurrentBalance);

    private sealed record BillPaymentStateRow(
        decimal OutstandingAmount,
        string BillStatusCode);
}
