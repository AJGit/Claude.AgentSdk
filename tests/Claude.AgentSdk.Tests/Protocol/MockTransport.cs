using System.Text.Json;
using SdkMockTransport = Claude.AgentSdk.Transport.MockTransport;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
///     A mock implementation of ITransport for testing QueryHandler message processing.
///     This is a thin wrapper around the SDK's <see cref="SdkMockTransport" /> for backward compatibility.
/// </summary>
internal class MockTransport : SdkMockTransport
{
    /// <summary>
    ///     Get all written messages as JsonElements.
    /// </summary>
    /// <remarks>
    ///     This method exists for backward compatibility with existing tests.
    ///     Prefer using <see cref="SdkMockTransport.WrittenMessages" /> directly.
    /// </remarks>
    public IReadOnlyList<JsonElement> GetAllWrittenMessagesAsJson()
    {
        return WrittenMessages;
    }
}
