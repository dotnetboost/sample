using DotNetBoost.Settings.Core.Attributes;
using FluentValidation;

namespace DotNetBoostSample.Application.Settings;

/// <summary>
/// How long the audit trail is kept, editable from the dashboard: the policy
/// <c>AuditRetentionService</c>, in the host, reads every time it decides whether to prune.
///
/// <para><b>Why this is only retention.</b> Everything else DotNetBoost.Auditing can be told is an
/// <c>AuditingOptions</c> value, and that object is read once, at <c>Build()</c> — its own
/// documentation says a change "takes a restart", with no <c>IOptionsMonitor</c> behind it. A
/// group here offering <c>Enabled</c> or <c>RecordDeletedValues</c> would be a form whose saves do
/// nothing until the process is replaced, which is worse than no form. Retention is the exception
/// because the library also takes it as an argument — <c>PruneOlderThanAsync(TimeSpan)</c> — and a
/// prune is something this application runs, so it can read the policy as it runs.</para>
///
/// <para><b>Off by default.</b> A prune is the one irreversible thing the library does, and a
/// sample that started deleting its own trail because someone ran it for a year would be the
/// wrong kind of helpful. Turned on, the trail older than <see cref="RetentionDays"/> goes.</para>
///
/// <para>Editing this group is itself recorded — <c>SettingGroupAuditObserver</c> writes one entry
/// per save, like any other group's — so who shortened the retention, and from what, is in the
/// trail the change applies to. A prune never removes that entry sooner than any other.</para>
/// </summary>
[SettingGroup("auditing")]
public class AuditingSettings
{
    /// <summary>Whether entries older than <see cref="RetentionDays"/> are deleted.</summary>
    public bool PruneEnabled { get; set; }

    /// <summary>How old an entry must be, in days, before a prune deletes it.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>How often, in hours, a prune runs while <see cref="PruneEnabled"/> is on.</summary>
    public int PruneIntervalHours { get; set; } = 24;
}

/// <summary>
/// Rules for the auditing group. <see cref="BrandingSettingsValidator"/> explains why these are
/// published as well as enforced.
/// </summary>
public sealed class AuditingSettingsValidator : AbstractValidator<AuditingSettings>
{
    /// <summary>The longest retention the form offers: ten years.</summary>
    public const int MaxRetentionDays = 3650;

    /// <summary>The longest gap between prunes: a week.</summary>
    public const int MaxPruneIntervalHours = 168;

    /// <summary>Creates the validator.</summary>
    public AuditingSettingsValidator()
    {
        // At least a day, and not only because PruneOlderThanAsync refuses zero — which it does,
        // since a zero age would delete the entry being written as it is written. A retention the
        // dashboard can set to "now" is a delete-everything button with a number field for a label.
        RuleFor(a => a.RetentionDays)
            .InclusiveBetween(1, MaxRetentionDays);

        // The service checks this group every minute, so an hour is the finest interval that
        // means what it says.
        RuleFor(a => a.PruneIntervalHours)
            .InclusiveBetween(1, MaxPruneIntervalHours);
    }
}
