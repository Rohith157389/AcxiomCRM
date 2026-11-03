namespace AcxiomCRM.Services;

public sealed class ApplicationInfoService : IApplicationInfoService
{
    private readonly IWebHostEnvironment _environment;

    public ApplicationInfoService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string GetApplicationName() => "AcxiomCRM";

    public string GetEnvironmentName() => _environment.EnvironmentName;
}
