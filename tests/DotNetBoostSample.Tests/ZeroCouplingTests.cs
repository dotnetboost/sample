using Xunit;
using System.Reflection;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;

namespace DotNetBoostSample.Tests;

/// <summary>
/// The sample's central claim, as tests rather than as a sentence in a README: the three
/// libraries are independent, and the only thing joining them is this application's own code.
/// <para>
/// A README decays silently. A failing build does not.
/// </para>
/// </summary>
public class ZeroCouplingTests
{
    private static readonly Assembly SettingsCore     = typeof(ISettingManager).Assembly;
    private static readonly Assembly MediaCore        = typeof(IMediaManager).Assembly;
    private static readonly Assembly MultiLingualCore = typeof(IMultiLingualManager).Assembly;

    private static readonly Assembly[] All = [SettingsCore, MediaCore, MultiLingualCore];

    [Fact]
    public void No_library_references_another_library()
    {
        var names = All.Select(a => a.GetName().Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in All)
        {
            var offenders = assembly.GetReferencedAssemblies()
                .Select(r => r.Name!)
                .Where(n => names.Contains(n) && !string.Equals(n, assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(
                offenders.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", offenders)}. "
                + "The libraries are meant to be independent; if one now needs another, that is a "
                + "design decision to make deliberately, not to discover here.");
        }
    }

    [Fact]
    public void No_library_references_a_shared_dotnetboost_kernel()
    {
        // There is no DotNetBoost.Core or DotNetBoost.Abstractions today. If one is introduced,
        // this test should be updated deliberately — the point is that nobody adds a mandatory
        // shared package by accident.
        foreach (var assembly in All)
        {
            var kernels = assembly.GetReferencedAssemblies()
                .Select(r => r.Name!)
                .Where(n => n.Equals("DotNetBoost.Core", StringComparison.OrdinalIgnoreCase)
                         || n.Equals("DotNetBoost.Abstractions", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(
                kernels.Count == 0,
                $"{assembly.GetName().Name} now depends on {string.Join(", ", kernels)}.");
        }
    }

    [Fact]
    public void Each_library_brings_its_own_entity_addressing_rather_than_a_shared_one()
    {
        // Media and MultiLingual each declare their own IEntityKeyAccessor, in their own
        // namespace. Two interfaces, same name, same job — the duplication that pays for the
        // independence the tests above assert. Recorded here so the trade-off is visible.
        var mediaAccessor = MediaCore.GetType("DotNetBoost.Media.Core.Interfaces.IEntityKeyAccessor");
        var mlAccessor    = MultiLingualCore.GetType("DotNetBoost.MultiLingual.Core.Interfaces.IEntityKeyAccessor");

        Assert.NotNull(mediaAccessor);
        Assert.NotNull(mlAccessor);
        Assert.NotEqual(mediaAccessor, mlAccessor);
    }
}
