using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Host.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The terminal adapter over a scripted keyboard: the same contract, without a terminal.
[Lane(TestLane.Unit)]
public sealed class ConsoleOwnerConsoleContractTests : OwnerConsoleContract
{
    protected override OwnerConsoleRig Create() => new ConsoleRig();

    private sealed class ConsoleRig : OwnerConsoleRig, IDisposable
    {
        private readonly StringWriter _terminal = new();
        private readonly ScriptedTerminalInput _keyboard = new();

        public ConsoleRig()
        {
            Console = new ConsoleOwnerConsole(_terminal, _keyboard);
        }

        public override IOwnerConsole Console { get; }

        public override string Terminal => _terminal.ToString();

        public override void Feed(params string?[] answers) => _keyboard.Feed(answers);

        public void Dispose() => _terminal.Dispose();
    }
}
