using System.Windows.Media;
using SemiconductorEdu.Domain.State;

namespace SemiconductorEdu.Wpf.ViewModels;

public sealed record CrossSectionColumnViewModel(double XNanometers, double SiliconNanometers, double OxideNanometers, double SurfaceNanometers)
{
    public static CrossSectionColumnViewModel From(CrossSectionColumn column)
    {
        var silicon = column.SegmentsBottomToTop.Where(segment => segment.Material == WaferMaterial.Silicon).Sum(segment => segment.Thickness.Nanometers);
        var oxide = column.SegmentsBottomToTop.Where(segment => segment.Material == WaferMaterial.SiliconDioxide).Sum(segment => segment.Thickness.Nanometers);
        return new CrossSectionColumnViewModel(column.X.Nanometers, silicon, oxide, column.SurfaceHeight.Nanometers);
    }
}
