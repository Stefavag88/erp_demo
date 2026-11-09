using System.Runtime.CompilerServices;

namespace ERP.CustomerCustomFields.IntegrationTests.Sql;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ERP_CUSTOM_FIELDS_TEST_CONNECTION")))
            Skip = "Set ERP_CUSTOM_FIELDS_TEST_CONNECTION to opt into disposable SQL Server tests.";
    }
}
