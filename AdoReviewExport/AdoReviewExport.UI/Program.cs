using System;
using System.Threading;

namespace AdoReviewExport.UI;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Phase 3+ implements GUI/CLI branching.
        Microsoft.UI.Xaml.Application.Start(_params =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }
}
