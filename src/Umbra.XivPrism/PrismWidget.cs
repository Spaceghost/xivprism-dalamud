using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Umbra.Common;
using Umbra.Widgets;
using Una.Drawing;

namespace Umbra.XivPrism;

[ToolbarWidget("XivPrism", "XivPrism",
    "Open XivPrism from your toolbar. A toolbar button with a customizable icon and label.",
    ["prism", "hud", "umbra", "companion"])]
public sealed class PrismWidget(WidgetInfo info, string? guid = null,
    Dictionary<string, object>? configValues = null) : StandardToolbarWidget(info, guid, configValues)
{
    private PrismClient? client;
    protected override StandardWidgetFeatures Features => StandardWidgetFeatures.Text
        | StandardWidgetFeatures.Icon | StandardWidgetFeatures.CustomizableIcon;
    protected override string DefaultIconType => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.Glasses;

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables() =>
    [
        .. base.GetConfigVariables(),
        new StringWidgetConfigVariable("PrismLabel", "Label", "Text on the Prism button.", "Prism", 32),
        new BooleanWidgetConfigVariable("DimUnavailable", "Dim when unavailable",
            "Grey the toolbar icon while Prism is unavailable or getting ready.", true),
        new BooleanWidgetConfigVariable("RightClickSettings", "Right-click opens Prism settings",
            "Open Prism settings when available. Ctrl+Shift+right-click still opens Umbra's widget settings.", true),
    ];

    protected override void OnLoad()
    {
        client = Framework.Service<PrismClient>();
        Node.OnClick += OnClick;
        Node.OnRightClick += OnRightClick;
    }

    protected override void OnUnload()
    {
        Node.OnClick -= OnClick;
        Node.OnRightClick -= OnRightClick;
        client = null;
    }

    protected override void OnDraw()
    {
        var label = GetConfigValue<string>("PrismLabel").Trim();
        SetText(string.IsNullOrEmpty(label) ? "Prism" : label);
        if (client is null) { SetTooltip("Prism is unavailable."); return; }
        // Umbra reapplies the user's chosen icon styling before OnDraw. Only
        // add the unavailable treatment; never erase a saved desaturation.
        if (client.State != PrismLinkState.Ready && GetConfigValue<bool>("DimUnavailable"))
            SetIconDesaturated(true);
        var tip = client.Description;
        if (client.HasSettings && GetConfigValue<bool>("RightClickSettings"))
            tip += "\nRight-click: Prism settings.";
        SetTooltip(tip);
    }

    private static bool IsUmbraShortcut()
    {
        var io = ImGui.GetIO();
        return io.KeyCtrl && io.KeyShift;
    }

    private void OnClick(Node _)
    {
        if (!IsUmbraShortcut()) client?.Toggle();
    }

    private void OnRightClick(Node _)
    {
        if (!IsUmbraShortcut() && GetConfigValue<bool>("RightClickSettings")) client?.OpenSettings();
    }
}
