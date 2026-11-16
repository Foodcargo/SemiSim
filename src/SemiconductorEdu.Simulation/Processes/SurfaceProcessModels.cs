using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Domain.Validation;
namespace SemiconductorEdu.Simulation.Processes;

// Specification v1.0 educational assumptions; these are not manufacturing recipes.
public sealed partial class SurfaceProcessModels
{
    private sealed class Checks
    {
        public List<ValidationIssue> Errors { get; } = new();
        public void Require(bool valid, string code, string field = "", ExecutionStage stage = ExecutionStage.InputValidation, string expected = "supported value")
        { if (!valid) Errors.Add(new(code, stage.ToString(), field, null, expected, code, "입력 및 현재 웨이퍼 상태를 확인하세요.")); }
        public void Range(double? value, double min, double max, string field) => Require(value is double x && double.IsFinite(x) && x >= min && x <= max, "INPUT_RANGE", field, expected:$"{min}..{max}");
        public void EnumValue<T>(T value, string field) where T : struct, Enum => Require(Enum.IsDefined(value), "UNKNOWN_OPTION", field);
        public ProcessModelOutput? Failure() => Errors.Count == 0 ? null : new(null, Array.Empty<ProcessMetric>(), new(false, Errors, Array.Empty<ValidationIssue>()));
    }
    private static bool HasPr(WaferState s) => s.CrossSection.Columns.Any(c => c.SegmentsBottomToTop.Any(m => m.Material == WaferMaterial.Photoresist && m.Thickness.Meters > 0));
    private static bool Planar(WaferState s) => s.CrossSection.Columns.Count > 1 && s.CrossSection.Columns.Max(c=>c.SurfaceHeight.Meters)-s.CrossSection.Columns.Min(c=>c.SurfaceHeight.Meters) <= 1e-12;
    private static CrossSectionColumn Column(CrossSectionColumn source, IEnumerable<MaterialSegment> segments)
    {
        var kept = segments.Where(s => s.Thickness.Meters > 0).ToArray();
        return source with { SegmentsBottomToTop = Array.AsReadOnly(kept), SurfaceHeight = Length.FromMeters(kept.Sum(s=>s.Thickness.Meters)) };
    }
    private static WaferState Candidate(WaferState before, CrossSectionColumn[] columns, Guid run, DateTimeOffset now, ResistPatternState? pattern)
    {
        var ids=columns.SelectMany(c=>c.SegmentsBottomToTop).Select(s=>s.LayerId).ToHashSet();
        return before with { StateId=Guid.NewGuid(), ParentStateId=before.StateId, Revision=before.Revision+1, CreatedAtUtc=now,
            CrossSection=before.CrossSection with { Columns=Array.AsReadOnly(columns) },
            Layers=Array.AsReadOnly(before.Layers.Where(l=>ids.Contains(l.LayerId)).ToArray()), ResistPattern=pattern,
            HistoryIds=Array.AsReadOnly(before.HistoryIds.Append(run).ToArray()) };
    }
    private static QualityTarget Relative(string id,double target,double tolerance,string unit="nm") => new(id,target,tolerance,null,null,null,unit,true);
    private static QualityTarget Maximum(string id,double max,string unit="nm") => new(id,0,null,null,max,null,unit,true);
    private static ProcessModelOutput Done(WaferState s, ProcessMetric[] metrics, QualityTarget[] targets, params string[] unsupported) =>
        new(s,metrics,ValidationResult.Success()) { DefaultQuality=new("EducationalDefault","edu-1.0",targets), UnsupportedEffects=unsupported };

