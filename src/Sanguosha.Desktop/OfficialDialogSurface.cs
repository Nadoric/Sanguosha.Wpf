using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sanguosha.Desktop
{
    // 官网窗口图按九宫格显示，保留边角与标题栏比例，不重绘素材。
    public sealed class OfficialDialogSurface : FrameworkElement
    {
        private static readonly BitmapSource[] Slices = CreateSlices();

        private static BitmapSource[] CreateSlices()
        {
            BitmapSource source = OfficialInterface.DialogBackground;
            int[] xs = { 0, 16, source.PixelWidth - 16, source.PixelWidth };
            int[] ys = { 0, 52, source.PixelHeight - 32, source.PixelHeight };
            List<BitmapSource> slices = new();
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    CroppedBitmap slice = new(source, new Int32Rect(
                        xs[column], ys[row], xs[column + 1] - xs[column], ys[row + 1] - ys[row]));
                    slice.Freeze();
                    slices.Add(slice);
                }
            }

            return slices.ToArray();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ActualWidth < 32 || ActualHeight < 84)
            {
                return;
            }

            double[] xs = { 0, 16, ActualWidth - 16, ActualWidth };
            double[] ys = { 0, 52, ActualHeight - 32, ActualHeight };
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    drawingContext.DrawImage(Slices[row * 3 + column], new Rect(
                        xs[column], ys[row], xs[column + 1] - xs[column], ys[row + 1] - ys[row]));
                }
            }
        }
    }
}
