using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Registration;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The engine as AddSindriCore composes it (the tools faked), over the in-memory lease, workflow store and recipe catalog, with a test gate.</summary>
internal sealed class WorkflowHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public WorkflowHarness(params IBlock[] extra)
    {
        Lease = new InMemorySindriStore(Clock, Probe);
        Store = new InMemoryWorkflowStore(Lease);
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton(SliceHarness.Json)
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSindriCore()
            .AddSingleton<IJudgeClient>(Judge)
            .AddSingleton<IUiEvidence>(UiEvidence)
            .AddSingleton<IBlock>(Model)
            .AddSingleton<IWorkflowStore>(Store)
            .AddSingleton<IRecipeCatalog>(static sp => sp.GetRequiredService<InMemoryRecipeCatalog>())
            .AddSingleton<InMemoryRecipeCatalog>();
        foreach (var block in extra)
        {
            _ = services.AddSingleton(block);
        }

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Catalog = _provider.GetRequiredService<InMemoryRecipeCatalog>();
        Add(TestRecipes.Loop);
        Add(TestRecipes.Parent);
    }

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public FakeProcessProbe Probe { get; } = new();

    public InMemorySindriStore Lease { get; }

    public InMemoryWorkflowStore Store { get; }

    public InMemoryRecipeCatalog Catalog { get; }

    public TestGate Model { get; } = new("gate.test.model", GateRung.Model);

    public FakeJudgeClient Judge { get; } = new();

    public FakeUiEvidence UiEvidence { get; } = new();

    public EvidenceProviders Providers => _provider.GetRequiredService<EvidenceProviders>();

    public IBlockRegistry Blocks => _provider.GetRequiredService<IBlockRegistry>();

    public HeldLease Held => _provider.GetRequiredService<HeldLease>();

    public IWorkflowEngine Engine => _provider.CreateScope().ServiceProvider.GetRequiredService<IWorkflowEngine>();

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    public void Add(string yaml, bool approved = true)
    {
        var (recipe, hash) = TestRecipes.Read(yaml);
        Catalog.Add(recipe, hash, approved);
    }

    public async Task<LeaseGrant> HoldLease()
    {
        var grant = (await Lease.Acquire(Probe.Self(), Token)).Value;
        Held.Hold(grant);
        return grant;
    }

    public async Task<WorkflowInstance> Start(string recipe = "workflow.loop", string item = "ITEM-1", int attempt = 1) =>
        (await Engine.Start(RecipeId.From(recipe), ItemId.From(item), attempt, baseSha: null, Token)).Value.Instance;

    public async Task<WorkflowInstance> OnlyChild(InstanceId parent) => Assert.Single((await Store.Children(parent, Token)).Value);

    public Task<Kernel.Result<WorkflowOutcome>> Apply(InstanceId id, WorkflowCommand command) => Engine.Apply(id, command, Token).AsTask();

    public Task<Kernel.Result<WorkflowOutcome>> Signal(WorkflowInstance i, string on, params (string Name, string Value)[] outputs) =>
        Signal(i.Id, new WorkflowSignal(on, ExpectedVersion: null, Sha: null, outputs.ToDictionary(o => o.Name, o => o.Value, StringComparer.Ordinal), [], EmptyDiff: false, Spend.Zero));

    public async Task<Kernel.Result<WorkflowOutcome>> Signal(InstanceId id, WorkflowSignal signal) => await Engine.Signal(id, signal, Token);

    public async Task<WorkflowInstance> Get(InstanceId id) => (await Store.Get(id, Token)).Value!;   // why: tests ask only for instances they started
}