    public ProcessModelOutput Photo(WaferState before, PhotoConditions p, Guid run, DateTimeOffset now, CancellationToken token=default)
    {
        var c=new Checks(); c.EnumValue(p.Tone,"resistTone");c.EnumValue(p.Exposure,"exposureMode");
        c.Range(p.ResistThickness.Meters,Length.FromMicrometers(0.02).Meters,Length.FromMicrometers(p.Exposure==ExposureMode.EUV?0.1:5).Meters,"resistThickness");
        c.Range(p.Dose,1,200,"dose");c.Range(p.Focus.Micrometers,-1,1,"focus");
        c.Range(p.ClearWidth.Nanometers,10,10000,"clearWidth");c.Range(p.Pitch.Nanometers,20,20000,"pitch");
        c.Range(p.Offset.Micrometers,0,100,"offset");c.Range(p.DevelopTime.Seconds,10,180,"developTime");
        c.Require(p.ClearWidth.Meters<p.Pitch.Meters,"CLEAR_WIDTH_REQUIRES_SMALLER_PITCH","clearWidth");
        c.Require(p.Offset.Meters+p.Pitch.Meters<=before.CrossSection.Width.Meters,"PATTERN_OUTSIDE_DOMAIN","offset");
        if(c.Failure() is {} invalid)return invalid;
        c.Require(!HasPr(before)&&before.ResistPattern is null,"PHOTO_RESIST_ALREADY_PRESENT",stage:ExecutionStage.Preconditions);
        c.Require(Planar(before),"PHOTO_NONPLANAR",stage:ExecutionStage.Preconditions);
        if(c.Failure() is {} pre)return pre;
        var feature=Math.Min(p.ClearWidth.Nanometers,p.Pitch.Nanometers-p.ClearWidth.Nanometers);
        var dx=before.CrossSection.Width.Nanometers/(before.CrossSection.Resolution-1);
        c.Require(feature>=(p.Exposure==ExposureMode.DUV?100:20),"OPTICAL_RESOLUTION",stage:ExecutionStage.ModelDomain);
        c.Require(feature/dx>=4,"GEOMETRY_RESOLUTION",stage:ExecutionStage.ModelDomain);
        if(c.Failure() is {} domain)return domain;
        var positive=p.Tone==ResistTone.Positive;
        var e0=p.Exposure==ExposureMode.DUV?50.0:30.0;
        var h0=p.Exposure==ExposureMode.DUV?1000.0:50.0;
        var focusScale=p.Exposure==ExposureMode.DUV?0.5:0.1;
        var target=positive?p.ClearWidth.Nanometers:p.Pitch.Nanometers-p.ClearWidth.Nanometers;
        var bias=(positive?1:-1)*(20*(p.Dose/e0-1)+10*(p.DevelopTime.Seconds/60-1));
        var cd=target+bias-20*Math.Pow(p.Focus.Micrometers/focusScale,2);
        var residual=p.ResistThickness.Nanometers*(1-Math.Min(1,(p.Dose/e0)*(p.DevelopTime.Seconds/60)*(h0/p.ResistThickness.Nanometers)));
        var intervals=new List<LengthInterval>();
        for(var origin=p.Offset.Nanometers;origin<before.CrossSection.Width.Nanometers;origin+=p.Pitch.Nanometers)
        {
            token.ThrowIfCancellationRequested();
            if(cd<=0)continue;
            var start=cd>=p.Pitch.Nanometers?origin:origin+(positive?0:p.ClearWidth.Nanometers);
            var end=Math.Min(origin+p.Pitch.Nanometers,start+cd);
            end=Math.Min(end,before.CrossSection.Width.Nanometers);
            if(end>start)intervals.Add(new(Length.FromNanometers(start),Length.FromNanometers(end)));
        }
        if(cd>0&&cd<p.Pitch.Nanometers)
        {
            c.Require(Math.Min(cd,p.Pitch.Nanometers-cd)/dx>=4,"GEOMETRY_RESOLUTION",stage:ExecutionStage.ModelDomain);
            if(c.Failure() is {} geometry)return geometry;
        }
        var layer=Guid.NewGuid();var residuals=new Length[before.CrossSection.Columns.Count];
        var columns=before.CrossSection.Columns.Select((col,i)=>
        {
            token.ThrowIfCancellationRequested();
            var opened=intervals.Any(v=>col.X.Meters>=v.Start.Meters&&col.X.Meters<v.End.Meters);
            residuals[i]=Length.FromNanometers(opened?residual:p.ResistThickness.Nanometers);
            return Column(col,col.SegmentsBottomToTop.Append(new(layer,WaferMaterial.Photoresist,residuals[i],run)));
        }).ToArray();
        var state=Candidate(before,columns,run,now,new(Guid.NewGuid(),intervals,residuals));
        if(columns.Any(col=>col.SegmentsBottomToTop.Any(m=>m.LayerId==layer)))
            state=state with { Layers=state.Layers.Append(new LayerReference(layer,WaferMaterial.Photoresist,run)).ToArray() };
        var validPattern=cd>0&&cd<p.Pitch.Nanometers;
        return Done(state,new[]{new ProcessMetric("cd",cd,"nm"),new ProcessMetric("openingResidual",residual,"nm"),
            new ProcessMetric("protectedResist",validPattern?p.ResistThickness.Nanometers:0,"nm"),new ProcessMetric("patternValid",validPattern?1:0,"1")},
            new[]{new QualityTarget("cd",500,null,50,null,null,"nm",true),Maximum("openingResidual",1),
                new QualityTarget("protectedResist",0,null,null,null,1,"nm",true),new QualityTarget("patternValid",0,null,null,null,1,"1",true)},
            "실제 광학·광산 발생·확률 결함·3D 마스크는 미반영");
    }

