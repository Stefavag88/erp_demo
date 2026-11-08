using System.Diagnostics;
using Microsoft.Data.SqlClient;
namespace ERP.Discounts.IntegrationTests.Sql;

// Provisioning and fixture DDL are test-only. Runtime repositories never execute SQL text.
public sealed class SqlServerFixture : IAsyncDisposable
{
    private readonly SqlConnectionStringBuilder settings;
    public string DatabaseName { get; } = "ERPDiscounts_Test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get { var copy=new SqlConnectionStringBuilder(settings.ConnectionString){InitialCatalog=DatabaseName}; return copy.ConnectionString; } }
    private SqlServerFixture(string connectionString) => settings=new(connectionString);
    public static async Task<SqlServerFixture> CreateAsync(int through = 4)
    {
        var fixture=new SqlServerFixture(Environment.GetEnvironmentVariable("ERP_DISCOUNTS_TEST_CONNECTION") ?? throw new InvalidOperationException("SQL tests require opt-in connection."));
        try { for(var i=0;i<=through;i++) await fixture.ApplyAsync(i); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }
    public async Task ApplyAsync(int number)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !File.Exists(Path.Combine(root.FullName,"ERP.Discounts.sln"))) root=root.Parent;
        if(root is null) throw new InvalidOperationException("Repository root not found.");
        var file=Directory.GetFiles(Path.Combine(root.FullName,"sql"),$"{number:D3}_*.sql").Single();
        await RunAsync(["-d","master","-v","DatabaseName="+DatabaseName,"-i",file]);
    }
    public Task<string> SqlAsync(string query) => RunAsync(["-d",DatabaseName,"-Q",query]);
    private async Task<string> RunAsync(string[] arguments)
    {
        var start=new ProcessStartInfo("sqlcmd"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(var arg in new[]{"-S",settings.DataSource,"-b","-C","-l","5","-h","-1","-W"}) start.ArgumentList.Add(arg);
        if(settings.IntegratedSecurity) start.ArgumentList.Add("-E");
        else {start.ArgumentList.Add("-U");start.ArgumentList.Add(settings.UserID);start.Environment["SQLCMDPASSWORD"]=settings.Password;}
        foreach(var arg in arguments) start.ArgumentList.Add(arg);
        using var process=Process.Start(start) ?? throw new InvalidOperationException("Could not start sqlcmd.");
        var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try {await process.WaitForExitAsync(timeout.Token);}catch {process.Kill(entireProcessTree:true);throw;}
        var result=await output;var error=await errors;
        if(process.ExitCode!=0)throw new InvalidOperationException($"sqlcmd exited {process.ExitCode}: {result} {error}");
        return result;
    }
    public async ValueTask DisposeAsync()
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(DatabaseName,@"\AERPDiscounts_Test_[0-9a-f]{32}\z"))throw new InvalidOperationException("Unsafe test database cleanup target.");
        await RunAsync(["-d","master","-Q",$"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END"]);
    }
}
