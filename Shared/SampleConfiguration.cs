// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace OCR
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "Windows Runtime OCR";


    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}