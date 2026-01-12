namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class UserDocument
{
    [BsonId]
    [BsonElement("_id")]
    public string? Id { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("emailDirty")]
    public bool? EmailDirty { get; set; }

    [BsonElement("username")]
    public string? Username { get; set; }

    [BsonElement("cpu")]
    public double? Cpu { get; set; }

    [BsonElement("active")]
    public int? Active { get; set; }

    [BsonElement("bot")]
    public string? Bot { get; set; }

    [BsonElement("badge")]
    public Dictionary<string, object?>? Badge { get; set; }

    [BsonElement("password")]
    public string? Password { get; set; }

    [BsonElement("lastRespawnDate")]
    public DateTime? LastRespawnDate { get; set; }

    [BsonElement("notifyPrefs")]
    public Dictionary<string, object?>? NotifyPrefs { get; set; }

    [BsonElement("gcl")]
    public Dictionary<string, object?>? Gcl { get; set; }

    [BsonElement("lastChargeTime")]
    public DateTime? LastChargeTime { get; set; }

    [BsonElement("blocked")]
    public bool? Blocked { get; set; }

    [BsonElement("customBadge")]
    public Dictionary<string, object?>? CustomBadge { get; set; }

    [BsonElement("power")]
    public double? Power { get; set; }

    [BsonElement("money")]
    public double? Money { get; set; }

    [BsonElement("steam")]
    public UserSteamDocument? Steam { get; set; }

    [BsonElement("powerExperimentations")]
    public double? PowerExperimentations { get; set; }

    [BsonElement("powerExperimentationTime")]
    public double? PowerExperimentationTime { get; set; }

    [BsonElement("usernameLower")]
    public string? UsernameLower { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UserSteamDocument
{
    [BsonElement("id")]
    public string? Id { get; set; }

    [BsonElement("displayName")]
    public string? DisplayName { get; set; }

    [BsonElement("ownership")]
    public Dictionary<string, object?>? Ownership { get; set; }

    [BsonElement("steamProfileLinkHidden")]
    public bool? SteamProfileLinkHidden { get; set; }
}
