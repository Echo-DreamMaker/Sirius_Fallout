using Content.Server._Misfits.NPC.Components;
using Content.Shared.NPC;
using Robust.Shared.Utility;

namespace Content.Server.NPC.Pathfinding;
///<summary>
///    Assumes NPC has comp PathfindingCustomizableComponent
///    Remove yaml comp PathfindingCustomizable for default implementation
///
///    Same as PathfindingSystem.AStar.cs implementation. Changes from original
///    kept as comments and annotated as a Misfit Change
///    Otherwise tried to improve readability
///
///<summary>
public sealed partial class PathfindingSystem
{

    private PathResult UpdateAStarCustomPath(AStarCustomPathRequest request)
    {
        PathPoly? currentNode = null;
        if (!RevalAndStart(ref request, ref currentNode, out PathResult r)) { return r; }

        DebugTools.Assert(!request.Task.IsCompleted);
        // guessing timer is started here since getPoly can be an expensive operation
        // and like we are technically pathing now
        request.Stopwatch.Restart();

        var startNode = GetPoly(request.Start);
        var endNode = GetPoly(request.End);

        if (startNode == null || endNode == null)
        {
            return PathResult.NoPath;
        }

        if (request.CostSoFar.Count == 0)
        {
            currentNode = startNode;
            request.Frontier.Add((0.0f, startNode));
            request.CostSoFar[startNode] = 0.0f;
        }

        // actual pathing done here
        AStarCustomizablePathing(ref request, ref currentNode, ref startNode, ref endNode,
                                out bool arrived, out bool timeOut);


        if (timeOut || (!arrived && request.Frontier.Count > 0))
            return PathResult.Continuing;

        if (!arrived)
            return PathResult.NoPath;

        var route = ReconstructPath(request.CameFrom, currentNode!);
        // #Misfits Change: path commented out. Was prolly a debug value
        // since it didn't do anything
        // var path = new Queue<EntityCoordinates>(route.Count);
        // #EndChange

        foreach (var node in route)
        {
            /// Due to partial planning some nodes may have been invalidated.
            if (!node.IsValid()) return PathResult.NoPath;
            // #Misfits Change: path commented out. unused
            // path.Enqueue(node.Coordinates);
            // #EndChange
        }

        DebugTools.Assert(route.Count > 0);
        request.Polys = route;
        return PathResult.Path;
    }
    ///<summary>
    /// revalidates an already started path or starts/"seeds" it by assigning fields in request
    /// also some basic checks. return false to end pathing early
    ///</summary>
    private bool RevalAndStart(ref AStarCustomPathRequest request, ref PathPoly? currentNode, out PathResult r)
    {

        if (request.Start.Equals(request.End))
        {
            r = PathResult.Path;
            return false;
        }

        if (request.Task.IsCanceled)
        {
            r = PathResult.NoPath;
            return false;
        }

        // TODO: Need partial planning that uses best node.


        // First run
        if (!request.Started)
        {
            // #Misfits Change /Fix/: Only seed the frontier once so time-sliced A* continues
            // from its in-progress state instead of repeatedly restarting from the start node.
            request.Frontier = new PriorityQueue<(float, PathPoly)>(PathPolyComparer);
            request.Started = true;
        }
        // Re-validate nodes
        else
        {
            // Theoretically this shouldn't be happening, but practically...
            if (request.Frontier.Count == 0)
            {
                r = PathResult.NoPath;
                return false;
            }

            (_, currentNode) = request.Frontier.Peek();

            if (!currentNode.IsValid())
            {
                r = PathResult.NoPath;
                return false;
            }

            // Re-validate parents too.
            if (request.CameFrom.TryGetValue(currentNode, out var parentNode) && !parentNode.IsValid())
            {
                r = PathResult.NoPath;
                return false;
            }
        }

        // doesnt matter what r is at this point
        r = PathResult.Continuing;
        return true;

    }

