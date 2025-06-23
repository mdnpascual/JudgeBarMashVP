using System.Windows;
using ScreenCapturerNS;
using Brush = System.Drawing.Brush;
using Brushes = System.Drawing.Brushes;
using Font = System.Drawing.Font;
using System.Diagnostics;
using Rectangle = System.Drawing.Rectangle;
using Color = System.Drawing.Color;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Point = System.Drawing.Point;
using Emgu.CV.OCR;
using Size = System.Drawing.Size;

namespace JudgeBarMashVP
{
    public partial class MainWindow : Window
    {
        private const int CropStartX = 1800;
        private const int CropStartY = 660;
        private const int CropEndX = 2046;
        private const int CropEndY = 800;
        private const int TesseractCropStartX = 67;
        private const int TesseractCropEndX = 1967 - CropStartX - TesseractCropStartX;
        private const int TesseractCropEndY = CropEndY - CropStartY;
        private const int GaussianBlurSize = 5;
        private const int ThresholdValue = 150; // HDR = 180

        private Bitmap _currentBitmap;
        private Bitmap _fpsBitmap;
        private Image<Bgr, byte> _croppedImage;
        private Image<Gray, byte> _grayImage;
        private Image<Gray, byte> _tesseractImage;
        private Stopwatch _stopwatch;
        public FixedSizeQueue<int> result50;
        private int _frameCount;
        private int _fps;
        private bool isDebug = false;
        private bool useAI = false;
        private bool useEMGU = true;

        private AI_OCR aiOCR;
        private ImageViewer _viewer;
        private Tesseract _tesseract;
        private float _confidence;

        private (int x, int y)[] negativeSignPixelPositions = new (int x, int y)[]
        {
            (10, 39), (12, 39), (15, 39), (19, 39), (22, 39)
        };

        private (int x, int y)[] positiveSignPixelPositions = new (int x, int y)[]
        {
            (209, 39), (238, 39), (224, 39), (224, 24), (224, 54)
        };

        public MainWindow()
        {
            InitializeComponent();
            aiOCR = new AI_OCR();
            InitializeTesseract();
            result50 = new FixedSizeQueue<int>(50);
            pictureBox.Click += PictureBox_Click;
            ScreenCapturer.OnScreenUpdated += OnScreenUpdated;
            ScreenCapturer.StartCapture();
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        private void PictureBox_Click(object sender, System.EventArgs e)
        {
            // Clear result50 and fill it with zeros
            result50.ClearAndFillWithZeros();

            // Redraw the bar
            UpdateBar();
        }

        private void InitializeTesseract()
        {
            _tesseract = new Tesseract("D:\\Downloads\\", "eng", OcrEngineMode.Default);
            _tesseract.SetVariable("tessedit_char_whitelist", "0123456789ms-+");
        }

        private void UpdateWindowTitle()
        {
            float average = CalculateAverage(result50);
            float weightedAverage = CalculateWeightedAverage(result50);
            Dispatcher.BeginInvoke(() =>
            {
                Title = $"{_fps} FPS | Average: {average:F2} | Weighted: {weightedAverage:F2}";
            });
        }

        private void OnScreenUpdated(object? sender, OnScreenUpdatedEventArgs e)
        {
            if (isDebug)
            {
                if (useEMGU)
                {
                    ProcessDebugFrameWithEmgu(e);
                }
                else
                {
                    ProcessDebugFrameWithoutEmgu(e);
                }
            }
            else
            {
                if (useEMGU)
                {
                    ProcessFrameWithEmgu(e);
                }
                else
                {
                    ProcessFrameWithoutEmgu(e);
                }
            }
        }

        private void ProcessDebugFrameWithEmgu(OnScreenUpdatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                CleanupImages();

                Image<Bgr, byte> originalImage = e.Bitmap.ToImage<Bgr, byte>();
                _croppedImage = CropImage(originalImage, CropStartX, CropStartY, CropEndX, CropEndY);

                if (useAI)
                {
                    ProcessAIWithEmgu();
                }
                else
                {
                    ProcessOCRWithEmgu();
                }

                UpdateFPS();
                DrawDebugFPS(originalImage);
                originalImage.Dispose();
            });
        }