    public ProcessModelOutput Etch(WaferState before, EtchConditions p, Guid run, DateTimeOffset now, CancellationToken token=default)
    {
        var c=new Checks();c.EnumValue(p.Method,"method");c.EnumValue(p.Scope,"scope");
        c.Range(p.Duration.Seconds,1,600,"duration");
        var expected=p.Method==SurfaceMethod.Dry?(p.TargetMaterial==WaferMaterial.Silicon?"EduChlorine":"EduFluorine"):
            p.TargetMaterial==WaferMaterial.Silicon?"EduSilicon":p.TargetMaterial==WaferMaterial.SiliconNitride?"EduNitride":"EduOxide";
        c.Require(p.TargetMaterial is WaferMaterial.Silicon or WaferMaterial.SiliconDioxide or WaferMaterial.SiliconNitride,"ETCH_MATERIAL_INVALID","targetMaterial");
        c.Require(p.Chemistry==expected,"CHEMISTRY_COMBINATION","chemistry",expected:expected);
        if(p.Method==SurfaceMethod.Dry)
        {
            c.Range(p.Pressure?.MilliTorr,1,500,"pressure");c.Range(p.RfPower,10,2000,"rfPower");
            c.Range(p.Flow?.Value,1,500,"flow");c.Range(p.Temperature.Celsius,-20,200,"temperature");
            c.Require(p.Concentration is null,"INAPPLICABLE_PARAMETER","concentration");
        }
        else
        {
            c.Range(p.Concentration?.Value,0.1,30,"concentration");c.Range(p.Temperature.Celsius,20,90,"temperature");
            c.Require(p.Pressure is null&&p.RfPower is null&&p.Flow is null,"INAPPLICABLE_PARAMETER","dryInputs");
        }
        if(c.Failure() is {} invalid)return invalid;
        c.Require(p.Scope==EtchScope.Patterned?before.ResistPattern is not null:!HasPr(before),"ETCH_SCOPE_PRECONDITION",stage:ExecutionStage.Preconditions);
        c.Require(p.TargetMaterial!=WaferMaterial.Silicon||before.ImplantProfiles.Count==0,"DOPED_SURFACE_COORDINATES_UNSUPPORTED",stage:ExecutionStage.Preconditions);
        c.Require(before.Wires.Count==0,"WIRE_MODIFICATION_UNSUPPORTED",stage:ExecutionStage.Preconditions);
        var exposed=before.CrossSection.Columns.Select(col=>col.SegmentsBottomToTop.LastOrDefault(s=>s.Material!=WaferMaterial.Photoresist)).ToArray();
        c.Require(exposed.Any(m=>m?.Material==p.TargetMaterial),"ETCH_TARGET_NOT_EXPOSED",stage:ExecutionStage.Preconditions);
        if(c.Failure() is {} pre)return pre;
        c.Require(p.Method==SurfaceMethod.Dry?p.Pressure!.Value.MilliTorr is >=5 and <=300 && p.RfPower<=1000:p.Temperature.Celsius<=80,
            "ETCH_INTERLOCK",stage:ExecutionStage.EquipmentInterlock);
        if(c.Failure() is {} interlock)return interlock;
        var factor=p.Method==SurfaceMethod.Dry?Math.Sqrt(p.RfPower!.Value/300)*Math.Pow(50/p.Pressure!.Value.MilliTorr,0.2)*Math.Pow(p.Flow!.Value.Value/100,0.2)*Math.Exp((p.Temperature.Celsius-25)/200):
            Math.Sqrt(p.Concentration!.Value.Value/5)*Math.Exp((p.Temperature.Celsius-25)/50);
        var rate=(p.TargetMaterial==WaferMaterial.SiliconDioxide?1:p.TargetMaterial==WaferMaterial.SiliconNitride?0.5:1.5)*factor;
        var prRate=rate/(p.Method==SurfaceMethod.Dry?5:10);var stop=false;var protectedLost=false;
        var removedOpen=new List<double>();var columns=new CrossSectionColumn[before.CrossSection.Columns.Count];var residuals=new Length[columns.Length];
        for(var i=0;i<columns.Length;i++)
        {
            token.ThrowIfCancellationRequested();var col=before.CrossSection.Columns[i];var segments=col.SegmentsBottomToTop.ToList();
            var pr=segments.Where(s=>s.Material==WaferMaterial.Photoresist).Sum(s=>s.Thickness.Nanometers);
            var geometricOpen=p.Scope==EtchScope.Blanket||before.ResistPattern!.OpenIntervals.Any(v=>col.X.Meters>=v.Start.Meters&&col.X.Meters<v.End.Meters);
            var initiallyOpen=geometricOpen&&pr<=1;
            var exposedSeconds=initiallyOpen?p.Duration.Seconds:Math.Max(0,p.Duration.Seconds-pr/prRate);
            var prLeft=Math.Max(0,pr-prRate*p.Duration.Seconds);
            if(!geometricOpen&&pr>0&&prLeft==0)protectedLost=true;
            var prToRemove=pr-prLeft;
            for(var j=segments.Count-1;j>=0;j--)
                if(segments[j].Material==WaferMaterial.Photoresist)
                {var take=Math.Min(prToRemove,segments[j].Thickness.Nanometers);prToRemove-=take;segments[j]=segments[j] with{Thickness=Length.FromNanometers(segments[j].Thickness.Nanometers-take)};}
            var need=rate*exposedSeconds;var removed=0.0;
            for(var j=segments.Count-1;j>=0&&need>0;j--)
            {
                if(segments[j].Material==WaferMaterial.Photoresist)continue;
                if(segments[j].Material!=p.TargetMaterial){stop=true;break;}
                var take=Math.Min(need,segments[j].Thickness.Nanometers);
                if(p.TargetMaterial==WaferMaterial.Silicon&&take>=segments[j].Thickness.Nanometers)
                {c.Require(false,"ETCH_SILICON_BREAKTHROUGH",stage:ExecutionStage.ModelDomain);return c.Failure()!;}
                need-=take;removed+=take;segments[j]=segments[j] with{Thickness=Length.FromNanometers(segments[j].Thickness.Nanometers-take)};
                if(segments[j].Thickness.Meters>0)break;
                if(j>0&&segments[j-1].Material!=p.TargetMaterial)stop=true;
            }
            if(initiallyOpen)removedOpen.Add(removed);
            columns[i]=Column(col,segments);residuals[i]=Length.FromNanometers(prLeft);
        }
        var pattern=before.ResistPattern is {} old&&residuals.Any(r=>r.Meters>0)?old with {ResidualThicknessByColumn=residuals}:null;
        var result=Done(Candidate(before,columns,run,now,pattern),new[]{new ProcessMetric("etchDepth",removedOpen.Count>0?removedOpen.Average():0,"nm"),
            new ProcessMetric("protectedPrLost",protectedLost?1:0,"1"),new ProcessMetric("maxPrResidual",residuals.Max(r=>r.Nanometers),"nm"),new ProcessMetric("stopLayerReached",stop?1:0,"1")},
            p.Scope==EtchScope.Patterned?new[]{Relative("etchDepth",100,10),Maximum("protectedPrLost",0,"1")}:new[]{Relative("etchDepth",100,10)},
            "Wet 언더컷·Dry 측벽·하부층 선택비는 미반영");
        return stop?result with {Validation=ValidationResult.Success(new[]{new ValidationIssue("STOP_LAYER_REACHED","Calculation",null,null,"target material exhausted","하부 다른 재료에서 제거를 멈췄습니다.","정지막 도달 결과를 확인하세요.")})}:result;
    }

