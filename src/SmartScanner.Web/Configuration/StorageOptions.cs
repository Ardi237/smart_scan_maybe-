namespace SmartScanner.Web.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "SmartScanner";

    public string DataRoot { get; set; } = "data";
}