        private void ProcessDebugFrameWithoutEmgu(OnScreenUpdatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                CleanupCurrentBitmap();

                Bitmap originalBitmap = (Bitmap)e.Bitmap.Clone();
                _currentBitmap = CropBitmap(originalBitmap, CropStartX, CropStartY, CropEndX, CropEndY);

                if (useAI)
                {
                    ProcessAIWithoutEmgu();
                }

                UpdateFPS();
                DrawFPS(_currentBitmap, _fps, result50);
                originalBitmap.Dispose();
            });
        }

        private void ProcessFrameWithEmgu(OnScreenUpdatedEventArgs e)
        {
            CleanupImages();

            Image<Bgr, byte> originalImage = e.Bitmap.ToImage<Bgr, byte>();
            _croppedImage = CropImage(originalImage, CropStartX, CropStartY, CropEndX, CropEndY);

            if (useAI)
            {
                ProcessAIWithEmgu();
            }
            else
            {
                ProcessOCRWithEmgu();
            }

            UpdateFPS();
            originalImage.Dispose();

            //BeginInvokeEmgu();
        }

        private void ProcessFrameWithoutEmgu(OnScreenUpdatedEventArgs e)
        {
            CleanupCurrentBitmap();

            Bitmap originalBitmap = (Bitmap)e.Bitmap.Clone();
            _currentBitmap = CropBitmap(originalBitmap, CropStartX, CropStartY, CropEndX, CropEndY);

            if (useAI)
            {
                ProcessAIWithoutEmgu();
            }

            UpdateFPS();
            originalBitmap.Dispose();
            BeginInvokeWitoutEmgu();
        }

        private void BeginInvokeWitoutEmgu()
        {
            Dispatcher.BeginInvoke(() =>
            {
                // Create a white bitmap to display the FPS
                using (Bitmap whiteBitmap = new Bitmap(200, 140))
                {
                    using (Graphics g = Graphics.FromImage(whiteBitmap))
                    {
                        // Fill the bitmap with white background
                        g.Clear(Color.White);

                        if (_fpsBitmap != null)
                        {
                            _fpsBitmap.Dispose();
                        }
                        _fpsBitmap = (Bitmap)whiteBitmap.Clone();

                        // Draw the FPS text
                        DrawFPS(_fpsBitmap, _fps, result50);
                    }
                }

                pictureBox.Image = _fpsBitmap;
            });
        }

        private void BeginInvokeEmgu()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_grayImage != null)
                {
                    _grayImage.Dispose();
                }
                // Create a white bitmap to display the FPS
                _grayImage = new Image<Gray, byte>(200, 140);
                var words = _tesseract.GetWords();
                DrawFPS_EMGU(_grayImage, _fps, result50, words.Length > 0 ? words[0] : null);

                // Initialize the ImageViewer if it hasn't been created yet
                if (_viewer == null)
                {
                    _viewer = new ImageViewer(_grayImage, "Cropped Image");
                    _viewer.Show();
                }
                else
                {
                    _viewer.Image = _grayImage;
                }

            });
        }

        private void CleanupImages()
        {
            _currentBitmap?.Dispose();
            _croppedImage?.Dispose();
            _grayImage?.Dispose();
            _tesseractImage?.Dispose();
        }

        private void CleanupCurrentBitmap()
        {
            _currentBitmap?.Dispose();
        }

        private Image<Bgr, byte> CropImage(Image<Bgr, byte> image, int startX, int startY, int endX, int endY)
        {
            Rectangle cropArea = new Rectangle(startX, startY, endX - startX, endY - startY);
            return image.GetSubRect(cropArea);
        }

        private Bitmap CropBitmap(Bitmap bitmap, int startX, int startY, int endX, int endY)
        {
            Rectangle cropArea = new Rectangle(startX, startY, endX - startX, endY - startY);
            return bitmap.Clone(cropArea, bitmap.PixelFormat);
        }

        private void ProcessAIWithEmgu()
        {
            Bitmap croppedBitmap = _croppedImage.ToBitmap();
            var response = aiOCR.PredictNumber(croppedBitmap);
            if (int.TryParse(response, out int aiResponse))
            {
                result50.Enqueue(aiResponse);
            }
        }

        private void ProcessAIWithoutEmgu()
        {
            var response = aiOCR.PredictNumber(_currentBitmap);
            if (int.TryParse(response, out int aiResponse))
            {
                result50.Enqueue(aiResponse);
            }
        }

        private void ProcessOCRWithEmgu()
        {
            // Convert to grayscale
            _grayImage = _croppedImage.Convert<Gray, byte>();

            // Apply Gaussian blur
            _grayImage = _grayImage.SmoothGaussian(GaussianBlurSize);

            // Apply binary thresholding
            _grayImage = _grayImage.ThresholdBinary(new Gray(ThresholdValue), new Gray(255));

            // Morphological operations
            Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
            _grayImage = _grayImage.MorphologyEx(MorphOp.Dilate, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));

            // Use Emgu Tesseract for OCR
            _tesseractImage = _grayImage.GetSubRect(new Rectangle(TesseractCropStartX, 0, TesseractCropEndX, TesseractCropEndY));
            _tesseract.SetImage(_tesseractImage);
            _tesseract.Recognize();
            string ocrResult = _tesseract.GetUTF8Text();
            var tessWords = _tesseract.GetWords();

            // Only parse when "ms" marker is in string and confidence is more than 80
            if (tessWords.Length > 0 && tessWords[0].Text != "ms" && tessWords[0].Confident > _confidence)
            {
                parseOCR(ocrResult, result50, _grayImage);
            }
        }

        private void UpdateFPS()
        {
            _frameCount++;
            if (_stopwatch.ElapsedMilliseconds >= 1000) // Every second
            {
                _fps = _frameCount;
                _frameCount = 0;
                _stopwatch.Restart();
                UpdateWindowTitle();
            }
        }

        private void DrawDebugFPS(Image<Bgr, byte> originalImage)
        {
            var words = _tesseract.GetWords();
            DrawFPS_EMGU(_grayImage, _fps, result50, words.Length > 0 ? words[0] : null);

            // Initialize the ImageViewer if it hasn't been created yet
            if (_viewer == null)
            {
                _viewer = new ImageViewer(_grayImage, "Cropped Image");
                _viewer.Show();
            }
            else
            {
                _viewer.Image = _grayImage; // Update the image
            }
        }

        private void DrawFPS(Bitmap bitmap, int fps, FixedSizeQueue<int>? list = null)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Set font and brush for drawing
                Font font = new Font("Arial", 6, System.Drawing.FontStyle.Bold);
                Brush brush = Brushes.Red;

                // Draw the FPS text on the bitmap
                g.DrawString($"FPS: {fps}", font, brush, new PointF(2, 2));
                if (list != null)
                {
                    g.DrawString($"Results: {list.PrintQueue()}", font, brush, new PointF(2, 62));
                }
            }
        }

        private void DrawFPS_EMGU(Image<Gray, byte> image, int fps, FixedSizeQueue<int>? list = null, Tesseract.Word? word = null)
        {
            // Set the font type and scale
            var fontFace = FontFace.HersheySimplex;
            double fontScale = 0.5;
            int thickness = 1;
            MCvScalar color = new MCvScalar(255, 255, 255); // White color

            // Draw the FPS text on the image
            CvInvoke.PutText(image, $"FPS: {fps}", new Point(2, 12), fontFace, fontScale, color, thickness);

            if (list != null && word == null)
            {
                var result = list.PrintNewest();
                CvInvoke.PutText(image, $"Result: {result}", new Point(2, 78), fontFace, fontScale, color, thickness);
            }
            if (list != null && word != null && word.Value.Text != "ms" && word.Value.Confident > _confidence)
            {
                CvInvoke.PutText(image, $"Result: {word.Value.Text} ({word.Value.Confident})", new Point(2, 78), fontFace, fontScale, color, thickness);
            }
        }

        private void parseOCR(string ocrResult, FixedSizeQueue<int> result50, Image<Gray, byte> image)
        {
            if (ocrResult.Contains("ms"))
            {
                // Strip unwanted characters
                string cleanedResult = ocrResult
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Trim(); // Remove leading and trailing whitespace

                // Find the position of "ms" and take the substring before it
                int msIndex = cleanedResult.IndexOf("ms");
                if (msIndex > 0) // Ensure "ms" is found and there are characters before it
                {
                    string numberPart = cleanedResult.Substring(0, msIndex).Trim(); // Get the part before "ms"

                    // Try to parse the number
                    if (int.TryParse(numberPart, out int ocrResponse))
                    {
                        if (AreSpecifiedPixelsWhite(image, negativeSignPixelPositions))
                        {
                            if (AreSpecifiedPixelsWhite(image, positiveSignPixelPositions))
                            {
                                result50.Enqueue(ocrResponse);
                            }
                            else
                            {
                                result50.Enqueue(ocrResponse * -1);
                            }
                        }
                        else
                        {
                            result50.Enqueue(ocrResponse);
                        }
                        UpdateBar();
                    }
                }
            }
        }

        public bool AreSpecifiedPixelsWhite(Image<Gray, byte> grayImage, (int x, int y)[] pixelPositions)
        {
            foreach (var (x, y) in pixelPositions)
            {
                if (x >= 0 && x < grayImage.Width && y >= 0 && y < grayImage.Height)
                {
                    byte pixelValue = grayImage.Data[y, x, 0]; // For grayscale, the channel index is 0
                    if (pixelValue != 255)
                    {
                        return false; // At least one pixel is not white
                    }
                }
                else
                {
                    return false; // Pixel position is out of bounds
                }
            }
            return true; // All specified pixels are white
        }

        private void UpdateBar()
        {
            // Create a bitmap for the pictureBox
            Bitmap barBitmap = new Bitmap(800, 50);
            using (Graphics g = Graphics.FromImage(barBitmap))
            {
                g.Clear(Color.Black);

                // Define colors for each section
                Color darkOrange = Color.FromArgb(255, 140, 0);
                Color darkGreen = Color.FromArgb(0, 128, 0);
                Color darkBlue = Color.FromArgb(0, 102, 204);
                Color centerLineColor = Color.White;

                // Define section boundaries
                int[] boundaries = { 150, 100, 50, 1, 0, -1, -49, -100, -150 };
                Color[] colors = { darkOrange, darkGreen, darkBlue, centerLineColor, darkBlue, darkGreen, darkOrange };

                // Draw the sections
                for (int i = 0; i < colors.Length - 1; i++)
                {
                    {
                        int indexToUse = i < 3 ? i : i + 2;
                        int colorIdxToUse = i < 3 ? i : i + 1;
                        int startX = MapValueToX(boundaries[indexToUse]);
                        int endX = MapValueToX(boundaries[indexToUse + 1]);
                        using (Brush brush = new SolidBrush(colors[colorIdxToUse]))
                        {
                            g.FillRectangle(brush, startX, 20, endX - startX, 15);
                        }
                    }
                    if (i == 3)
                    {
                        int startXCenter = MapValueToX(1);
                        int endXCenter = MapValueToX(-1);
                        using (Brush brush = new SolidBrush(colors[i]))
                        {
                            g.FillRectangle(brush, startXCenter, 20, endXCenter - startXCenter, 15);
                        }
                    }
                }

                // Draw tick marks based on result50 values
                foreach (var value in result50.PrintQueue().Split(','))
                {
                    if (int.TryParse(value.Trim(), out int intValue))
                    {
                        int tickX = MapValueToX(intValue);
                        g.DrawLine(Pens.White, tickX, 0, tickX, 80); // Draw tick mark
                    }
                }

                // Calculate average and weighted average
                float average = CalculateAverage(result50);
                float weightedAverage = CalculateWeightedAverage(result50);

                // Draw the triangles
                DrawTriangle(g, average, Color.Orange, 20);
                DrawTriangle(g, weightedAverage, Color.Yellow, 20, true);
            }

            // Update the pictureBox image
            pictureBox.Image?.Dispose();
            pictureBox.Image = barBitmap;
        }

        // Helper method to map values to X coordinates in the picture box
        private int MapValueToX(int value)
        {
            value = value * -1;
            // Map the value to the range of the pictureBox width (800)
            // Assuming the range is from -150 to 150
            if (value < -150) return 0;
            if (value > 150) return 800;

            // Scale the value to the width of the pictureBox
            return (int)((value + 150) * (800.0 / 300.0));
        }

        private float CalculateAverage(FixedSizeQueue<int> queue)
        {
            if (queue.Size == 0) return 0;

            float sum = 0;
            foreach (var value in queue.PrintQueue().Split(','))
            {
                if (int.TryParse(value.Trim(), out int intValue))
                {
                    sum += intValue;
                }
            }
            return sum / queue.Size;
        }

        private float CalculateWeightedAverage(FixedSizeQueue<int> queue)
        {
            if (queue.Size == 0) return 0;

            float sum = 0;
            float totalWeight = 0;
            int count = 0;

            // Get the number of elements in the queue
            int queueCount = queue.PrintQueue().Split(',').Length;

            foreach (var value in queue.PrintQueue().Split(','))
            {
                if (int.TryParse(value.Trim(), out int intValue))
                {
                    // Calculate weight based on position
                    float weight = 3.0f - (1.0f * count / (queueCount - 1)); // Weight decreases from 2 to 1

                    // Ensure weight does not go below 1
                    weight = Math.Max(weight, 1.0f);

                    sum += intValue * weight;
                    totalWeight += weight;
                    count++;
                }
            }

            return sum / totalWeight; // Return the weighted average
        }

        private void DrawTriangle(Graphics g, float value, Color color, int size, bool isWeighted = false)
        {
            int baseLength = size; // Base length of the triangle
            int height = size; // Height of the triangle

            int x = MapValueToX((int)value);
            int y = isWeighted ? 35 : 20; // Y position for the triangle

            // Define the points for the triangle
            Point[] points = new Point[]
            {
                new Point(x, y), // Top point
                new Point(x - baseLength / 2, y - height + (2 * height * (isWeighted ? 1 : 0))), // Bottom left point
                new Point(x + baseLength / 2, y - height + (2 * height * (isWeighted ? 1 : 0)))  // Bottom right point
            };

            using (Brush brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, points);
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ScreenCapturer.StopCapture();
            base.OnClosing(e);
        }
    }
}