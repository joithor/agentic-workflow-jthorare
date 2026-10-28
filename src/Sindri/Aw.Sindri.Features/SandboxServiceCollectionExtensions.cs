using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.EndLearnMode;
using Aw.Sindri.Features.ListDenials;
using Aw.Sindri.Features.PlanSandbox;
using Aw.Sindri.Features.RecordLimitHit;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the sandbox slices (sandbox spec §12, SB-1 onward).</summary>
internal static class SandboxServiceCollectionExtensions
{
    /// <summary>Adds PlanSandbox, the plan builder behind 'sindri sandbox show' and 'explain'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPlanSandbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IService<PlanSandboxRequest, PlanSandboxResponse>, PlanSandboxService>();
    }

    /// <summary>Adds RecordLimitHit, the issue 'session wrap' makes when a limit is reached that does not end the session (proven by the launch secret).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecordLimitHit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<RecordLimitHitRequest>, AcceptAllValidator<RecordLimitHitRequest>>().AddIssue<RecordLimitHitRequest, RecordLimitHitResponse, RecordLimitHitService>();
    }

    /// <summary>Adds EndLearnMode, the desk command behind 'sindri sandbox learn off'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddEndLearnMode(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<EndLearnModeRequest>, AcceptAllValidator<EndLearnModeRequest>>().AddCommand<EndLearnModeRequest, CommandAccepted, EndLearnModeService>();
    }

    /// <summary>Adds ListDenials, the query behind 'sindri sandbox denials'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddListDenials(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<ListDenialsRequest>, AcceptAllValidator<ListDenialsRequest>>().AddQuery<ListDenialsRequest, ListDenialsResponse, ListDenialsService>();
    }

    /// <summary>Adds CheckSandbox, the query behind 'sindri sandbox check' and Check again, with the checks it runs and doctor's sandbox rows (SB-5a).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddCheckSandbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<CheckSandboxRequest>, AcceptAllValidator<CheckSandboxRequest>>().AddQuery<CheckSandboxRequest, CheckSandboxResponse, CheckSandboxService>()
            .AddScoped<ISandboxChecks, SandboxChecks>().AddScoped<SandboxResumeList>()
            .AddScoped<ISandboxFormCheck, SandboxFormCheck>().AddScoped<ISandboxLoginCheck, SandboxLoginCheck>()
            .AddScoped<ISandboxParks, SandboxParkedInstances>().AddScoped<ISandboxSlots, SandboxSlots>()
            .AddSingleton<ISandboxFixture, NoSandboxFixture>().AddSingleton<ISandboxMachine, ProcessSandboxMachine>()
            .AddScoped<IDoctorChecks, SandboxDoctorChecks>();
    }
}
