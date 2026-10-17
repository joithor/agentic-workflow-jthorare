using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxProvisioning;

[Lane(TestLane.Unit)]
public sealed class FakeOnyxProvisioningContractTests : ProvisioningContract
{
    protected override ProvisioningRig Create(bool ownerIsAdmin) => new FakeRig(new FakeOnyxProvisioning { OwnerIsAdmin = ownerIsAdmin });

    private sealed class FakeRig(FakeOnyxProvisioning fake) : ProvisioningRig
    {
        public override IOnyxProvisioning Client => fake;

        public override string Email => fake.OwnerEmail;

        public override string Password => fake.OwnerPassword;
    }
}
