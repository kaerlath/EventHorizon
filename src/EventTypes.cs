using System.Numerics;

namespace EventHorizon;

public static class EventTypes
{
    public sealed record Category(string Code, string Name, Vector4 Color);
    public static readonly Category[] All = [
        new("SE-OE", "Seasonal Event · Original", new(1f,.32f,.68f,1)),
        new("SE-RE", "Seasonal Event · Recurring", new(1f,.79f,.20f,1)),
        new("SE-SE", "Seasonal Event · Special", new(.25f,.68f,1f,1)),
        new("GE", "Game Event", new(.55f,.65f,1f,1)),
        new("CE", "Community Event", new(.35f,.85f,.68f,1)),
        new("SO", "Social Event", new(.82f,.55f,1f,1)),
        new("DM", "DM’d Event", new(1f,.48f,.32f,1)),
        new("FC", "Free Company Event", new(.25f,.82f,.9f,1)),
        new("PE", "Personal Event", new(.77f,.79f,.84f,1))
    ];
    public static Category For(EventRecord item) => All.FirstOrDefault(c => c.Code ==
        (item.PersonalOnly ? "PE" : item.EventType == "PE" ? "CE" : item.EventType)) ?? All[item.InformationOnly ? 3 : 4];
}
