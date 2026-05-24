namespace ProxyForward;

static class Program
{
    [STAThread]
    static void Main()
    {
        if (!SingleInstance.TryAcquire(out var singleInstanceMutex))
        {
            SingleInstance.SignalExistingInstance();
            return;
        }

        using (singleInstanceMutex)
        {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        }
    }    
}
