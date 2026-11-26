using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Addin.Snapshots;
using Contracts;
using Xunit;

namespace Addin.Tests
{
    /// <summary>
    /// The add-in's hand-written JSON must read back into the snapshot classes exactly, the way the website reads it:
    /// System.Text.Json, camel case, and strict, so a misspelt or missing property fails here and not on the website.
    /// </summary>
    public class SnapshotJsonTests
    {
        private static readonly JsonSerializerOptions WEBSITE_READING = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        private static Snapshot SampleSnapshot()
        {
            var pileType = new ElementTypeFacts
            {
                TypeId = 501,
                FamilyName = "Bored pile",
                TypeName = "P1",
                Parameters = new List<ParameterFact>
                {
                    new() { Name = "Diameter", Number = 1.2, NumberUnit = "m", Display = "1200" },
                },
            };
            var wall = new ElementFacts
            {
                UniqueId = "a1b2c3-0001",
                ElementId = 1001,
                Category = "Walls",
                BuiltInCategory = "OST_Walls",
                TypeId = 400,
                FamilyName = "Basic Wall",
                TypeName = "Wall 600 mm \"core\"",
                LevelName = "Basement 04",
                BaseLevelName = "Basement 04",
                TopLevelName = "Ground Floor",
                BottomElevationM = -13.25,
                TopElevationM = 2.85,
                Materials = new List<MaterialQuantity>
                {
                    new() { MaterialName = "Concrete in-situ", MaterialClass = "Concrete", VolumeM3 = 12.5, AreaM2 = 40.25 },
                },
                Parameters = new List<ParameterFact>
                {
                    new() { Name = "Concrete grade", SharedGuid = Guid.Parse("00000000-0000-4000-8000-000000000001"), Text = "C50/60", Display = "C50/60" },
                },
            };
            var beamWithNoLevel = new ElementFacts { UniqueId = "a1b2c3-0002", ElementId = 3, Category = "Structural Framing", BuiltInCategory = "OST_StructuralFraming" };
            return new Snapshot
            {
                SnapshotId = Guid.Parse("6f1d3a52-8c47-4b0e-9a61-2f7c5e3b9d14"),
                TakenAtUtc = new DateTime(2026, 11, 26, 9, 30, 0, DateTimeKind.Utc),
                SyncedBy = "modeller",
                Trigger = SnapshotTriggers.MANUAL_SYNC,
                AddinVersion = "0.2.0",
                RevitVersion = "2026",
                RevitBuild = "26.5.0.55",
                ReadSeconds = 3.25,
                Model = new ModelIdentity { AccProjectId = Guid.NewGuid(), AccModelId = Guid.NewGuid(), CloudRegion = "EMEA", Title = "Tower STR", IsWorkshared = true, IsInCloud = true },
                Levels = new List<SnapshotLevel> { new() { ElementId = 311, Name = "Basement 04", ElevationM = -13.25, ProjectElevationM = -13.25 } },
                Types = new List<ElementTypeFacts> { pileType },
                Elements = new List<ElementFacts> { wall, beamWithNoLevel },
            };
        }

        private static Snapshot WriteAndReadBack(Snapshot snapshot)
        {
            var output = new StringWriter();
            SnapshotJson.Write(snapshot, output);
            return JsonSerializer.Deserialize<Snapshot>(output.ToString(), WEBSITE_READING)!;
        }

        [Fact]
        public void A_snapshot_reads_back_with_every_value_unchanged()
        {
            Snapshot written = SampleSnapshot();

            Snapshot read = WriteAndReadBack(written);

            string writtenAsJson = JsonSerializer.Serialize(written, WEBSITE_READING);
            string readAsJson = JsonSerializer.Serialize(read, WEBSITE_READING);
            Assert.Equal(writtenAsJson, readAsJson);
        }

        [Fact]
        public void The_times_and_ids_survive_exactly()
        {
            Snapshot written = SampleSnapshot();

            Snapshot read = WriteAndReadBack(written);

            Assert.Equal(written.SnapshotId, read.SnapshotId);
            Assert.Equal(written.TakenAtUtc, read.TakenAtUtc.ToUniversalTime());
            Assert.Equal(written.Model.AccModelId, read.Model.AccModelId);
            Assert.Equal(12.5, read.Elements[0].Materials[0].VolumeM3);
            Assert.Null(read.Elements[1].LevelName);
        }

        [Fact]
        public void A_model_outside_the_cloud_has_no_acc_ids()
        {
            Snapshot written = SampleSnapshot();
            written.Model = new ModelIdentity { Title = "Detached copy", IsDetached = true };

            Snapshot read = WriteAndReadBack(written);

            Assert.Null(read.Model.AccProjectId);
            Assert.Null(read.Model.AccModelId);
        }
    }
}