    ///<summary>
    /// pathing done here. Same as original but uses fields from ... to allow customization
    /// so ai can be dumber, smarter, ect... in specific ways
    ///</summary>
    private void AStarCustomizablePathing(ref AStarCustomPathRequest request, ref PathPoly? currentNode, ref PathPoly startNode, ref PathPoly endNode,
                                out bool arrived, out bool timeOut)
    {

        timeOut = false;
        var count = 0;
        arrived = false;

        while (request.Frontier.Count > 0 && count < request.Comp.NodeLimit)
        {
            // Handle whether we need to pause if we've taken too long
            if (count % 20 == 0 && count > 0 && request.Stopwatch.Elapsed > PathTime)
            {
                // I had this happen once in testing but I don't think it should be possible?
                DebugTools.Assert(request.Frontier.Count > 0);
                timeOut = true;
                return;
            }

            count++;

            // Actual pathfinding here
            (_, currentNode) = request.Frontier.Take();

            // If we're inside the required distance OR we're at the end node.
            if ((request.Distance > 0f &&
                currentNode.Coordinates.TryDistance(EntityManager, request.End, out var distance) &&
                distance <= request.Distance) ||
                currentNode.Equals(endNode))
            {
                arrived = true;
                return;
            }

            foreach (var neighbor in currentNode.Neighbors)
            {
                var tileCost = GetTileCostCustom(request, currentNode, neighbor);

                if (tileCost.Equals(0f))
                {
                    continue;
                }

                // f = g + h
                // gScore is distance to the start node
                // hScore is distance to the end node
                var gScore = request.CostSoFar[currentNode] + tileCost;
                if (request.CostSoFar.TryGetValue(neighbor, out var nextValue) && gScore >= nextValue)
                {
                    continue;
                }

                request.CameFrom[neighbor] = currentNode;
                request.CostSoFar[neighbor] = gScore;
                // pFactor is tie-breaker where the fscore is otherwise equal.
                // See http://theory.stanford.edu/~amitp/GameProgramming/Heuristics.html#breaking-ties
                // There's other ways to do it but future consideration
                // The closer the fScore is to the actual distance then the better the pathfinder will be
                // (i.e. somewhere between 1 and infinite)
                // Can use hierarchical pathfinder or whatever to improve the heuristic but this is fine for now.

                // MisFit Change: hScoreModifier from 1.0f+1.0f/1000f => 1.001f
                // I dont know why that value was hardcoded. Maybe they wanted float point errors on purpose???
                var hScore = OctileDistance(endNode, neighbor) * request.Comp.hScoreModifier;
                var fScore = gScore + hScore;
                request.Frontier.Add((fScore, neighbor));
            }
        }
    }
    /// <summary>
    /// 0 means tile is ignored so can't be walked on at allllll
    /// </summary>
    private float GetTileCostCustom(AStarCustomPathRequest request, PathPoly start, PathPoly end)
    {
        PathfindingCustomizableComponent custValues = request.Comp;
        float baseModifier = custValues.BaseCost;

        if ((end.Data.Flags & PathfindingBreadcrumbFlag.Space) != 0x0 &&
            (!TryComp<Shared.Gravity.GravityComponent>(end.GraphUid, out var gravity) || !gravity.Enabled))
        {
            return 0f;
        }

        if ((request.CollisionLayer & end.Data.CollisionMask) != 0x0 ||
            (request.CollisionMask & end.Data.CollisionLayer) != 0x0)
        {
            var isDoor = (end.Data.Flags & PathfindingBreadcrumbFlag.Door) != 0x0;
            var isAccess = (end.Data.Flags & PathfindingBreadcrumbFlag.Access) != 0x0;
            var isClimb = (end.Data.Flags & PathfindingBreadcrumbFlag.Climb) != 0x0;

            // TODO: Handling power + door prying
            // Door we should be able to open
            // Unlocked doors can be opened normally; access-locked doors require prying.
            if (isDoor && !isAccess && (request.Flags & PathFlags.Interact) != 0x0)
            {
                baseModifier += custValues.DoorOpenCost;
            }
            // Door we can force open one way or another
            else if (isDoor && isAccess && (request.Flags & PathFlags.Prying) != 0x0)
            {
                baseModifier += custValues.DoorPryCost;
            }
            else if ((request.Flags & PathFlags.Smashing) != 0x0 && end.Data.Damage > 0f)
            {
                baseModifier += end.Data.Damage / custValues.WallSmashCostDivisor;
            }
            else if (isClimb && (request.Flags & PathFlags.Climbing) != 0x0)
            {
                baseModifier += custValues.ClimbCost;
            }
            else
            {
                return 0f;
            }
        }

        return baseModifier * OctileDistance(end, start);
    }
}


