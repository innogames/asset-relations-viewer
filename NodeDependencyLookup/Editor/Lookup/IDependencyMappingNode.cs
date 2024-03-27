namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public interface IIdentifyable
	{
		string Id { get; }
	}

	/// <summary>
	/// A node that got resolved by one of the caches
	/// This could be for example an Asset, LocaKey, AssetBundle, etc.
	/// </summary>
	public interface IDependencyMappingNode : IIdentifyable
	{
		string Type { get; }
		string Key { get; }
	}
}