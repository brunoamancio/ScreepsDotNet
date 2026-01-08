namespace ScreepsDotNet.Backend.Core.Services;

using System;

public sealed class PowerCreepValidationException(string message) : Exception(message);
