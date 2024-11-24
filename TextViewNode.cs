using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

[NodeType(
    Id = "54419783-866f-452e-bde7-20c7e003d453",
    Title = "TextView",
    Category = "CATEGORY_DEBUG"
)]
public class TextViewNode : Node
{
    [Markdown]
    [Transient]
    public string Text = "test";

    [DataInput]
    public object InputObject;

    [FlowInput]
    public Continuation Enter() => null;

    protected override void OnCreate()
    {
        base.OnCreate();
        Watch(nameof(InputObject), Refresh);
        Refresh();
    }

    private void Refresh()
    {
        var text = this.InputObject is null ? "***null-object***" : this.InputObject.ToString();
        this.Text = text is null ? "***null***" : text;
        BroadcastDataInput(nameof(Text));
    }
}
