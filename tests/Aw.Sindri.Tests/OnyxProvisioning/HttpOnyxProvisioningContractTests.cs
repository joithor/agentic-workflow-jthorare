using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxProvisioning;

// The HTTP client over a handler that behaves like Onyx v4.9.0; nothing listens on a socket.
[Lane(TestLane.Unit)]
public sealed class HttpOnyxProvisioningContractTests : ProvisioningContract
{
    protected override ProvisioningRig Create(bool ownerIsAdmin) => new HttpRig(new FakeOnyxAdmin { OwnerIsAdmin = ownerIsAdmin });

    private sealed class HttpRig : ProvisioningRig
    {
        private readonly FakeOnyxAdmin _admin;
        private readonly HttpClient _http;

        public HttpRig(FakeOnyxAdmin admin)
        {
            _admin = admin;
            _http = new HttpClient(admin);
            Client = new HttpOnyxProvisioning(_http);
        }

        public override IOnyxProvisioning Client { get; }

        public override string Email => _admin.OwnerEmail;

        public override string Password => _admin.OwnerPassword;

        public override void Dispose()
        {
            _http.Dispose();
            _admin.Dispose();
            base.Dispose();
        }
    }
}
