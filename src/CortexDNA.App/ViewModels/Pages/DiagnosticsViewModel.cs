using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class DiagnosticsViewModel : PageViewModel
{
    public DiagnosticsViewModel(MainViewModel shell) : base(PageId.Diagnostics, "Application diagnostics", "Local application state and log access. No information is uploaded.", shell) { }
    public string Version => typeof(CortexDNA.App).Assembly.GetName().Version?.ToString() ?? "Unknown";
    public string WindowsVersion => Environment.OSVersion.VersionString;
    public string Logs => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CortexDNA", "Logs");
}
