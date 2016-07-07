﻿using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using System.Threading.Tasks;
using TestApp;

namespace TestAndroid
{
    [Activity(Label = "TestAndroid", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        int count = 1;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            Button button = FindViewById<Button>(Resource.Id.MyButton);

            button.Click += async delegate 
            {
                button.Text = "Running Test";
                await DoTest();
                button.Text = "Done";
            };
        }

        // TODO improve that!
        Task DoTest()
        {
            var t = new TaskCompletionSource<bool>();
            new Java.Lang.Thread(() => 
            {
                try
                {
                    ThreadedRun();
                }
                finally { t.TrySetResult(true); }
            }).Start();
            return t.Task;
        }
        void ThreadedRun()
        {
            var path = new PathTests();
            path.PathWorks();
            //path.WeakRefWorks();

            var reg = new RegistryTests();
            reg.Create1();
            reg.Create2();

            var ser = new SerializationTests();
            ser.CheckAnnotation();
            ser.CheckComplexClass();
            ser.CheckComplexClass2();
            ser.CheckIsFastEnough();
            ser.CheckIsSmall();
            ser.CheckReaderWriter();
            ser.CheckSerializationName();
            ser.CheckSimpleTypes();
            ser.CheckSubclass();
            ser.CheckSubclass();
            ser.CheckTypeRestored();
            //ser.ExoticTest();
        }
    }
}
