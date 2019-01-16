﻿using DIM_Interaction.Cross;
using DIM_Interaction.Data;
using DIM_Interaction.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace DIM_Interaction.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NewPuzzle : Page
    {
        List<WriteableBitmap> images = new List<WriteableBitmap>();
        BitmapImage image = new BitmapImage();

        public NewPuzzle()
        {
            this.InitializeComponent();
        }


        private async void btPickImage_Click(object sender, RoutedEventArgs e)
        {
            if (images.Any()) images.Clear();
            FileOpenPicker picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(ImageTypes.Jpeg);
            picker.FileTypeFilter.Add(ImageTypes.Jpg);
            picker.FileTypeFilter.Add(ImageTypes.Png);
            StorageFile imagefile = await picker.PickSingleFileAsync();
            if (imagefile != null)
            {
                string imagename = imagefile.DisplayName;
                string imagepath = imagefile.Path;
                using (IRandomAccessStream fileStream = await imagefile.OpenAsync(FileAccessMode.Read))
                {
                    images = await CutImageInPiecesAsync(fileStream, 3);
                    image.SetSource(fileStream);
                    await SaveIRandomAccessStreamToFileAsync(fileStream, imagename);
                    StorageFolder pictureFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("SlidingPuzzles", CreationCollisionOption.OpenIfExists);
                    StorageFolder imageFolder = await pictureFolder.GetFolderAsync(imagename);
                    if (!PuzzleObservableList.Instance.Any(puzzle => puzzle.Name.Equals(imagename)))
                    {
                        PuzzleObservableList.Instance.Add(new Puzzle(imageFolder.DisplayName, imageFolder, image, false));
                    }
                    sampleImage.Source = image;
                    sampleImage.Tag = imageFolder;
                    puzzleGrid.Tag = imagename;
                    CreatePuzzleGridLines(3);
                }
            }
        }


        private void CreatePuzzleGridLines(int puzzlesize)
        {
            if (puzzleGrid.Children.Any())
            {
                puzzleGrid.Children.Clear();
            }
            int linesSize = 500 / puzzlesize;
            int linesXY = puzzlesize - 1;
            List<Line> XLinesForCanvas = new List<Line>();
            List<Line> YLinesForCanvas = new List<Line>();
            for (int i = 0; i < linesXY; i++)
            {
                XLinesForCanvas.Add(CreateLine(0, 0, 500, 0));
                YLinesForCanvas.Add(CreateLine(0, 500, 0, 0));
            }
            int XLineCount = 1;
            foreach (Line item in XLinesForCanvas)
            {
                Canvas.SetTop(item, linesSize * XLineCount);
                Canvas.SetLeft(item, 0);
                XLineCount++;
            }
            int YLineCount = 1;
            foreach (Line item in YLinesForCanvas)
            {
                Canvas.SetTop(item, 0);
                Canvas.SetLeft(item, linesSize * YLineCount);
                YLineCount++;
            }
            foreach (Line item in XLinesForCanvas)
            {
                puzzleGrid.Children.Add(item);
            }
            foreach (Line item in YLinesForCanvas)
            {
                puzzleGrid.Children.Add(item);
            }
        }

        private Line CreateLine(double X1, double Y1, double X2, double Y2)
        {
            Line line = new Line();
            line.Stroke = new SolidColorBrush(Colors.White);
            line.StrokeThickness = 3;
            line.X1 = X1;
            line.Y1 = Y1;
            line.X2 = X2;
            line.Y2 = Y2;
            return line;
        }

        private async void BtCreateCustomPuzzle_ClickAsync(object sender, RoutedEventArgs e)
        {
            bool result = false;
            int i3 = 0;
            images.ForEach(async image => await SaveBitmapToFileAsync(image, puzzleGrid.Tag as string, ++i3, 3));
            if (PuzzleObservableList.Instance.Any(puzzle => puzzle.Name == puzzleGrid.Tag as string))
                while (result == false) { result = await CheckIfItemExistsAsync(sampleImage.Tag as StorageFolder, "3"); }
            PuzzleObservableList.Instance.First(puzzle => puzzle.Name.Equals(puzzleGrid.Tag)).IsPuzzleAvailable = result;

            ContentDialog dg = new ContentDialog()
            {
                Title = "Puzzle is created",
                Content = "Puzzle has succesfully been created!",
                CloseButtonText = "Ok"
            };
            await dg.ShowAsync();
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(MainPage));
        }


        public static async Task SaveBitmapToFileAsync(WriteableBitmap image, string imagename, int id, int size)
        {
            StorageFolder pictureFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("SlidingPuzzles", CreationCollisionOption.OpenIfExists);
            StorageFolder pictureFolder2 = await pictureFolder.CreateFolderAsync(imagename, CreationCollisionOption.OpenIfExists);
            StorageFolder pictureFolder3 = await pictureFolder2.CreateFolderAsync(size.ToString(), CreationCollisionOption.OpenIfExists);
            StorageFile file = await pictureFolder3.CreateFileAsync($"id.ToString() {ImageTypes.Png}", CreationCollisionOption.ReplaceExisting);
            using (Stream stream = await file.OpenStreamForWriteAsync())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                Stream pixelStream = image.PixelBuffer.AsStream();
                byte[] pixels = new byte[image.PixelBuffer.Length];
                await pixelStream.ReadAsync(pixels, 0, pixels.Length);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)image.PixelWidth, (uint)image.PixelHeight, 96, 96, pixels);
                await encoder.FlushAsync();
            }
        }

        private async Task<List<WriteableBitmap>> CutImageInPiecesAsync(IRandomAccessStream filestream, int puzzlesize)
        {
            List<WriteableBitmap> images = new List<WriteableBitmap>();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(filestream);
            uint x = (uint)decimal.Zero;
            uint y = (uint)decimal.Zero;
            for (int i = (int)decimal.Zero; i < (puzzlesize * puzzlesize); i++)
            {
                InMemoryRandomAccessStream ras = new InMemoryRandomAccessStream();
                BitmapEncoder enc = await BitmapEncoder.CreateForTranscodingAsync(ras, decoder);
                enc.BitmapTransform.ScaledHeight = 501;
                enc.BitmapTransform.ScaledWidth = 501;
                BitmapBounds bounds = new BitmapBounds();
                uint size = Convert.ToUInt32(500 / puzzlesize);
                bounds.Height = size;
                bounds.Width = size;
                if (x > 490)
                {
                    x = 0;
                    y = y + size;
                }
                bounds.X = x;
                bounds.Y = y;
                enc.BitmapTransform.Bounds = bounds;
                await enc.FlushAsync();
                var wb = new WriteableBitmap(Convert.ToInt32(size), Convert.ToInt32(size));
                await wb.SetSourceAsync(ras);
                images.Add(wb);
                x = x + size;
            }
            return images;
        }

        public static async Task SaveIRandomAccessStreamToFileAsync(IRandomAccessStream filestream, string imagename)
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(filestream);
            InMemoryRandomAccessStream ras = new InMemoryRandomAccessStream();
            BitmapEncoder enc = await BitmapEncoder.CreateForTranscodingAsync(ras, decoder);
            enc.BitmapTransform.ScaledHeight = 500;
            enc.BitmapTransform.ScaledWidth = 500;
            BitmapBounds bounds = new BitmapBounds();
            uint size = Convert.ToUInt32(500);
            bounds.Height = size;
            bounds.Width = size;
            bounds.X = (uint)decimal.Zero;
            bounds.Y = (uint)decimal.Zero;
            enc.BitmapTransform.Bounds = bounds;
            await enc.FlushAsync();
            WriteableBitmap wb = new WriteableBitmap(500, 500);
            await wb.SetSourceAsync(ras);
            StorageFolder pictureFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("SlidingPuzzles", CreationCollisionOption.OpenIfExists);
            StorageFolder pictureFolder2 = await pictureFolder.CreateFolderAsync(imagename, CreationCollisionOption.OpenIfExists);
            StorageFile file = await pictureFolder2.CreateFileAsync(imagename + ".png", CreationCollisionOption.ReplaceExisting);
            using (Stream stream = await file.OpenStreamForWriteAsync())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                Stream pixelStream = wb.PixelBuffer.AsStream();
                byte[] pixels = new byte[wb.PixelBuffer.Length];
                await pixelStream.ReadAsync(pixels, 0, pixels.Length);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)wb.PixelWidth, (uint)wb.PixelHeight, 96, 96, pixels);
                await encoder.FlushAsync();
            }
        }

        private async Task<bool> CheckIfItemExistsAsync(StorageFolder Folder, string itemName)
        {
            var item = await Folder.TryGetItemAsync(itemName);
            return item != null;
        }
    }
}