    public ProcessModelOutput Clean(WaferState before, CleanConditions p, Guid run, DateTimeOffset now, CancellationToken token=default)
    {
        var c=new Checks();c.EnumValue(p.Method,"method");c.EnumValue(p.Purpose,"purpose");
        var wet=p.Method==SurfaceMethod.Wet;
        c.Range(p.Temperature.Celsius,20,wet?90:200,"temperature");c.Range(p.Duration.Seconds,10,wet?1800:600,"duration");
        c.Require(p.Chemistry==(wet?(p.Purpose==CleanPurpose.PRStrip?"EduStrip":"EduClean"):"EduPlasma"),"CHEMISTRY_COMBINATION","chemistry");
        if(wet){c.Range(p.Concentration?.Value,0.1,30,"concentration");c.Require(p.Power is null&&p.Pressure is null,"INAPPLICABLE_PARAMETER","dryInputs");}
        else {c.Range(p.Power,10,500,"power");c.Range(p.Pressure?.MilliTorr,10,1000,"pressure");c.Require(p.Concentration is null,"INAPPLICABLE_PARAMETER","concentration");}
        if(c.Failure() is {} invalid)return invalid;
        c.Require(p.Purpose!=CleanPurpose.PRStrip||HasPr(before),"CLEANING_RESIST_REQUIRED",stage:ExecutionStage.Preconditions);
        if(c.Failure() is {} pre)return pre;
        c.Require(wet?p.Temperature.Celsius<=80:p.Power<=300,"CLEANING_INTERLOCK",stage:ExecutionStage.EquipmentInterlock);
        if(c.Failure() is {} interlock)return interlock;
        var factor=wet?Math.Sqrt(p.Concentration!.Value.Value/5)*Math.Exp((p.Temperature.Celsius-25)/100):
            Math.Sqrt(p.Power!.Value/100)*Math.Pow(100/p.Pressure!.Value.MilliTorr,0.1)*Math.Exp((p.Temperature.Celsius-80)/200);
        var residuals=new Length[before.CrossSection.Columns.Count];
        var columns=before.CrossSection.Columns.Select((col,i)=>
        {
            token.ThrowIfCancellationRequested();var removal=p.Purpose==CleanPurpose.PRStrip?10*factor*p.Duration.Seconds:0;
            var segments=col.SegmentsBottomToTop.ToArray();
            for(var j=segments.Length-1;j>=0;j--)if(segments[j].Material==WaferMaterial.Photoresist)
            {var take=Math.Min(removal,segments[j].Thickness.Nanometers);removal-=take;segments[j]=segments[j] with {Thickness=Length.FromNanometers(segments[j].Thickness.Nanometers-take)};}
            residuals[i]=Length.FromMeters(segments.Where(s=>s.Material==WaferMaterial.Photoresist).Sum(s=>s.Thickness.Meters));
            return Column(col,segments);
        }).ToArray();
        var pattern=before.ResistPattern is {} old&&residuals.Any(r=>r.Meters>0)?old with {ResidualThicknessByColumn=residuals}:null;
        var state=Candidate(before,columns,run,now,pattern) with {ContaminationIndex=before.ContaminationIndex*Math.Exp(-0.02*factor*p.Duration.Seconds)};
        return Done(state,new[]{new ProcessMetric("contamination",state.ContaminationIndex,"point"),new ProcessMetric("maxPrResidual",residuals.Max(r=>r.Nanometers),"nm")},
            p.Purpose==CleanPurpose.PRStrip?new[]{Maximum("contamination",10,"point"),Maximum("maxPrResidual",1)}:new[]{Maximum("contamination",10,"point")},
            "재료 손상·재오염·실제 용액 조성은 미반영");
    }

