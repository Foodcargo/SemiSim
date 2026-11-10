using SemiconductorEdu.Domain.Equipment;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Infrastructure.Configuration;
using SemiconductorEdu.Infrastructure.Storage;
using SemiconductorEdu.Simulation.Initialization;

namespace SemiconductorEdu.IntegrationTests;

public sealed class ConfigurationAndSessionStoreTests
{
    [Fact]
    public void ConfigurationLoaderAcceptsValidStageOneConfigurationAndRejectsFutureProcess()
    {
        var validJson = """
        {
          "schemaVersion": 1,
          "configurationVersion": "1.0.0",
          "definitionVersion": "1.0.0",
          "modelVersion": "1.0.0",
          "oxidation": {
            "a_Nanometers": 100,
            "dryB0_NanometersSquaredPerMinute": 200,
            "wetB0_NanometersSquaredPerMinute": 800,
            "referenceTemperatureCelsius": 1000,
            "temperatureScaleCelsius": 150,
            "referencePressureTorr": 760,
            "siliconConsumptionRatio": 0.44,
            "maximumOxideThicknessNanometers": 2000,
            "version": "1.0.0"
          },
          "equipment": {
            "id": "educational-furnace",
            "limits": { "temperature": { "minimum": 800, "maximum": 1100, "unit": "°C" } },
            "allowedWaferDiametersMm": [200]
          },
          "quality": {
            "id": "default",
            "version": "1.0.0",
            "targets": [{ "metricId": "oxideThickness", "target": 70, "tolerancePercent": 10, "unit": "nm", "required": true }]
          },
          "enabledProcesses": ["WaferInitialization", "Oxidation"]
        }
        """;
        var loader = new SimulationConfigurationLoader();

        var valid = loader.Load(validJson);
        var unsupported = loader.Load(validJson.Replace("\"Oxidation\"]", "\"IonImplantation\"]", StringComparison.Ordinal));

        Assert.True(valid.IsValid);
        Assert.True(valid.Configuration!.Oxidation.ToOxidationCoefficients().A_Nanometers == 100);
        Assert.Contains(unsupported.Errors, issue => issue.Code == "CONFIG_UNIMPLEMENTED_PROCESS_ENABLED");
    }

    [Fact]
    public async Task SessionStoreRoundTripsTypedWaferStateAndRejectsUnknownSchema()
    {
        var state = new WaferStateFactory().Create(
            WaferInitializationParameters.EducationalDefaults(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var document = new SessionDocument(1, "1.0", "1.0", "1.0", DateTimeOffset.UtcNow, state.StateId, new[] { state }, Array.Empty<ProcessResult>());
        var path = Path.Combine(Path.GetTempPath(), $"semiconductor-session-{Guid.NewGuid():N}.json");
        var store = new SessionStore();

        try
        {
            await store.SaveAsync(path, document);
            var loaded = await store.LoadAsync(path);
            var invalid = await store.LoadAsync(path.Replace(".json", "-missing.json", StringComparison.Ordinal));

            Assert.True(loaded.IsValid);
            Assert.Equal(state.StateId, loaded.Document!.CurrentStateId);
            Assert.Equal(512, loaded.Document.States[0].CrossSection.Columns.Count);
            Assert.False(invalid.IsValid);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
