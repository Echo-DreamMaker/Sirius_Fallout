using System.Linq;
using System.Text;
using Content.Shared._NC.Sponsor; // Forge-Change
using Content.Shared.Inventory;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Customization.Systems;


public sealed class CharacterRequirementsSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;


    public bool CheckRequirementValid(CharacterRequirement requirement, JobPrototype job,
        HumanoidCharacterProfile profile, Dictionary<string, TimeSpan> playTimes, bool whitelisted, IPrototype prototype,
        IEntityManager entityManager, IPrototypeManager prototypeManager, IConfigurationManager configManager,
        ISharedSponsorManager sponsorManager, out string? reason, int depth = 0, bool jobWhitelisted = false) // Forge-Change
    {
        var valid = requirement.IsValid(job, profile, playTimes, whitelisted, prototype,
            entityManager, prototypeManager, configManager, sponsorManager, // Forge-Change
            out reason, depth);

        if (!valid && IsGrandfatheredTimer(job, requirement, playTimes))
        {
            reason = null;
            valid = true;
        }
        else if (!valid && jobWhitelisted && requirement.CanBeBypassedByJobWhitelist(job))
        {
            reason = null;
            valid = true;
        }

        // Return false if the requirement is invalid and not inverted
        // If it's inverted return false when it's valid
        return !valid ? requirement.Inverted : !requirement.Inverted;
    }

    public bool CheckRequirementsValid(List<CharacterRequirement> requirements, JobPrototype job,
        HumanoidCharacterProfile profile, Dictionary<string, TimeSpan> playTimes, bool whitelisted, IPrototype prototype,
        IEntityManager entityManager, IPrototypeManager prototypeManager, IConfigurationManager configManager,
        ISharedSponsorManager sponsorManager, out List<string> reasons, int depth = 0, bool jobWhitelisted = false) // Forge-Change
    {
        reasons = new List<string>();
        var valid = true;

        foreach (var requirement in requirements)
        {
            var requirementValid = requirement.IsValid(job, profile, playTimes, whitelisted, prototype,
                entityManager, prototypeManager, configManager, sponsorManager, // Forge-Change
                out var reason, depth);

            if (!requirementValid && IsGrandfatheredTimer(job, requirement, playTimes))
            {
                reason = null;
                requirementValid = true;
            }
            else if (!requirementValid && jobWhitelisted && requirement.CanBeBypassedByJobWhitelist(job))
            {
                reason = null;
                requirementValid = true;
            }

            // Set valid to false if the requirement is invalid and not inverted
            // If it's inverted set valid to false when it's valid
            if (!requirementValid)
            {
                if (valid)
                    valid = requirement.Inverted;
            }
            else
            {
                if (valid)
                    valid = !requirement.Inverted;
            }

            if (reason != null)
                reasons.Add(reason);
        }

        return valid;
    }

    private static bool IsGrandfatheredTimer(JobPrototype job, CharacterRequirement requirement,
        Dictionary<string, TimeSpan> playTimes)
    {
        // Only bypass role-timer requirements. Species, sex, whitelist, and other
        // job restrictions remain enforced for grandfathered players.
        return playTimes.ContainsKey(PlayTimeTrackingShared.GrandfatheredRoleTracker(job.ID))
            && requirement.CanBeBypassedByJobWhitelist(job);
    }

    public bool CheckPlaytimeRequirementsVisible(List<CharacterRequirement> requirements, JobPrototype job,
        HumanoidCharacterProfile profile, Dictionary<string, TimeSpan> playTimes, bool whitelisted, IPrototype prototype,
        IEntityManager entityManager, IPrototypeManager prototypeManager, IConfigurationManager configManager,
        ISharedSponsorManager sponsorManager, out List<string> reasons, int depth = 0, bool jobWhitelisted = false) // #Misfits Change
    {
        var playtimeRequirements = requirements
            .Where(requirement => requirement.CanBeBypassedByJobWhitelist(job))
            .ToList();

        if (playtimeRequirements.Count == 0)
        {
            reasons = new List<string>();
            return true;
        }

        return CheckRequirementsValid(
            playtimeRequirements,
            job,
            profile,
            playTimes,
            whitelisted,
            prototype,
            entityManager,
            prototypeManager,
            configManager,
            sponsorManager,
            out reasons,
            depth,
            jobWhitelisted);
    }


    /// <summary>
    ///     Gets the reason text from <see cref="CheckRequirementsValid"/> as a <see cref="FormattedMessage"/>.
    /// </summary>
    public FormattedMessage GetRequirementsText(List<string> reasons)
    {
        // #Misfits Fix: Use permissive parser to handle any potential markup parse errors with non-ASCII characters
        return FormattedMessage.FromMarkupPermissive(GetRequirementsMarkup(reasons));
    }

    /// <summary>
    ///     Gets the reason text from <see cref="CheckRequirementsValid"/> as a markup string.
    /// </summary>
    public string GetRequirementsMarkup(List<string> reasons)
    {
        var text = new StringBuilder();
        foreach (var reason in reasons)
            text.Append($"\n{reason}");

        return text.ToString().Trim();
    }


    /// <summary>
    ///     Returns true if the given dummy can equip the given item.
    ///     Does not care if items are already in equippable slots, and ignores pockets.
    /// </summary>
    public bool CanEntityWearItem(EntityUid dummy, EntityUid clothing, bool bypassAccessCheck = false)
    {
        return _inventory.TryGetSlots(dummy, out var slots)
            && slots.Where(slot => !slot.SlotFlags.HasFlag(SlotFlags.POCKET))
                .Any(slot => _inventory.CanEquip(dummy, clothing, slot.Name, out _, onSpawn: true, bypassAccessCheck: bypassAccessCheck));
    }
}
