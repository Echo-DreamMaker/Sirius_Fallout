namespace Content.Shared._Sirius.Books;

[RegisterComponent]
public sealed partial class HyperlinkBookComponent : Component
{
    [DataField(required: true)]
    public string Url = string.Empty;
}
