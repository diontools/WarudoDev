using System;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Plugins.Core.Nodes;

[NodeType(
    Id = "fd578bed-cd66-4b32-82b3-79cf0b5f0f9e",
    Title = "列挙値をIntegerに変換",
    Category = "CATEGORY_ARITHMETIC"
)]
public class EnumToIntegerNode : EnumNode
{
    private int _convertedValue;

    [DataInput]
    [Label("INPUT_ENUM_VALUE")]
    public object Input;

    [DataOutput]
    public int Integer() => _convertedValue;


    protected override void OnCreate()
    {
        base.OnCreate();
        WatchAll(new[] { nameof(EnumType), nameof(Input) }, UpdateValue);
        UpdateValue();
    }

    private void UpdateValue()
    {
        var enumType = this.EnumSystemType;
        var input = this.Input;

        if (enumType == null
            || input == null
            || input.GetType() != enumType
            || Enum.GetUnderlyingType(enumType) != typeof(int))
        {
            _convertedValue = 0;
            return;
        }

        var value = (int)input;
        if (_convertedValue != value)
        {
            _convertedValue = value;
        }
    }
}
