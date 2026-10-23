using Xunit;
using System.Reflection;
using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;

namespace DotNetBoostSample.Tests;

/// <summary>
/// The sample's central claim, as tests rather than as a sentence in a README: the four
/// libraries are independent, and the only thing joining them is this application's own code.
/// <para>
/// A README decays silently. A failing build does not.
/// </para>
/// <para>
/// The fourth assembly is the one that makes this worth re-reading. DotNetBoost.Auditing was added
/// to a solution the other three were already in, and the assertions below did not change — no new
/// package reference appeared between any of them, and no shared kernel arrived with it. What DID
/// change is in <c>Infrastructure/DependencyInjection.cs</c>: independence is not free, and this
/// installation pays for it by recording its own changes because two of these libraries cannot
/// both watch one <c>DbContext</c>. Independence asserted here; its price written down there.
/// </para>
/// </summary>
public class ZeroCouplingTests
{
    private static readonly Assembly SettingsCore     = typeof(ISettingManager).Assembly;
    private static readonly Assembly MediaCore        = typeof(IMediaManager).Assembly;
    private static readonly Assembly MultiLingualCore = typeof(IMultiLingualManager).Assembly;
    private static readonly Assembly AuditingCore     = typeof(IAuditManager).Assembly;

    private static readonly Assembly[] All =
        [SettingsCore, MediaCore, MultiLingualCore, AuditingCore];

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
        // Media, MultiLingual and Auditing each declare their own IEntityKeyAccessor, in their own
        // namespace. Three interfaces, same name, same job — the duplication that pays for the
        // independence the tests above assert, and it grew by one with the fourth library rather
        // than being consolidated. Recorded here so the trade-off stays visible.
        //
        // Three copies of a contract is the cost; KeyFormatAgreementTests is why it is affordable,
        // since all three implement one published addressing spec and say which version.
        var accessors = new[]
        {
            MediaCore.GetType("DotNetBoost.Media.Core.Interfaces.IEntityKeyAccessor"),
            MultiLingualCore.GetType("DotNetBoost.MultiLingual.Core.Interfaces.IEntityKeyAccessor"),
            AuditingCore.GetType("DotNetBoost.Auditing.Core.Interfaces.IEntityKeyAccessor")
        };

        Assert.DoesNotContain(null, accessors);
        Assert.Equal(accessors.Length, accessors.Distinct().Count());
    }
}
