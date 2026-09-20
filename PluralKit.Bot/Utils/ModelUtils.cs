using PluralKit.Core;

namespace PluralKit.Bot;

public static class ModelUtils
{
    public static string DisplayName(this PKMember member) =>
        member.DisplayName ?? member.Name;

    public static string DisplayHid(this PKSystem system, SystemConfig? cfg = null, bool isList = false) => HidTransform(system.Hid, cfg, isList);
    public static string DisplayHid(this PKGroup group, SystemConfig? cfg = null, bool isList = false, bool shouldPad = true) => HidTransform(group.Hid, cfg, isList, shouldPad);
    public static string DisplayHid(this PKMember member, SystemConfig? cfg = null, bool isList = false, bool shouldPad = true) => HidTransform(member.Hid, cfg, isList, shouldPad);
    private static string HidTransform(string hid, SystemConfig? cfg = null, bool isList = false, bool shouldPad = true) =>
        HidUtils.HidTransform(
            hid,
            cfg != null && cfg.HidDisplaySplit,
            cfg != null && cfg.HidDisplayCaps,
            isList && shouldPad ? (cfg?.HidListPadding ?? SystemConfig.HidPadFormat.None) : SystemConfig.HidPadFormat.None // padding only on lists
        );
}