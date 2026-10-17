using System.Net;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>What an allowlisted request got back: the status, the body and the Set-Cookie header values.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Body">The body bytes.</param>
/// <param name="SetCookies">The Set-Cookie header values.</param>
internal sealed record ProvisioningReply(HttpStatusCode Status, byte[] Body, IReadOnlyList<string> SetCookies);
