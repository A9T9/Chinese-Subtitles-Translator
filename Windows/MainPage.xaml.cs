/*
 (a9t9)
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using OCR.Common;
using Windows.UI.Input;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using WindowsPreview.Media.Ocr;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Storage;
using PortableRest;
using System.Diagnostics;

namespace OCR
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{

		//Help class for processing responce from yandex.translate
		public class YandexTranslateResponce
		{
			public int code { get; set; }
			public String lang { get; set; }
			public IEnumerable<String> text { get; set; }
		}
		//Min size of image - OCR lib requrement
		const int MIN_OCR_SIZE = 40;

		public static MainPage Current;
		PointerPoint startPoint;
		PointerPoint endPoint;
		WebViewBrush brush;
		WriteableBitmap croppedBitmap;
		private OcrEngine ocrEngineTraditional;
		private OcrEngine ocrEngineSimpl;
		bool bLoading = false;
		RestClient rest;

		public MainPage()
		{
			this.InitializeComponent();
			// This is a static public property that allows downstream pages to get a handle to the MainPage instance
			// in order to call methods that are in this class.
			Current = this;
			wbMain.NavigationStarting += wbMain_NavigationStarting;
			wbMain.ContentLoading += wbMain_ContentLoading;
			wbMain.DOMContentLoaded += wbMain_DOMContentLoaded;
			wbMain.UnviewableContentIdentified += wbMain_UnviewableContentIdentified;
			wbMain.NavigationCompleted += wbMain_NavigationCompleted;
			wbMain.LoadCompleted += wbMain_LoadCompleted;
			wbMain.NavigationFailed += wbMain_NavigationFailed;
			wbMain.ScriptNotify += wbMain_ScriptNotify;

			ocrEngineTraditional = new OcrEngine(OcrLanguage.ChineseTraditional);
			ocrEngineSimpl = new OcrEngine(OcrLanguage.ChineseSimplified);

			//Create rest client
			rest = new RestClient();
            //Please get your own Yandex translate key - its free"
			rest.BaseUrl = "https://translate.yandex.net/api/v1.5/tr.json/translate?key=XXXXXXXXXXXXX&lang=zh-en&&text=";

			//Create timer - 2 times in a sec we read current url from web view
			DispatcherTimer dt = new DispatcherTimer();
			dt.Interval = TimeSpan.FromMilliseconds(500);
			dt.Tick += dt_Tick;
			dt.Start();

			//key pressing - hotkeys
			Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;


            //Autostart
            Loaded += MainPage_Loaded;

            Application.Current.UnhandledException += Current_UnhandledException;
        }

        void Current_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageDialog d = new MessageDialog("Error:" + e.Message);

            e.Handled = true;
            d.ShowAsync();
        }

        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Button_Click(null, null);


		}


		/// <summary>
		/// Hot keys
		/// </summary>
		void CoreWindow_CharacterReceived(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.CharacterReceivedEventArgs args)
		{
			if (args.KeyCode == 26) //Ctrl+Z
			{
				Button_Click_2(null, null);
			}
		}

		/// <summary>
		/// Update URL by timer - workaround
		/// </summary>
		void dt_Tick(object sender, object e)
		{
			updateUrl();
		}

		void wbMain_ScriptNotify(object sender, NotifyEventArgs e)
		{
		}

		void wbMain_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
		{
			bLoading = false;
		}

		void wbMain_LoadCompleted(object sender, NavigationEventArgs e)
		{
			LoadingProcessProgressRing.IsActive = false;
			wbMain.Visibility = Visibility.Visible;
			MaskRectangle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
		}

		void wbMain_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
		{
			bLoading = false;
			//Hack to avoid opening links in new page
			wbMain.InvokeScriptAsync("eval", new[]
            {
                @"(function()
                {
                    var hyperlinks = document.getElementsByTagName('a');
                    for(var i = 0; i < hyperlinks.length; i++)
                    {
                        if(hyperlinks[i].getAttribute('target') != null)
                        {
                            hyperlinks[i].setAttribute('target', '_self');
                        }
                    }
                })()"
            });


		}

		void wbMain_UnviewableContentIdentified(WebView sender, WebViewUnviewableContentIdentifiedEventArgs args)
		{
		}

		void wbMain_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
		{
		}

		void wbMain_ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
		{
		}

		void wbMain_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
		{
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{

		}



		/// <summary>
		/// Used to display messages to the user
		/// </summary>
		public void NotifyUser(string strMessage, NotifyType type)
		{
			switch (type)
			{
				case NotifyType.StatusMessage:
					StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
					break;
				case NotifyType.ErrorMessage:
					StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
					break;
			}
			StatusBlock.Text = strMessage;

			// Collapse the StatusBlock if it has no text to conserve real estate.
			if (StatusBlock.Text != String.Empty)
			{
				StatusBorder.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			else
			{
				StatusBorder.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			}
		}

		async void Footer_Click(object sender, RoutedEventArgs e)
		{
			await Windows.System.Launcher.LaunchUriAsync(new Uri(((HyperlinkButton)sender).Tag.ToString()));
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Uri targetUri = new Uri(txtUrl.Text);
				LoadingProcessProgressRing.IsActive = true;
				bLoading = true;
				wbMain.Navigate(targetUri);
            
			}
			catch (FormatException myE)
			{
				// Bad address  - search on google
				String s = "https://www.google.com/search?q=";
				String s2 = System.Net.WebUtility.UrlEncode(txtUrl.Text);
				Uri targetUri2 = new Uri(s + s2);
				LoadingProcessProgressRing.IsActive = true;
				bLoading = true;
				wbMain.Navigate(targetUri2);
			}
		}



        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            
            if (wbMain.CanGoBack)  wbMain.GoBack();
	

		}


        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (wbMain.CanGoForward) wbMain.GoForward();
        }




		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			if (wbMain.Visibility == Visibility.Collapsed)
			{
				MaskRectangle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				AreaRectangle.Visibility = cbFrame.IsChecked == true ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
				wbMain.Visibility = Visibility.Visible;
				btnCapture.Content = "New Subtitle Area";
				NotifyUser("Web mode", NotifyType.StatusMessage);
			}
			else
			{
				brush = new WebViewBrush();
				brush.SourceName = "wbMain";
				brush.Redraw();
				MaskRectangle.Visibility = Windows.UI.Xaml.Visibility.Visible;
				AreaRectangle.Visibility = Windows.UI.Xaml.Visibility.Visible;
				MaskRectangle.Fill = brush;
				brush.Redraw();
				wbMain.Visibility = Visibility.Collapsed;
				btnCapture.Content = "* SET AREA MODE *";
				NotifyUser("Capture mode", NotifyType.StatusMessage);
			}
		}

		private void MaskRectangle_PointerMoved(object sender, PointerRoutedEventArgs e)
		{
			endPoint = e.GetCurrentPoint(overlapGrid);
			updateArea();
		}

		private void MaskRectangle_PointerEntered(object sender, PointerRoutedEventArgs e)
		{

		}

		private void MaskRectangle_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if (startPoint != null) return;
			startPoint = e.GetCurrentPoint(overlapGrid);
			endPoint = e.GetCurrentPoint(overlapGrid);
			updateArea();
		}

		/// <summary>
		/// Update area to capture
		/// </summary>
		private void updateArea()
		{
			if (startPoint == null) return;
			if (endPoint == null) return;
			AreaRectangle.Width = Math.Abs(startPoint.Position.X - endPoint.Position.X);
			AreaRectangle.Height = Math.Abs(startPoint.Position.Y - endPoint.Position.Y);

			Canvas.SetLeft(AreaRectangle, Math.Min(startPoint.Position.X, endPoint.Position.X));
			Canvas.SetTop(AreaRectangle, Math.Min(startPoint.Position.Y, endPoint.Position.Y));
			AreaRectangle.Visibility = Windows.UI.Xaml.Visibility.Visible;
		}

		private async void MaskRectangle_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			endPoint = e.GetCurrentPoint(overlapGrid);
			updateArea();
			startPoint = endPoint = null;

			await captureBlock();
			await doOcrTranslate();
			Button_Click_1(null, null);
		}

		private async Task captureBlock()
		{
			if (brush == null) return;
			brush.Redraw();
			await CaptureAndSave(MaskRectangle, imgCapture);

		}


		async System.Threading.Tasks.Task<int> CaptureAndSave(UIElement element, Image img)
		{
			if (element.Visibility != Windows.UI.Xaml.Visibility.Visible) return 0;
			// capture    
			var bitmap = new Windows.UI.Xaml.Media.Imaging.RenderTargetBitmap();
			await bitmap.RenderAsync(element);

			double x, y, w, h;
			w = AreaRectangle.Width;
			h = AreaRectangle.Height;
			if (Double.IsNaN(w) || Double.IsNaN(h)) return 0;
			x = Canvas.GetLeft(AreaRectangle);
			y = Canvas.GetTop(AreaRectangle);

			//Check& correct for min sizes
			if (w < MIN_OCR_SIZE)
			{
				double ww;
				ww = MaskRectangle.Width;
				if (ww - x < MIN_OCR_SIZE)
				{
					x = ww - MIN_OCR_SIZE;
				}
				w = 40;
			}

			if (w < MIN_OCR_SIZE)
			{
				double hh;
				hh = MaskRectangle.Height;
				if (hh - y < MIN_OCR_SIZE)
				{
					y = hh - MIN_OCR_SIZE;
				}
				h = 40;
			}



			IBuffer buff = await bitmap.GetPixelsAsync();
			InMemoryRandomAccessStream memw = new InMemoryRandomAccessStream();
			var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, memw);
			byte[] bytes = buff.ToArray();
			var dpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

			//TODODEBUG
			//new MessageDialog(String.Format("Rectangle: {0},{1} - {2}, {3}", x, y, w, h) +"\n"+
			//	String.Format("Params: {0} - {1}, {2}", dpi, bitmap.PixelWidth, bitmap.PixelHeight) + "\n" +
			//	String.Format("WebView: {0},{1} - {2}, {3}", Canvas.GetLeft(wbMain), Canvas.GetTop(wbMain), wbMain.ActualWidth, wbMain.ActualHeight) + "\n" +
			//	String.Format("Area: {0},{1} - {2}, {3}", Canvas.GetLeft(AreaRectangle), Canvas.GetTop(AreaRectangle), AreaRectangle.ActualWidth, AreaRectangle.ActualHeight)
			//	).ShowAsync();

			encoder.SetPixelData(BitmapPixelFormat.Bgra8,
								 BitmapAlphaMode.Ignore,
								 (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight,
								 dpi, dpi, bytes);

			await encoder.FlushAsync();


			IRandomAccessStream sss = memw;
			BitmapEncoder coder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, sss);

			//Fixfor win9=10 (it's drowing with some scaling)
			double kx = 1, ky = 1;
			kx = bitmap.PixelWidth / wbMain.ActualWidth;
			ky = bitmap.PixelHeight / wbMain.ActualHeight;
			x = x * kx;
			y = y * ky;
			w = w * kx;
			h = h * ky;

			BitmapDecoder dec = null;
			try
			{
				dec = await BitmapDecoder.CreateAsync(sss);
			}
			catch (Exception ex)
			{
			}
			croppedBitmap = await CropBitmap.GetCroppedBitmapAsync(dec, new Point(x, y), new Size(w, h), 1);

			img.Source = croppedBitmap;
			return 0;
		}

		async Task doOcrTranslate()
		{
			if (brush == null) return;

			NotifyUser("Start OCR process", NotifyType.StatusMessage);
			WriteableBitmap bitmap = croppedBitmap;
			// This main API call to extract text from image.
			OcrResult ocrResult = null;
			try
			{
				if (rbSimple.IsChecked == true)
				{
					ocrResult = await ocrEngineSimpl.RecognizeAsync((uint)bitmap.PixelHeight, (uint)bitmap.PixelWidth, bitmap.PixelBuffer.ToArray());
				}
				else
				{
					ocrResult = await ocrEngineTraditional.RecognizeAsync((uint)bitmap.PixelHeight, (uint)bitmap.PixelWidth, bitmap.PixelBuffer.ToArray());
				}
			}
			catch (Exception ocrE)
			{
				NotifyUser("OCR error:" + ocrE, NotifyType.ErrorMessage);
			}

			// OCR result does not contain any lines, no text was recognized. 
			if (ocrResult != null && ocrResult.Lines != null)
			{

				string extractedText = "";

				// Iterate over recognized lines of text.
				foreach (var line in ocrResult.Lines)
				{
					foreach (var v in line.Words)
						extractedText += v.Text;
					extractedText += Environment.NewLine;
				}

				NotifyUser("OCR success:", NotifyType.StatusMessage);
			//	txtOCR.Text = extractedText;
                txtbox111.Text = extractedText;
				try
				{
					txtPin.Text = ChineseToPinYin.Convert3Pin(extractedText);
				}
				catch (Exception pinEx)
				{
					txtPin.Text = "Error during converting to PinYin";
				}

				NotifyUser("Start translation", NotifyType.StatusMessage);
				if (cbTranslate.IsChecked == true)
					DoTranslate();
			}
			else
			{
			//	txtOCR.Text = "No text.";
                txtbox111.Text = "No text.";
				txtPin.Text = "No text.";
				txtTranslation.Text = "No text.";
				NotifyUser("No text recognized", NotifyType.ErrorMessage);
			}
		}


		private async void Button_Click_2(object sender, RoutedEventArgs e)
		{
			if (wbMain.Visibility == Visibility.Visible)
				MaskRectangle.Visibility = Windows.UI.Xaml.Visibility.Visible;
			await captureBlock();
			await doOcrTranslate();
			if (wbMain.Visibility == Visibility.Visible)
				MaskRectangle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
		}

		private async void DoTranslate()
		{
			try
			{
                var request = new RestRequest(txtbox111.Text);
				var res = await rest.ExecuteAsync<YandexTranslateResponce>(request);
				if (res.code == 200)
				{
					string s = "";
					foreach (var w in res.text)
						s += w;
					txtTranslation.Text = s;
					NotifyUser("Translation success", NotifyType.StatusMessage);
				}
				else
				{
					txtTranslation.Text = string.Format("Translation error: {0}", res.code);
					NotifyUser(string.Format("Translation error: {0}", res.code), NotifyType.ErrorMessage);
				}
			}
			catch (Exception ex)
			{
				NotifyUser("Erorr during translation", NotifyType.ErrorMessage);
			}
		}



		/// <summary>
		/// Hack - each timer tickget current url and update it
		/// </summary>
		void updateUrl()
		{
			try
			{
				if (FocusManager.GetFocusedElement() == txtUrl) return;
				if (bLoading) return;

				string url = wbMain.InvokeScript("eval", new string[] { "document.URL;" }); // This is where we will sift to get the 
				if (!txtUrl.Text.Equals(url))
				{
					txtUrl.Text = url;
				}
			}
			catch (Exception ex)
			{
			}
		}


		private void wbMain_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
		}

		private void cbFrame_Click(object sender, RoutedEventArgs e)
		{
			if (wbMain.Visibility == Windows.UI.Xaml.Visibility.Visible)
				AreaRectangle.Visibility = cbFrame.IsChecked == true ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
		}

		private void txtUrl_KeyUp(object sender, KeyRoutedEventArgs e)
		{
			if (e.Key == Windows.System.VirtualKey.Enter)
			{
				Button_Click(null, null);
			}
		}

		private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
		{

		}

		private void Hyperlink_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
		{

		}

        private void txtUrl_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

	}

	public enum NotifyType
	{
		StatusMessage,
		ErrorMessage
	};


}
