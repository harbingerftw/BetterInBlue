using System;
using System.Numerics;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace BetterInBlue;

public class SearchInput : SimpleComponentNode {
    private readonly TextInputNode textInputNode;
    private readonly ImageNode helpNode;

    public SearchInput() {
        var statuses = Services.DataManager.GetExcelSheet<Status>();
        
        // var helpText = new SeStringBuilder()
        //                .Append("This is some ")
        //                .PushColorType(15)
        //                .PushEdgeColorType(14)
        //                .Append("Demo Text")
        //                .PopEdgeColorType()
        //                .PopColorType()
        //                .ToReadOnlySeString();
        textInputNode = new TextInputNode {
            PlaceholderString = "Search for spell...",
            MultiplyColor = new Vector3(0.9f, 0.9f, 0.9f),
            Tooltip = statuses.GetRow(5051).Description
        };
        textInputNode.AttachNode(this);

        helpNode = new SimpleImageNode {
            TexturePath = "ui/uld/CircleButtons.tex",
            TextureCoordinates = new Vector2(112.0f, 84.0f),
            TextureSize = new Vector2(28.0f, 28.0f),
            Tooltip = "Help Text Here"
        };
        helpNode.AttachNode(this);
    }

    public required Action<ReadOnlySeString>? OnInputReceived {
        get => textInputNode.OnInputReceived;
        set => textInputNode.OnInputReceived = value;
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        helpNode.Size = new Vector2(Height, Height);
        helpNode.Position = new Vector2(Width - helpNode.Width - 5.0f, 0.0f);

        textInputNode.Size = new Vector2(Width - helpNode.Width - 5.0f, Height);
        textInputNode.Position = new Vector2(0.0f, 0.0f);
    }

    public ReadOnlySeString SearchString {
        get => textInputNode.SeString;
        set => textInputNode.SeString = value;
    }
}
