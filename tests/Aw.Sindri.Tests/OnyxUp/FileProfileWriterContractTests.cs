using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The file writer over a real temp directory.
[Lane(TestLane.Integration)]
public sealed class FileProfileWriterContractTests : ProfileWriterContract
{
    protected override ProfileWriterRig Create(string? text) => new FileRig(text);

    private sealed class FileRig : ProfileWriterRig
    {
        private readonly string _home = Directory.CreateTempSubdirectory("sindri-profile-").FullName;
        private readonly string _path;

        public FileRig(string? text)
        {
            _path = Path.Combine(_home, "profile", "profile.yaml");
            if (text is not null)
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, text);
            }

            Writer = new FileProfileWriter(new SindriSettings(_home, _path), "thor");
        }

        public override IProfileWriter Writer { get; }

        public override string? Text
        {
            get => File.Exists(_path) ? File.ReadAllText(_path) : null;
            set => File.WriteAllText(_path, value);
        }

        public override void Dispose()
        {
            Directory.Delete(_home, recursive: true);
            base.Dispose();
        }
    }
}
