using System.Windows.Media;
using DeskGroup.Helpers;
using Xunit;

namespace DeskGroup.Tests
{
    public class HexConvertersTests
    {
        [Theory]
        [InlineData("#0078D4", 0x00, 0x78, 0xD4)]
        [InlineData("0078D4", 0x00, 0x78, 0xD4)]
        [InlineData("#FF0078D4", 0x00, 0x78, 0xD4)]
        [InlineData("#FFFFFFFF", 0xFF, 0xFF, 0xFF)]
        public void HexToColorConverter_ConvertsValidHex(string input, byte expR, byte expG, byte expB)
        {
            var conv = new HexToColorConverter();
            var result = conv.Convert(input, typeof(Color), null, System.Globalization.CultureInfo.InvariantCulture);

            Assert.IsType<Color>(result);
            var color = (Color)result!;
            Assert.Equal(expR, color.R);
            Assert.Equal(expG, color.G);
            Assert.Equal(expB, color.B);
        }

        [Fact]
        public void HexToBrushConverter_ReturnsFrozenBrush()
        {
            var conv = new HexToBrushConverter();
            var result = conv.Convert("#FF112233", typeof(SolidColorBrush), null, System.Globalization.CultureInfo.InvariantCulture);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result!;
            Assert.True(brush.IsFrozen);
            Assert.Equal(0x11, brush.Color.R);
            Assert.Equal(0x22, brush.Color.G);
            Assert.Equal(0x33, brush.Color.B);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-color")]
        public void HexToColorConverter_InvalidReturnsTransparent(object? input)
        {
            var conv = new HexToColorConverter();
            var result = conv.Convert(input, typeof(Color), null, System.Globalization.CultureInfo.InvariantCulture);
            Assert.IsType<Color>(result);
            var color = (Color)result!;
            Assert.Equal(Colors.Transparent, color);
        }
    }
}
