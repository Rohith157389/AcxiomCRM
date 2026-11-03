namespace AcxiomCRM.Services;

public interface IApplicationInfoService
{
    string GetApplicationName();

    string GetEnvironmentName();
}
