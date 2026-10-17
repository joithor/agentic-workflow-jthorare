using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>The provisioning client's own JSON context for Onyx's replies and the key request (used only through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(MeWire))]
[JsonSerializable(typeof(List<UserWire>))]
[JsonSerializable(typeof(KeyRequestWire))]
[JsonSerializable(typeof(KeyWire))]
internal sealed partial class OnyxProvisioningJsonContext : JsonSerializerContext;