    public ProcessModelOutput Cmp(WaferState before, CmpConditions p, Guid run, DateTimeOffset now, CancellationToken token=default)
    {
        var c=new Checks();c.EnumValue(p.Slurry,"slurry");
        c.Require(p.Slurry==CmpSlurry.Oxide?p.TargetMaterial==WaferMaterial.SiliconDioxide:p.TargetMaterial is WaferMaterial.Aluminum or WaferMaterial.Copper or WaferMaterial.Tungsten or WaferMaterial.TitaniumNitride,"CMP_COMBINATION","targetMaterial");
        c.Range(p.PolishPressure.Pascals,5000,70000,"polishPressure");c.Range(p.PadSpeed.RevolutionsPerMinute,10,200,"padSpeed");
        c.Range(p.WaferSpeed.RevolutionsPerMinute,10,200,"waferSpeed");c.Range(p.Duration.Seconds,10,600,"duration");
        if(c.Failure() is {} invalid)return invalid;
        c.Require(!HasPr(before),"CMP_PR_PRESENT",stage:ExecutionStage.Preconditions);
        c.Require(before.Wires.Count==0,"WIRE_MODIFICATION_UNSUPPORTED",stage:ExecutionStage.Preconditions);
        c.Require(before.CrossSection.Columns.All(col=>col.SegmentsBottomToTop.LastOrDefault()?.Material==p.TargetMaterial),"CMP_TOP_MATERIAL_REQUIRED",stage:ExecutionStage.Preconditions);
        if(c.Failure() is {} pre)return pre;
        c.Require(p.PolishPressure.Pascals<=50000,"CMP_INTERLOCK",stage:ExecutionStage.EquipmentInterlock);
        if(c.Failure() is {} interlock)return interlock;
        var removal=(p.Slurry==CmpSlurry.Oxide?0.001:0.002)*(p.PolishPressure.Pascals/1000)*(p.PadSpeed.RevolutionsPerMinute+p.WaferSpeed.RevolutionsPerMinute)/2*p.Duration.Seconds;
        var minHeight=before.CrossSection.Columns.Min(col=>col.SurfaceHeight.Nanometers);
        var removed=new List<double>();var residual=new List<double>();var stopped=false;
        var columns=before.CrossSection.Columns.Select(col=>
        {
            token.ThrowIfCancellationRequested();var need=removal*(1+(col.SurfaceHeight.Nanometers-minHeight)/1000);var total=0.0;
            var segments=col.SegmentsBottomToTop.ToArray();
            for(var j=segments.Length-1;j>=0&&need>0;j--)
            {
                if(segments[j].Material!=p.TargetMaterial){stopped=true;break;}
                var take=Math.Min(need,segments[j].Thickness.Nanometers);need-=take;total+=take;
                segments[j]=segments[j] with {Thickness=Length.FromNanometers(segments[j].Thickness.Nanometers-take)};
            }
            removed.Add(total);residual.Add(segments.Reverse().TakeWhile(s=>s.Material==p.TargetMaterial).Sum(s=>s.Thickness.Nanometers));
            return Column(col,segments);
        }).ToArray();
        return Done(Candidate(before,columns,run,now,null),new[]{new ProcessMetric("removedThickness",removed.Average(),"nm"),new ProcessMetric("remainingThickness",residual.Average(),"nm"),
            new ProcessMetric("stepHeight",columns.Max(col=>col.SurfaceHeight.Nanometers)-columns.Min(col=>col.SurfaceHeight.Nanometers),"nm"),new ProcessMetric("stopLayerReached",stopped?1:0,"1")},
            new[]{Relative("remainingThickness",28,10),Maximum("stepHeight",20)},"패드 탄성·디싱·에로전·혼합 재료 표면은 미반영");
    }
}
