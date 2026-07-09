namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// getAlbumList2 list types used by the Home view (requirements §6.2–§6.4).
/// Further types can be added when a requirement needs them.
/// </summary>
public enum AlbumListType
{
    /// <summary>Most played (Home "Most Played", §6.2).</summary>
    Frequent,

    /// <summary>Newest additions (Home "Recently Added", §6.3).</summary>
    Newest,

    /// <summary>Random albums (Home "Random Picks", §6.4).</summary>
    Random,
}
