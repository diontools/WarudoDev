using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

[NodeType(
    Id = "b5ccd3d1-0000-4d8a-9a18-ff6a78135a4c",
    Title = "ランダムタイマー RandomTimer",
    Category = "CATEGORY_EVENTS"
)]
public class RandomTimerNode : Node
{
    private float _nextTime;

    [FlowOutput]
    public Continuation Exit;

    [DataInput]
    [Label("有効？")]
    public bool Enabled;

    [DataInput]
    [Label("最小間隔[秒]")]
    [FloatSlider(0, 100)]
    public float MinInterval = 1;

    [DataInput]
    [Label("最大間隔[秒]")]
    [FloatSlider(0, 100)]
    public float MaxInterval = 5;

    public override void Create()
    {
        base.Create();

        Watch(nameof(Enabled), () => ResetTimer());
        Watch(nameof(MinInterval), () => ResetTimer());
        Watch(nameof(MaxInterval), () => ResetTimer());
    }

    public override void OnUpdate()
    {
        if (!this.Enabled)
        {
            return;
        }

        var time = Time.time;
        if (_nextTime > time)
        {
            return;
        }

        var isFirst = _nextTime == 0;
        _nextTime = time + Random.Range(MinInterval, MaxInterval);

        if (isFirst)
        {
            return;
        }

        if (this.Exit != null)
        {
            InvokeFlow(nameof(Exit));
        }
    }

    private void ResetTimer() => this._nextTime = 0;
}
