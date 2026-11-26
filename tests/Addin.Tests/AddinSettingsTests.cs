using Addin;
using Xunit;

namespace Addin.Tests
{
    /// <summary>The snapshot after Synchronize with Central stays off unless this PC's settings switch it on.</summary>
    public class AddinSettingsTests
    {
        [Theory]
        [InlineData("{ \"snapshotAfterSync\": true }", true)]
        [InlineData("{\"snapshotAfterSync\":true}", true)]
        [InlineData("{ \"snapshotAfterSync\": false }", false)]
        [InlineData("{ }", false)]
        [InlineData("not json at all", false)]
        [InlineData(null, false)]
        public void Only_an_explicit_true_switches_it_on(string? settingsText, bool expected)
        {
            Assert.Equal(expected, AddinSettings.IsSnapshotAfterSyncOn(settingsText));
        }

        [Fact]
        public void A_settings_file_that_cannot_be_read_means_off()
        {
            bool isOn = AddinSettings.IsSnapshotAfterSyncOn("Product", path => throw new System.IO.IOException("locked"));

            Assert.False(isOn);
        }
    }
}
