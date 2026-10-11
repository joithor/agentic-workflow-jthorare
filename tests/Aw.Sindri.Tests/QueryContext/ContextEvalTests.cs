using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.QueryContext;
using Aw.Sindri.Infrastructure.Context;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Context;
using Aw.Sindri.Tests.Context;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.QueryContext;

/// <summary>
/// The eval harness (factory addendum §17.3 Proof): a fixed question set with the documents a good answer cites, run
/// through the real planner and the real code and notes adapters over a fixture corpus. It prints recall@5 and
/// precision@5 per question and gates the means, so a ranking change that loses recall fails here.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextEvalTests : IDisposable
{
    internal const int K = 5;
    internal const double RecallBar = 0.9;
    internal const double PrecisionBar = 0.3;
    internal const double TopOneBar = 0.8;

    private static readonly (string Question, string[] Expected)[] _cases =
    [
        ("how is the host lease fenced by its epoch", ["src/Lease/HostLease.cs", "2026-10-01-standup.md"]),
        ("ledger idempotency key for appended events", ["src/Ledger/EventLedger.cs"]),
        ("approve the yaml profile by its hash", ["src/Profile/ProfileLoader.cs"]),
        ("heartbeat cadence of the scheduler", ["src/Scheduler/Heartbeat.cs"]),
        ("dark mode for the desktop window", ["ideas.md"]),
        ("recover after a kill and restart", ["docs/runbook.md"]),
    ];

    private readonly TempTree _repo = new();
    private readonly TempTree _notes = new();

    public ContextEvalTests()
    {
        _ = _repo.Write("src/Lease/HostLease.cs", "// The host lease: one holder, an epoch that fencing checks on every write.\nclass HostLease { long Epoch; void Renew() { } }\n");
        _ = _repo.Write("src/Ledger/EventLedger.cs", "// Appended events carry an idempotency key; a replayed key writes nothing.\nclass EventLedger { void Append() { } }\n");
        _ = _repo.Write("src/Profile/ProfileLoader.cs", "// Loads the yaml profile and checks its hash before approve.\nclass ProfileLoader { }\n");
        _ = _repo.Write("src/Scheduler/Heartbeat.cs", "// The heartbeat service runs on the scheduler at its cadence, one slot at a time.\nclass Heartbeat { }\n");
        _ = _repo.Write("docs/runbook.md", "# Runbook\nAfter a kill -9, restart sindri: it can recover the lease at once when the holder is dead.\n");
        _ = _repo.Write("README.md", "# App\nA small app.\n");

        // Distractors: each shares a term or two with a question but isn't what it asks about.
        _ = _repo.Write("src/Lease/LeaseRenewer.cs", "// Renews the lease every twenty seconds.\nclass LeaseRenewer { }\n");
        _ = _repo.Write("src/Ledger/LedgerReader.cs", "// Reads events from the ledger in order.\nclass LedgerReader { }\n");
        _ = _repo.Write("src/Profile/ProfileWatcher.cs", "// Watches the profile file for changes.\nclass ProfileWatcher { }\n");
        _ = _repo.Write("src/Scheduler/Slots.cs", "// The scheduler skips missed slots after a sleep.\nclass Slots { }\n");
        _ = _repo.Write("docs/desktop.md", "# Desktop\nThe desktop window shows status.\n");
        _repo.GitAddAll();
        _ = _notes.Write("2026-10-01-standup.md", "# Standup\nDecided: fence ledger writes by the lease epoch.\n");
        _ = _notes.Write("ideas.md", "# Ideas\n- dark mode for the desktop window\n- a tray icon\n");
        _ = _notes.Write("deploy.md", "# Deploy\nShip on Fridays only after the evidence pack is green.\n");
    }

    public void Dispose()
    {
        _repo.Dispose();
        _notes.Dispose();
    }

    [Fact]
    public async Task The_fixed_question_set_meets_the_recall_and_precision_bars()
    {
        var service = await Service();
        var scores = new List<EvalScore>();
        var topOne = 0;
        foreach (var (question, expected) in _cases)
        {
            var answer = (await service.Handle(new QueryContextRequest(question), TestContext.Current.CancellationToken)).Value;
            var score = EvalScore.Of(expected, [.. answer.Results.Select(r => r.Cite.Document)], K);
            scores.Add(score);
            topOne += answer.Results.Count > 0 && EvalScore.Of(expected, [answer.Results[0].Cite.Document], 1).Precision == 1 ? 1 : 0;
            TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"recall@{K} {score.Recall:0.00}  precision@{K} {score.Precision:0.00}  {question}"));
        }

        var mean = EvalScore.Mean(scores);
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"mean recall@{K} {mean.Recall:0.00}  precision@{K} {mean.Precision:0.00} over {scores.Count} questions"));
        var topOneShare = (double)topOne / scores.Count;
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"top-1 correct {topOneShare:0.00}"));
        Assert.True(topOneShare >= TopOneBar, string.Create(CultureInfo.InvariantCulture, $"top-1 share {topOneShare:0.00} is under the bar {TopOneBar}"));
        Assert.True(mean.Recall >= RecallBar, string.Create(CultureInfo.InvariantCulture, $"mean recall@{K} {mean.Recall:0.00} is under the bar {RecallBar}"));
        Assert.True(mean.Precision >= PrecisionBar, string.Create(CultureInfo.InvariantCulture, $"mean precision@{K} {mean.Precision:0.00} is under the bar {PrecisionBar}"));
    }

    private async Task<QueryContextService> Service()
    {
        var profiles = new InMemoryProfiles();
        var profile = ProfileRules.Check(new SindriProfile(1, "eval", Repos: [new RepoEntry("app", _repo.Path)],
            Context: new ContextSettings(new CodeContext(), new NotesContext(_notes.Path), Limit: K))).Value;
        _ = await profiles.Save(new LoadedProfile(profile, ProfileHashing.Of("{}"u8), "eval"), TestContext.Current.CancellationToken);
        var catalog = new FakeSourceCatalog()
            .With(new GitGrepCodeSource([new CodeRepo("app", _repo.Path)]))
            .With(new NotesDirectorySource(_notes.Path));
        return new QueryContextService(profiles, catalog, new RedactionScrubber(Redactor.Default), new FakeTimeProvider(KnownInstants.Epoch));
    }
}
