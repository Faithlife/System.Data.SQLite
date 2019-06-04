using Foundation;
using NUnit.Runner.Services;
using UIKit;

namespace XamarinTestRunner.iOS
{
    [Register("AppDelegate")]
    public partial class AppDelegate : global::Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
    {
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            global::Xamarin.Forms.Forms.Init();

			var nunit = new NUnit.Runner.App();
			nunit.AddTestAssembly(typeof(System.Data.SQLite.Tests.PlatformTests).Assembly);
			nunit.Options = new TestOptions { AutoRun = true };
			LoadApplication(nunit);

			return base.FinishedLaunching(app, options);
        }
    }
}
