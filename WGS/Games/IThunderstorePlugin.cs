namespace WGS.Games;

/// <summary>
/// A game whose mods can be installed from Thunderstore. Only games whose dedicated server has been verified with
/// the BepInEx install rules implement this.
/// </summary>
public interface IThunderstorePlugin
{
    ThunderstoreGame Thunderstore { get; }
}

/// <param name="Community">Thunderstore community identifier ("valheim").</param>
/// <param name="LoaderPackage">The mod loader package, "Namespace-Name".</param>
/// <param name="LoaderRootFolder">Folder inside the loader zip whose contents go to the server root.</param>
public sealed record ThunderstoreGame(string Community, string LoaderPackage, string LoaderRootFolder);
