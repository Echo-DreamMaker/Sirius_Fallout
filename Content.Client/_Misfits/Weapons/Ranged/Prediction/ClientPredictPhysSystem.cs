using Robust.Client.GameObjects;
using Robust.Client.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
namespace Content.Client._Misfits.Weapons.Ranged.Prediction;
/// <summary> yap
/// Based from <see cref="GunPredictionSystem"> which was ripped from RCM
/// Just attach <see cref="PredictedClientPhysicsComponent"> to whatever needs physics on the client
/// without having to wait for the server for updates if current prediction isnt enough
/// as this basically "extends prediction". Tho of course can be used for other things
/// using the engine raised events
///
/// allow physic updates without ent deleted or reset by prediction (so extends prediction basically)
/// Of course can use alongside other comps so take advantage of this with your own implementations
///
/// How this is actully implemented/why is detailed a bit in
/// <see cref="PhysicsSystem.ResetContacts">
/// but basically calling pys.UpdateIsPredicted(ent) adds ent to a list
/// that checks later client physics sys if it should add the comp <see cref="PredictedPhysicsComponent">
/// calls a ref ev <see cref="UpdateIsPredictedEvent"> and sees if its flag is true as one of the checks
/// Then anything with said comp doesnt get physics reset(I am not sure
///
/// Ideally should be used for clientside that dont need be to perfectly sync(ie dynamic visuals)
/// Can still send visuals to other clients(networked events ect...) even from the server
/// tho I suggest not having the server waste time computing/handling anything
/// and just have it network stuff since that'll be missing the point
///
/// Still havent fully understood the entity pipeline from how things
/// are updated/handled outside the content gameloop
/// </summary>


public sealed partial class ClientPredictPhysSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PhysicsSystem _physics = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
        SubscribeLocalEvent<VisualPhysComponent, UpdateIsPredictedEvent>(OnUpdatePredicted);
        SubscribeLocalEvent<VisualPhysComponent, ComponentInit>(OnCompInit);
        UpdatesBefore.Add(typeof(TransformSystem));
    }
    /// <summary>
    /// "marks" ent as predicted. convenience rather than calling manually
    /// </summary>
    public void OnCompInit(EntityUid ent, VisualPhysComponent comp, ComponentInit args)
    {
        _physics.UpdateIsPredicted(ent); /// overloads <see cref="PhysicsSystem.UpdateIsPredicted"/>
    }

    /// <summary>
    /// Called for anything marked from above method in
    /// Resides in <see cref="PhysicsSystem.UpdateIsPredicted"/> the one,
    /// called right before <see cref="PhysicsSystem.SimulateWorld"/>,
    /// Seems to just be a point to add your own checks before really doing physics calc
    /// </summary>
    public static void OnUpdatePredicted(Entity<VisualPhysComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    /// <summary>
    /// Raised right before physics sim each substep. <see cref="PhysicsSystem.SimulateWorld"/>
    /// </summary>
    public void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent args)
    {
        var query = EntityQueryEnumerator<VisualPhysComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            predicted.Coords = Transform(uid).Coordinates;
        }
    }
    /// <summary>
    /// Raised after physics Step method and right before FinalStep <see cref="PhysicsSystem.SimulateWorld"/>
    /// We just set the coords
    /// </summary>
    public void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent args)
    {
        if (_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<VisualPhysComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            _transform.SetCoordinates(uid, predicted.Coords);
        }
    }
}
