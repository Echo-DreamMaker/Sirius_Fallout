using System.Linq;
using Robust.Shared.Map;
using static Content.Shared.Interaction.SharedInteractionSystem;

namespace Content.Shared.Construction
{
    public abstract class SharedConstructionSystem : EntitySystem
    {
        [Dependency] private readonly SharedMapSystem _mapManager = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;

        /// <summary>
        ///     Get predicate for construction obstruction checks.
        /// </summary>
        public Ignored? GetPredicate(bool canBuildInImpassable, MapCoordinates coords)
        {
            if (!canBuildInImpassable)
                return null;

            if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
                return null;

            var ignored = _mapSystem.GetAnchoredEntities(gridUid, grid, coords).ToHashSet();
            return e => ignored.Contains(e);
        }
    }
}
