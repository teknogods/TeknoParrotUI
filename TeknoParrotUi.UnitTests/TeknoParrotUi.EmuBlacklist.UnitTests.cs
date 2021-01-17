using Xunit;

namespace TeknoParrotUi.UnitTests
{
    public class TeknoParrotUi
    {
        [Theory]
        [InlineData("config", false)]
        [InlineData("jconfig*.exe", true)]
        [InlineData("detoured.dll", true)]
        [InlineData("detoured", false)]
        [InlineData("typex_asd", true)]
        [InlineData("typex", false)]
        [InlineData("jvsemu*.dll", true)]
        [InlineData("jvsemu*", false)]
        [InlineData("ttx_lolcopter", true)]
        [InlineData("ttx", false)]
        [InlineData("monitor_pop", true)]
        [InlineData("monitor", false)]
        public void TestEmuBlacklistWithDifferentTrueAndFalseValues(string blackList, bool detected)
        {
            // Arrange
            // Act
            // Assert
            Assert.Equal(detected, EmuBlacklist.CheckBlacklist(blackList));
        }
    }
}
