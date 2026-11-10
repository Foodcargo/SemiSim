namespace SemiconductorEdu.Domain.Equipment;

public sealed record EquipmentSnapshot(
    bool DoorClosed,
    bool EmergencyStop,
    bool Available,
    IReadOnlySet<int> AllowedWaferDiametersMm);

public sealed record EquipmentProfile(
    string Id,
    IReadOnlyDictionary<string, (double Minimum, double Maximum, string Unit)> Limits,
    IReadOnlySet<int> AllowedWaferDiametersMm);
