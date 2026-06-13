namespace Cedeva.Infrastructure.Configuration;

/// <summary>Strongly-typed binding for the "AzureStorage" configuration section.</summary>
public class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "cedeva-files";
}
