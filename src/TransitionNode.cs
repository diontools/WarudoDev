using DG.Tweening;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

#nullable enable

[NodeType(
    Id = "ef318e4d-5e59-497c-a2eb-7a82f337e555",
    Title = "トランジション",
    Category = "CATEGORY_ASSETS"
)]
public class TransitionNode : Node
{
    private Sequence? transitionSequence;


    [FlowOutput]
    [Label("入場")]
    public Continuation? TransitionStart;

    [FlowOutput]
    [Label("トランジション")]
    public Continuation? Exit;

    [FlowOutput]
    [Label("退場")]
    public Continuation? TransitionEnd;

    [FlowInput]
    public Continuation? Enter()
    {
        this.StartTransition();
        return null;
    }

    [DataInput]
    [Label("トランジション")]
    public TransitionAsset? Transition;

    [DataInput]
    [Label("入場時間")]
    [FloatSlider(0, 3f)]
    public float EnterTime = 1f;

    [DataInput]
    [Label("待ち時間")]
    [FloatSlider(0, 5f)]
    public float WaitTime = 3f;

    [DataInput]
    [Label("退場時間")]
    [FloatSlider(0, 3f)]
    public float ExitTime = 1f;

    private void StartTransition()
    {
        var transition = this.Transition;
        if (transition == null) return;

        if (this.transitionSequence != null)
        {
            this.transitionSequence.Kill();
            this.transitionSequence = null;
        }

        this.InvokeFlow(nameof(this.TransitionStart));

        var sequence = DOTween.Sequence();
        sequence.Append(DOTween.To(v => transition.SetDataInput(nameof(transition.Rate), v), 0f, 0.5f, this.EnterTime));
        sequence.AppendCallback(() => this.InvokeFlow(nameof(this.Exit)));
        sequence.AppendInterval(this.WaitTime);
        sequence.Append(DOTween.To(v => transition.SetDataInput(nameof(transition.Rate), v), 0.5f, 1f, this.ExitTime));
        sequence.AppendCallback(() => this.InvokeFlow(nameof(this.TransitionEnd)));
        this.transitionSequence = sequence.Play();
    }
}
