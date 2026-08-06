using Robust.Shared.Prototypes;


namespace Content.Shared._Misfits.Wielding;

[RegisterComponent]
public sealed partial class GrantActionOnWieldComponent : Component
{
   [DataField]
   public List<EntProtoId> Actions = new();
   
   /// <summary>
   /// This is used to store the EntityUids of the actions after they have been generated and added.
   ///
   /// It's helpful for removing the actions on unwield
   /// </summary>
   public List<EntityUid> ActionIds = new();
}
