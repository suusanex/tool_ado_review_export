using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration.TestHelpers;

public sealed class StubApiFactory : WebApplicationFactory<Program>
{
    private readonly string _mode;

    public StubApiFactory(string mode)
    {
        _mode = mode;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("STUB_MODE", _mode);

        // Program.cs reads Environment variables directly.
        Environment.SetEnvironmentVariable("STUB_MODE", _mode);
    }
}
