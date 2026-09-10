using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Security.Principal;

namespace TrayManager;

internal static class Program
{
    internal static EventWaitHandle? ShowSignal;
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--scan"))
        {
            System.IO.Directory.CreateDirectory(Preferences.DirectoryPath);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Preferences.DirectoryPath, "scan.json"), System.Text.Json.JsonSerializer.Serialize(
                TrayCatalog.Read().Select(e => new { e.Key, e.Name, e.Path, e.Guid, Window = e.Window.ToInt64(), e.Uid, e.ProcessId })));
            return;
        }
        var suffix = WindowsIdentity.GetCurrent().User!.Value;
        using var mutex = new Mutex(true, @"Local\Tomclanc.TrayManager." + suffix, out var first);
        ShowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Tomclanc.TrayManager.Show." + suffix);
        if (!first) { ShowSignal.Set(); return; }
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(callbackArgs =>
            {
                SynchronizationContext.SetSynchronizationContext(new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
                _ = new TrayApplication();
            });
        }
        catch (Exception e)
        {
            System.IO.Directory.CreateDirectory(Preferences.DirectoryPath);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Preferences.DirectoryPath, "error.log"), e.ToString());
        }
        finally { ShowSignal.Dispose(); mutex.ReleaseMutex(); }
    }
}

public sealed partial class TrayApplication : Application
{
    private MainWindow? window;
    public TrayApplication()
    {
        UnhandledException += (_, e) =>
        {
            System.IO.Directory.CreateDirectory(Preferences.DirectoryPath);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Preferences.DirectoryPath, "ui-error.log"), e.Exception.ToString());
        };
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args) { window = new MainWindow(); window.Activate(); }
}
