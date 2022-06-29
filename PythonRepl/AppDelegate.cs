using Foundation;
using UIKit;

namespace PythonRepl
{
    [Register ("AppDelegate")]
    public class AppDelegate : UIResponder, IUIApplicationDelegate
    {
        [Export("window")]
        public UIWindow Window { get; set; }

        [Export ("application:didFinishLaunchingWithOptions:")]
        public bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            Window = new UIWindow(UIScreen.MainScreen.Bounds);
            var vc = new PythonReplViewController();
            Window.RootViewController = new UINavigationController(vc);
            Window.MakeKeyAndVisible();
            return true;
        }
    }
}


