using SemiconductorEdu.Application.Execution;
using SemiconductorEdu.Domain.Equipment;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Infrastructure.Storage;
using SemiconductorEdu.Simulation.Initialization;
using SemiconductorEdu.Simulation.Oxidation;

namespace SemiconductorEdu.IntegrationTests;

public sealed class SessionRecoveryTests
{
    private static ProcessSession CreateSession() => new(new Dictionary<ProcessType, IProcessExecutor>
    {
        [ProcessType.Oxidation] = new OxidationProcessExecutor(new OxidationModel(),
            new EquipmentSnapshot(true, false, true, new HashSet<int> { 200 }),
            new QualityProfile("default", "1", new[] { new QualityTarget("oxideThickness", 70, 10, null, null, null, "nm", true) }))
    });

    [Fact]
    public async Task SavedRunRestoresTypedRecipeHistoryAndUndoParent()
    {
        var session = CreateSession();
        var initial = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        session.Initialize(initial);
        var recipe = new ProcessRecipe(Guid.NewGuid(), ProcessType.Oxidation, OxidationParameters.EducationalDefaults(), "furnace", "default", "1", "1");
        session.Execute(recipe, DateTimeOffset.UtcNow);
        var snapshot = session.Capture();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new SessionStore();
            await store.SaveAsync(path, new(1, "1.0.0", "1.0.0", "1.0.0", DateTimeOffset.UtcNow, snapshot.CurrentStateId, snapshot.States, snapshot.History));
            var loaded = await store.LoadAsync(path);
            Assert.True(loaded.IsValid, string.Join(",", loaded.Errors.Select(e => e.Code)));
            var restored = CreateSession();
            var document = loaded.Document!;
            restored.Restore(new(document.CurrentStateId, document.States, document.History));
            Assert.Equal(recipe, restored.History.Single().Recipe);
            Assert.IsType<OxidationParameters>(restored.History.Single().Recipe!.Parameters);
            Assert.True(restored.Undo());
            Assert.Equal(initial.StateId, restored.CurrentState!.StateId);
            Assert.Single(restored.History);
            Assert.True(restored.Redo());
            Assert.Equal(snapshot.CurrentStateId, restored.CurrentState!.StateId);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"states\":null,\"history\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"states\":[null],\"history\":[]}")]
    [InlineData("{\"schemaVersion\":999,\"states\":[],\"history\":[]}")]
    [InlineData("{\"unknownField\":true}")]
    public async Task MalformedDocumentsReturnErrors(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllTextAsync(path, json);
            var loaded = await new SessionStore().LoadAsync(path);
            Assert.False(loaded.IsValid);
            Assert.NotEmpty(loaded.Errors);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidRestoreLeavesExistingSessionUnchanged()
    {
        var session = CreateSession();
        var initial = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        session.Initialize(initial);
        Assert.Throws<ArgumentException>(() => session.Restore(new(Guid.NewGuid(), new[] { initial }, Array.Empty<ProcessResult>())));
        Assert.Equal(initial, session.CurrentState);
    }

    [Fact]
    public async Task CancelledSavePreservesExistingFileAndRemovesTemporaryFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var initial = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var store = new SessionStore();
        var document = new SessionDocument(1, "1", "1", "1", DateTimeOffset.UtcNow, initial.StateId, new[] { initial }, Array.Empty<ProcessResult>());
        try
        {
            await store.SaveAsync(path, document);
            var original = await File.ReadAllTextAsync(path);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(path, document, new CancellationToken(true)));
            Assert.Equal(original, await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".*.tmp"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void BrokenStateAncestryIsRejected()
    {
        var initial = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var broken = initial with { ParentStateId = initial.StateId };
        var result = SessionStore.Validate(new(1, "1", "1", "1", DateTimeOffset.UtcNow, broken.StateId, new[] { broken }, Array.Empty<ProcessResult>()));
        Assert.Contains(result.Errors, e => e.Code == "SESSION_ANCESTRY_INVALID");
    }
}
