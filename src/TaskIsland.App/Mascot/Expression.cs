using System.Windows.Media;
using TaskIsland.Core.Mascot;

namespace TaskIsland.App.Mascot;

/// <summary>
/// One part of a face (an eye, the mouth or the accessory) as frozen geometry. Stroke = round-capped lines,
/// Fill = filled shapes, Dark = small dark shapes drawn on top (the Curious eye notch). Mirrors the classes "s", "f"
/// and the notch group in docs/design/mascot-faces.html.
/// </summary>
public sealed record PartShape(Geometry? Stroke, Geometry? Fill, Geometry? Dark = null)
{
    public static readonly PartShape Empty = new(null, null);
}

/// <summary>An expression is data: the same rig draws all 12 (PLAN.md section 6.8).</summary>
public sealed record Expression(
    Reaction Reaction,
    PartShape LeftEye,
    PartShape RightEye,
    PartShape Mouth,
    PartShape Accessory,
    Color? EyeColor = null);
