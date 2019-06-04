﻿using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using NUnit.Runner.Services;

namespace XamarinTestRunner.Droid
{
    [Activity(Label = "XamarinTestRunner", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);

			var nunit = new NUnit.Runner.App();
			nunit.AddTestAssembly(typeof(System.Data.SQLite.Tests.PlatformTests).Assembly);
			nunit.Options = new TestOptions { AutoRun = true };
			LoadApplication(nunit);
		}

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
