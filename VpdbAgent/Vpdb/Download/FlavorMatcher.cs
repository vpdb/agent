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
		/// <param name="currentFile">The current/previous file of the release or null if new release</param>
		/// <returns>True if file qualifies for download for given flavor</returns>
		bool Matches(VpdbTableFile tableFile, VpdbTableFile currentFile);

		/// <summary>
		/// Returns a weight depending on if the primary or fallback flavor setting was matched for a given file.
		/// </summary>
		/// <param name="tableFile">File to weight</param>
		/// <param name="currentFile">The current/previous file of the release or null if new release</param>
		/// <returns>Weight relative to if the primary or fallback flavor setting was hit</returns>
		int Weight(VpdbTableFile tableFile, VpdbTableFile currentFile);
	}

	/// <summary>
	/// Contains the general logic how to calculate weights.
	/// </summary>
	/// <typeparam name="TSettingFlavor">Flavor enum of the settings</typeparam>
	public abstract class FlavorMatcher<TSettingFlavor> : IFlavorMatcher
	{
		protected abstract TSettingFlavor PrimarySetting { get; }
		protected abstract TSettingFlavor FallbackSetting { get; }

		public bool Matches(VpdbTableFile tableFile, VpdbTableFile currentFile)
		{
			return Weight(tableFile, currentFile) > 0;
		}

		public int Weight(VpdbTableFile tableFile, VpdbTableFile currentFile)
		{
			var weight = Weight(tableFile.Flavor, PrimarySetting, currentFile?.Flavor, null) * 100;
			if (weight == 0) {
				weight = Weight(tableFile.Flavor, FallbackSetting, currentFile?.Flavor, PrimarySetting);
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
		/// <param name="previousFlavor">If an update, this is the flavor of the previous file</param>
		/// <param name="primarySetting">If fallback, this is the primary setting. WTF C# why can't you accept null for a typed param?</param>
		/// <returns>3 on exact match, 1 if matched by "any" and 0 if no match.</returns>
		private int Weight(VpdbFlavor fileFlavor, TSettingFlavor setting, VpdbFlavor previousFlavor, object primarySetting)
		{
			if (MustMatchExactly(setting)) {
				return Matches(fileFlavor, setting) ? 3 : 0;
			}
			if (MustMatchSame(setting)) {
				if (previousFlavor != null) {
					return Matches(fileFlavor, previousFlavor) ? 3 : 0;
				}
				if (primarySetting != null) {
					return Matches(fileFlavor, (TSettingFlavor)primarySetting) ? 3 : 0;
				}
				return 0;
			}
			if (MustMatchAny(setting)) {
				return 1;
			}
			throw new ArgumentOutOfRangeException(nameof(setting), setting, null);
		}

		/// <summary>
		/// Checks if the given setting can be matched in an "exact" way.
		/// </summary>
		/// <param name="setting">Setting to check</param>
		/// <returns>True if "exact" setting, false otherwise.</returns>
		protected abstract bool MustMatchExactly(TSettingFlavor setting);

		/// <summary>
		/// Checks if the given setting is a "same" (i.e. same as previous file) matching setting.
		/// </summary>
		/// <param name="setting">Setting to check</param>
		/// <returns>True if "same" setting, false otherwise.</returns>
		protected abstract bool MustMatchSame(TSettingFlavor setting);

		/// <summary>
		/// Checks if the given setting is an "any" (i.e. doesn't matter) matching setting.
		/// </summary>
		/// <param name="setting">Setting to check</param>
		/// <returns>True if "any" setting, false otherwise.</returns>
		protected abstract bool MustMatchAny(TSettingFlavor setting);

		/// <summary>
		/// Checks if a given file flavor matches a given setting.
		/// </summary>
		/// <seealso cref="MustMatchExactly"/>
		/// <param name="fileFlavor">Flavor of the file to check</param>
		/// <param name="setting">Setting to check against (only <see cref="MustMatchExactly"/> setting given)</param>
		/// <returns>True if file flavor matches, false otherwise.</returns>
		protected abstract bool Matches(VpdbFlavor fileFlavor, TSettingFlavor setting);

		/// <summary>
		/// Checks if two flavors are the same.
		/// </summary>
		/// <param name="firstFlavor">First flavor</param>
		/// <param name="secondFlavor">Second flavpr</param>
		/// <returns></returns>
		protected abstract bool Matches(VpdbFlavor firstFlavor, VpdbFlavor secondFlavor);
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

		protected override bool Matches(VpdbFlavor fileFlavor, SettingsManager.Orientation setting)
		{
			switch (fileFlavor.Orientation) {
				case VpdbFlavor.VpdbOrientation.FS:
					return setting == SettingsManager.Orientation.Portrait;
				case VpdbFlavor.VpdbOrientation.WS:
					return setting == SettingsManager.Orientation.Landscape;
				case VpdbFlavor.VpdbOrientation.Any:
					return MustMatchExactly(setting);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override bool Matches(VpdbFlavor firstFlavor, VpdbFlavor secondFlavor)
		{
			return firstFlavor.Orientation == secondFlavor.Orientation;
		}

		protected override bool MustMatchExactly(SettingsManager.Orientation setting)
		{
			return setting == SettingsManager.Orientation.Landscape 
				|| setting == SettingsManager.Orientation.Portrait 
				|| setting == SettingsManager.Orientation.Universal;
		}

		protected override bool MustMatchSame(SettingsManager.Orientation setting)
		{
			return setting == SettingsManager.Orientation.Same;
		}

		protected override bool MustMatchAny(SettingsManager.Orientation setting)
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

		protected override bool Matches(VpdbFlavor fileFlavor, SettingsManager.Lighting setting)
		{
			switch (fileFlavor.Lighting) {
				case VpdbFlavor.VpdbLighting.Day:
					return setting == SettingsManager.Lighting.Day;
				case VpdbFlavor.VpdbLighting.Night:
					return setting == SettingsManager.Lighting.Night;
				case VpdbFlavor.VpdbLighting.Any:
					return MustMatchExactly(setting);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override bool Matches(VpdbFlavor firstFlavor, VpdbFlavor secondFlavor)
		{
			return firstFlavor.Lighting == secondFlavor.Lighting;
		}

		protected override bool MustMatchExactly(SettingsManager.Lighting setting)
		{
			return setting == SettingsManager.Lighting.Day 
				|| setting == SettingsManager.Lighting.Night 
				|| setting == SettingsManager.Lighting.Universal;
		}

		protected override bool MustMatchSame(SettingsManager.Lighting setting)
		{
			return setting == SettingsManager.Lighting.Same;
		}

		protected override bool MustMatchAny(SettingsManager.Lighting setting)
		{
			return setting == SettingsManager.Lighting.Any;
		}
	}
}
