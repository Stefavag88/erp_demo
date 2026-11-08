using ERP.Discounts.IntegrationTests.Sql;

namespace ERP.Discounts.IntegrationTests.Resilience;

// Disposable test databases only: production procedures remain read-only.
internal static class RetrySqlFaults
{
    internal static async Task InstallAsync(SqlServerFixture db, string procedureName, int failures, int errorNumber = 40197)
    {
        var timestamp = procedureName == "dbo.usp_Promotion_GetDefinitions" ? "NULL" : "@AsOfUtc";
        var failure = errorNumber == 40197
            ? "RAISERROR(40197,13,1,1); RETURN;"
            : $"THROW {errorNumber},'Permanent retry-test failure.',1;";
        var hook = $"SET NOCOUNT ON; DECLARE @retryAttempt int = NEXT VALUE FOR dbo.RetryAttempts; INSERT dbo.RetryProbe VALUES(@retryAttempt,{timestamp}); IF @retryAttempt<={failures} BEGIN {failure} END;";

        await db.SqlAsync($"""
            CREATE SEQUENCE dbo.RetryAttempts AS int START WITH 1;
            CREATE TABLE dbo.RetryProbe(Attempt int NOT NULL, AsOfUtc datetime2(7) NULL);
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'{procedureName.Replace("'", "''")}'));
            IF @definition IS NULL THROW 50000,'Retry-test procedure missing.',1;
            SET @definition=STUFF(@definition,1,CHARINDEX(N'PROCEDURE',@definition)-1,N'ALTER ');
            SET @definition=REPLACE(@definition,N'SET NOCOUNT ON;',N'{hook.Replace("'", "''")}');
            EXEC sys.sp_executesql @definition;
            """);
    }
}
