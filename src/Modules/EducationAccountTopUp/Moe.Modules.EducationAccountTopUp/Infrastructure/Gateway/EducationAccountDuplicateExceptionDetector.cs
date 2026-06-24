using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal static class EducationAccountDuplicateExceptionDetector
{
    public static bool IsDuplicateEducationAccount(DbUpdateException exception)
    {
        return exception.InnerException switch
        {
            SqlException sqlException => sqlException.Number is 2601 or 2627,
            SqliteException sqliteException => sqliteException.SqliteErrorCode == 19,
            _ => false
        };
    }
}
