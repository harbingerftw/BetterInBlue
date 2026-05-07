using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.Simple;
using Lumina.Text.ReadOnly;

namespace BetterInBlue.Native;

public class SearchInput : SimpleComponentNode {
    private readonly TextInputNode textInputNode;

    // private readonly ImageNode helpNode;
    private readonly SimpleNineGridNode inputTooltipBg;
    private readonly TextNode inputTooltip;
    private readonly TextNode inputTooltipExtraText;

    private ReadOnlySeString lastSearchString = "";

    private Vector2 TooltipSize { get; set; }

    public SearchInput(ReadOnlySeString initialTooltip, Vector2 initialTooltipSize) {
        textInputNode = new TextInputNode {
            PlaceholderString = "Search for spell...",
            MultiplyColor = new Vector3(0.9f, 0.9f, 0.9f),
            EnableCompletion = false,
            OnFocused = ShowTooltip,
            OnFocusLost = HideTooltip
        };

        TooltipSize = initialTooltipSize;
        textInputNode.OnInputReceived += (s) => {
            var result = OnInputReceived?.Invoke(s);

            if (result != null) {
                lastSearchString = result.Value.Item1;
                textInputNode.CurrentTextNode.String = result.Value.Item1;
                inputTooltip?.String = result.Value.Item2;
            }

            OnSizeChanged();
        };
        textInputNode.OnFocused += () => textInputNode.CurrentTextNode.String = lastSearchString;
        textInputNode.OnFocusLost += () => textInputNode.CurrentTextNode.String = lastSearchString;
        textInputNode.OnInputComplete += (str) => OnComplete?.Invoke(str);

        textInputNode.AttachNode(this);

        textInputNode.OnFocused += ShowTooltip;
        textInputNode.OnFocusLost += HideTooltip;

        inputTooltipBg = new SimpleNineGridNode {
            Size = TooltipSize,
            TexturePath = "ui/uld/AozBriefing.tex",
            TextureCoordinates = new Vector2(240, 104),
            TextureSize = new Vector2(40, 40),
            TopOffset = 12,
            BottomOffset = 12,
            LeftOffset = 12,
            RightOffset = 12,
            Color = new Vector4(0.5f, 0.5f, 0.5f, 0.8f),
            IsVisible = false
        };
        inputTooltipBg.AttachNode(this);

        inputTooltip = new TextNode {
            Position = new Vector2(4.0f, 1.0f),
            Size = new Vector2(TooltipSize.X - 10.0f, 28f),
            FontSize = 12,
            LineSpacing = 13,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = TextFlags.Edge | TextFlags.Emboss | TextFlags.MultiLine | TextFlags.WordWrap,
            String = initialTooltip,
            IsVisible = false
        };

        inputTooltipExtraText = new TextNode {
            String = this.ExtraTooltip,
            Position = new Vector2(4.0f, 1.0f),
            TextColor = KnownColor.Aqua.Vector(),
            Size = new Vector2(TooltipSize.X - 14.0f, 20f),
            FontSize = 10,
            LineSpacing = 13,
            AlignmentType = AlignmentType.Right,
            TextFlags = TextFlags.Edge | TextFlags.Italic,
            IsVisible = false
        };
        inputTooltipExtraText.AttachNode(this);
        inputTooltip.AttachNode(this);
    }

    public bool InputIsError {
        get => textInputNode.IsError;
        set => textInputNode.IsError = value;
    }

    public required Func<ReadOnlySeString, (ReadOnlySeString, ReadOnlySeString)>? OnInputReceived { get; init; }
    public required Action<ReadOnlySeString>? OnComplete { get; init; }

    public required ReadOnlySeString ExtraTooltip {
        get => field;
        init {
            field = value;
            inputTooltipExtraText.String = value;
            inputTooltipExtraText.IsVisible = false;
        }
    }

    private new void ShowTooltip() {
        inputTooltipBg.IsVisible = true;
        inputTooltip.IsVisible = true;
        inputTooltipExtraText.IsVisible = true;
    }

    private new void HideTooltip() {
        inputTooltipBg.IsVisible = false;
        inputTooltip.IsVisible = false;
        inputTooltipExtraText.IsVisible = false;
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        textInputNode.Size = new Vector2(Width - 5.0f, Height);
        textInputNode.Position = new Vector2(0.0f, 0.0f);

        var tooltipSize = inputTooltip.GetTextDrawSize(false);
        var tooltipExtraSize = inputTooltip.GetTextDrawSize(false);

        // inputTooltip.Size = new Vector2(TooltipSize.X + 10, 1);
        // Services.Log.Debug($"bg size {TooltipSize} --- {size} - {inputTooltip.LineSpacing}");
        inputTooltip.Position = new Vector2(10.0f, 33f);

        this.inputTooltipExtraText.Position = new Vector2(4f, tooltipSize.Y + 27f);

        inputTooltipBg.Size = TooltipSize with {Y = tooltipSize.Y + 23f + (tooltipSize.Y * 0.1f)};
        inputTooltipBg.Position = new Vector2(0.0f, 23f);
    }

    public ReadOnlySeString SearchString {
        get => textInputNode.String;
        set => textInputNode.String = value;
    }
}
