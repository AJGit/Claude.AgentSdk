using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Tests for internal helper functions used by session listing/reading (path sanitization,
///     UUID validation, lite JSON field extraction, first-prompt extraction, Unicode
///     sanitization). Ported from the Python SDK's <c>TestHelpers</c> and
///     <c>TestSanitizeUnicode</c> test classes.
/// </summary>
[UnitTest]
public class SessionHelpersTests
{
    [Fact]
    public void ValidateUuid_Valid_ReturnsValue()
    {
        Assert.Equal(
            "550e8400-e29b-41d4-a716-446655440000",
            SessionPaths.ValidateUuid("550e8400-e29b-41d4-a716-446655440000"));
        Assert.Equal(
            "550E8400-E29B-41D4-A716-446655440000",
            SessionPaths.ValidateUuid("550E8400-E29B-41D4-A716-446655440000"));
    }

    [Fact]
    public void ValidateUuid_Invalid_ReturnsNull()
    {
        Assert.Null(SessionPaths.ValidateUuid("not-a-uuid"));
        Assert.Null(SessionPaths.ValidateUuid(""));
        Assert.Null(SessionPaths.ValidateUuid("550e8400-e29b-41d4-a716"));
    }

    [Fact]
    public void SanitizePath_Basic_ReplacesNonAlphanumeric()
    {
        Assert.Equal("-Users-foo-my-project", SessionPaths.SanitizePath("/Users/foo/my-project"));
        Assert.Equal("plugin-name-server", SessionPaths.SanitizePath("plugin:name:server"));
    }

    [Fact]
    public void SanitizePath_Long_TruncatesWithHashSuffix()
    {
        var longPath = string.Concat(Enumerable.Repeat("/x", 150)); // 300 chars
        var result = SessionPaths.SanitizePath(longPath);
        Assert.True(result.Length > 200);
        Assert.StartsWith("-x-x", result, StringComparison.Ordinal);
        Assert.Contains('-', result[200..]);
    }

    [Fact]
    public void SimpleHash_Deterministic()
    {
        Assert.Equal(SessionPaths.SimpleHash("hello"), SessionPaths.SimpleHash("hello"));
        Assert.NotEqual(SessionPaths.SimpleHash("hello"), SessionPaths.SimpleHash("world"));
    }

    [Fact]
    public void SimpleHash_EmptyString_ReturnsZero()
    {
        Assert.Equal("0", SessionPaths.SimpleHash(""));
    }

    [Fact]
    public void ExtractJsonStringField_Simple()
    {
        const string text = "{\"foo\":\"bar\",\"baz\":\"qux\"}";
        Assert.Equal("bar", TranscriptReader.ExtractJsonStringField(text, "foo"));
        Assert.Equal("qux", TranscriptReader.ExtractJsonStringField(text, "baz"));
        Assert.Null(TranscriptReader.ExtractJsonStringField(text, "missing"));
    }

    [Fact]
    public void ExtractJsonStringField_WithSpace()
    {
        const string text = "{\"foo\": \"bar\"}";
        Assert.Equal("bar", TranscriptReader.ExtractJsonStringField(text, "foo"));
    }

    [Fact]
    public void ExtractJsonStringField_Escaped()
    {
        const string text = "{\"foo\":\"bar\\\"baz\"}";
        Assert.Equal("bar\"baz", TranscriptReader.ExtractJsonStringField(text, "foo"));
    }

    [Fact]
    public void ExtractLastJsonStringField_ReturnsLastOccurrence()
    {
        const string text = "{\"summary\":\"first\"}\n{\"summary\":\"second\"}\n{\"summary\":\"third\"}";
        Assert.Equal("third", TranscriptReader.ExtractLastJsonStringField(text, "summary"));
    }

    [Fact]
    public void ExtractFirstPromptFromHead_Simple()
    {
        var head = "{\"type\":\"user\",\"message\":{\"content\":\"Hello!\"}}\n";
        Assert.Equal("Hello!", TranscriptReader.ExtractFirstPromptFromHead(head));
    }

    [Fact]
    public void ExtractFirstPromptFromHead_SkipsMeta()
    {
        var head =
            "{\"type\":\"user\",\"isMeta\":true,\"message\":{\"content\":\"meta\"}}\n" +
            "{\"type\":\"user\",\"message\":{\"content\":\"real prompt\"}}\n";
        Assert.Equal("real prompt", TranscriptReader.ExtractFirstPromptFromHead(head));
    }

    [Fact]
    public void ExtractFirstPromptFromHead_SkipsToolResult()
    {
        var head =
            "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"content\":\"x\"}]}}\n" +
            "{\"type\":\"user\",\"message\":{\"content\":\"actual prompt\"}}\n";
        Assert.Equal("actual prompt", TranscriptReader.ExtractFirstPromptFromHead(head));
    }

    [Fact]
    public void ExtractFirstPromptFromHead_ContentBlocks()
    {
        var head =
            "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"block prompt\"}]}}\n";
        Assert.Equal("block prompt", TranscriptReader.ExtractFirstPromptFromHead(head));
    }

    [Fact]
    public void ExtractFirstPromptFromHead_Truncates()
    {
        var longPrompt = new string('x', 300);
        var head = $"{{\"type\":\"user\",\"message\":{{\"content\":\"{longPrompt}\"}}}}\n";
        var result = TranscriptReader.ExtractFirstPromptFromHead(head);
        Assert.True(result.Length <= 201);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractFirstPromptFromHead_CommandFallback()
    {
        var head =
            "{\"type\":\"user\",\"message\":{\"content\":\"<command-name>/help</command-name>stuff\"}}\n";
        Assert.Equal("/help", TranscriptReader.ExtractFirstPromptFromHead(head));
    }

    [Fact]
    public void ExtractFirstPromptFromHead_Empty()
    {
        Assert.Equal("", TranscriptReader.ExtractFirstPromptFromHead(""));
        Assert.Equal("", TranscriptReader.ExtractFirstPromptFromHead("{\"type\":\"assistant\"}\n"));
    }

    [Fact]
    public void SanitizeUnicode_PassthroughCleanString()
    {
        Assert.Equal("hello", SessionMutationHelpers.SanitizeUnicode("hello"));
        Assert.Equal("tag-with-dashes_123", SessionMutationHelpers.SanitizeUnicode("tag-with-dashes_123"));
    }

    [Fact]
    public void SanitizeUnicode_StripsZeroWidth()
    {
        Assert.Equal("ab", SessionMutationHelpers.SanitizeUnicode("a​b"));
        Assert.Equal("ab", SessionMutationHelpers.SanitizeUnicode("a‌b"));
        Assert.Equal("ab", SessionMutationHelpers.SanitizeUnicode("a‍b"));
    }

    [Fact]
    public void SanitizeUnicode_StripsBom()
    {
        Assert.Equal("hello", SessionMutationHelpers.SanitizeUnicode("﻿hello"));
    }

    [Fact]
    public void SanitizeUnicode_StripsDirectionalMarks()
    {
        Assert.Equal("abc", SessionMutationHelpers.SanitizeUnicode("a‪b‬c"));
        Assert.Equal("abc", SessionMutationHelpers.SanitizeUnicode("a⁦b⁩c"));
    }

    [Fact]
    public void SanitizeUnicode_StripsPrivateUse()
    {
        Assert.Equal("ab", SessionMutationHelpers.SanitizeUnicode("ab"));
        Assert.Equal("ab", SessionMutationHelpers.SanitizeUnicode("ab"));
    }

    [Fact]
    public void SanitizeUnicode_NfkcNormalization()
    {
        // Fullwidth 'A' -> ASCII 'A'
        Assert.Equal("A", SessionMutationHelpers.SanitizeUnicode("Ａ"));
    }

    [Fact]
    public void SanitizeUnicode_IterativeConverges()
    {
        var result = SessionMutationHelpers.SanitizeUnicode("a" + new string('​', 20) + "b");
        Assert.Equal("ab", result);
    }
}
