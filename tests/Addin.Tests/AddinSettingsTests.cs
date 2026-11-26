using Addin;
using Xunit;

namespace Addin.Tests
{
    /// <summary>The snapshot after Synchronize with Central is on unless this PC's settings switch it off.</summary>
    public class AddinSettingsTests
    {
        [Theory]
        [InlineData("{ \"snapshotAfterSync\": false }", false)]
        [InlineData("{\"snapshotAfterSync\":false}", false)]
        [InlineData("{ \"snapshotAfterSync\": true }", true)]
        [InlineData("{ }", true)]
        [InlineData("not json at all", true)]
        [InlineData(null, true)]
        public void Only_an_explicit_false_switches_it_off(string? settingsText, bool expected)
        {
            Assert.Equal(expected, AddinSettings.IsSnapshotAfterSyncOn(settingsText));
        }

        [Fact]
        public void A_settings_file_that_cannot_be_read_leaves_it_on()
        {
            bool isOn = AddinSettings.IsSnapshotAfterSyncOn("Product", path => throw new System.IO.IOException("locked"));

            Assert.True(isOn);
        }
    }
}
