namespace Content.Shared._NC.RandomAccessKey;
/// <summary>
/// #Cythisiax Edited - marker for a constructed door that has a randomly assigned key.
/// The system mints the key on construction completion and lets the holder toggle the
/// door's lock (stored in <c>AccessReader.Enabled</c>) by using the key on the door.
/// </summary>
[RegisterComponent]
public sealed partial class RandomAccessKeyComponent : Component
{
}
