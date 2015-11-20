using System;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Vpdb.Download
{
	/// <summary>
	/// An interface that helps matching and sorting files based on their 
	/// flavor and the user's settings.
	/// </summary>
	/// <remarks>
	/// Note that every instance of this interface only handles one flavor 
	/// type.
	/// </remarks>
	public interface IFlavorMatcher
	{
		/// <summary>
		/// Returns true if either the primary or fallback flavor setting matches a given file.
		/// </summary>
		/// <param name="tableFile">File to check</param>
		/// <returns>True if file qualifies for download for given flavor</returns>
		bool Matches(TableFile tableFile);

		/// <summary>
		/// Returns a weight depending on if the primary or fallback flavor setting was matched for a given file.
		/// </summary>
		/// <param name="tableFile">File to weight</param>
		/// <returns>Weight relative to if the primary or fallback flavor setting was hit</returns>
		int Weight(TableFile tableFile);
	}

	/// <summary>
	/// Contains the general logic how to calculate weights.
	/// </summary>
	/// <typeparam name="TSettingFlavor">Flavor enum of the settings</typeparam>
	public abstract class FlavorMatcher<TSettingFlavor> : IFlavorMatcher
	{
		protected abstract TSettingFlavor PrimarySetting { get; }
		protected abstract TSettingFlavor FallbackSetting { get; }

		public bool Matches(TableFile tableFile)
		{
			return Weight(tableFile) > 0;
		}

		public int Weight(TableFile tableFile)
		{
			var weight = Weight(tableFile.Flavor, PrimarySetting) * 100;
			if (weight == 0) {
				weight = Weight(tableFile.Flavor, FallbackSetting);
			}
			return weight;
		}

		/// <summary>
		/// Computes a weight relative to how the flavor of a given file scores
		/// against the user setting.
		/// </summary>
		/// <remarks>
		/// Note that "universal" files result in "exact" matches (3).
		/// </remarks>
		/// <param name="fileFlavor">Flavor of the file</param>
		/// <param name="setting">Setting to match against</param>
		/// <returns>3 on exact match, 1 if matched by "any" and 0 if no match.</returns>
		private int Weight(Flavor fileFlavor, TSettingFlavor setting)
		{
			if (IsExactMatch(setting)) {
				return Matches(fileFlavor, setting) ? 3 : 0;
			}
			if (IsSameMatch(setting)) {
				return 0; // todo support updates
			}
			if (IsAnyMatch(setting)) {
				return 1;
			}
			throw new ArgumentOutOfRangeException(nameof(setting), setting, null);
		}

		/// <summary>
		/// Checks if the given setting can be matched in an "exact" way.
		/// </summary>
		/// <param name="setting">Setting to check</param>
		/// <returns>True if "exact" setting, false otherwise.</returns>
		protected abstract bool IsExactMatch(TSettingFlavor setting);

		/// <summary>
		/// Checks if the given setting is a "same" (i.e. same as previous file) matching setting.
		/// </summary>
		/// <param name="setting">Setting to check</param>
		/// <returns>True if "same" setting, false otherwise.</returns>
		protected abstract bool IsSameMatch(TSettingFlavor setting);

		/// <summary>
		/// Checks if the given setting is an "any" (i.e. doesn't matter) matching setting.
		/// </summary>
		/// <param name="setting">Setting to check</param>
		/// <returns>True if "any" setting, false otherwise.</returns>
		protected abstract bool IsAnyMatch(TSettingFlavor setting);

		/// <summary>
		/// Checks if a given file flavor matches a given setting.
		/// </summary>
		/// <seealso cref="IsExactMatch"/>
		/// <param name="fileFlavor">Flavor of the file to check</param>
		/// <param name="setting">Setting to check against (only <see cref="IsExactMatch"/> setting given)</param>
		/// <returns>True if file flavor matches, false otherwise.</returns>
		protected abstract bool Matches(Flavor fileFlavor, TSettingFlavor setting);
	}

	/// <summary>
	/// Matches orientation flavor.
	/// </summary>
	public class OrientationMatcher : FlavorMatcher<SettingsManager.Orientation>
	{
		protected override SettingsManager.Orientation PrimarySetting { get; }
		protected override SettingsManager.Orientation FallbackSetting { get; }

		public OrientationMatcher(Settings settings)
		{
			PrimarySetting = settings.DownloadOrientation;
			FallbackSetting = settings.DownloadOrientationFallback;
		}

		protected override bool Matches(Flavor fileFlavor, SettingsManager.Orientation setting)
		{
			switch (fileFlavor.Orientation) {
				case Flavor.OrientationValue.FS:
					return setting == SettingsManager.Orientation.Portrait;
				case Flavor.OrientationValue.WS:
					return setting == SettingsManager.Orientation.Landscape;
				case Flavor.OrientationValue.Any:
					return IsExactMatch(setting);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override bool IsExactMatch(SettingsManager.Orientation setting)
		{
			return setting == SettingsManager.Orientation.Landscape 
				|| setting == SettingsManager.Orientation.Portrait 
				|| setting == SettingsManager.Orientation.Universal;
		}

		protected override bool IsSameMatch(SettingsManager.Orientation setting)
		{
			return setting == SettingsManager.Orientation.Same;
		}

		protected override bool IsAnyMatch(SettingsManager.Orientation setting)
		{
			return setting == SettingsManager.Orientation.Any;
		}
	}

	/// <summary>
	/// Matches lighting flavor.
	/// </summary>
	public class LightingMatcher : FlavorMatcher<SettingsManager.Lighting>
	{
		protected override SettingsManager.Lighting PrimarySetting { get; }
		protected override SettingsManager.Lighting FallbackSetting { get; }

		public LightingMatcher(Settings settings)
		{
			PrimarySetting = settings.DownloadLighting;
			FallbackSetting = settings.DownloadLightingFallback;
		}

		protected override bool Matches(Flavor fileFlavor, SettingsManager.Lighting setting)
		{
			switch (fileFlavor.Lighting) {
				case Flavor.LightingValue.Day:
					return setting == SettingsManager.Lighting.Day;
				case Flavor.LightingValue.Night:
					return setting == SettingsManager.Lighting.Night;
				case Flavor.LightingValue.Any:
					return IsExactMatch(setting);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override bool IsExactMatch(SettingsManager.Lighting setting)
		{
			return setting == SettingsManager.Lighting.Day 
				|| setting == SettingsManager.Lighting.Night 
				|| setting == SettingsManager.Lighting.Universal;
		}

		protected override bool IsSameMatch(SettingsManager.Lighting setting)
		{
			return setting == SettingsManager.Lighting.Same;
		}

		protected override bool IsAnyMatch(SettingsManager.Lighting setting)
		{
			return setting == SettingsManager.Lighting.Any;
		}
	}
}
