public static class ZoneSceneMap
{
    public static string GetSceneName(int zoneId) => zoneId switch
    {
        1 => "TownScene",
        2 => "DungeonScene",
        _ => "TownScene"
    };
}
