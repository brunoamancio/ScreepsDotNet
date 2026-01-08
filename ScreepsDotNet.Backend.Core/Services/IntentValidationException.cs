namespace ScreepsDotNet.Backend.Core.Services;

using System;

/// <summary>
/// Represents a user-facing validation failure when attempting to persist intents.
/// </summary>
public sealed class IntentValidationException(string message) : Exception(message);
