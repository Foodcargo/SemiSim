using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.Validation;
namespace SemiconductorEdu.Application.Execution;
public static class QualityEvaluation
{
    public static ProcessExecutionResponse Apply(ProcessExecutionResponse response,QualityProfile? profile)
    {
        if(response.CommittedState is null||profile is null)return response;
        var criteria=new List<QualityCriterionResult>();var missing=new List<ValidationIssue>();
        foreach(var target in profile.Targets)
        {
            var metric=response.Result.Metrics.FirstOrDefault(m=>m.Id==target.MetricId);
            if(metric is null)
            {
                if(target.Required)missing.Add(new("REQUIRED_METRIC_MISSING","QualityEvaluation",target.MetricId,null,"calculated metric","필수 지표가 누락되었습니다.","계산 모델을 확인하세요."));
                continue;
            }
            double? low=target.Minimum,high=target.Maximum;
            if(target.TolerancePercent is double relative){var delta=Math.Abs(target.Target)*relative/100;low=target.Target-delta;high=target.Target+delta;}
            else if(target.AbsoluteTolerance is double absolute){low=target.Target-absolute;high=target.Target+absolute;}
            bool Pass(double value,double boundary,bool minimum)=>minimum?value+1e-9*Math.Max(1,Math.Max(Math.Abs(value),Math.Abs(boundary)))>=boundary:value<=boundary+1e-9*Math.Max(1,Math.Max(Math.Abs(value),Math.Abs(boundary)));
            var valid=metric.Value is double x&&double.IsFinite(x)&&metric.Unit==target.Unit&&
                (low is not double lo||Pass(x,lo,true))&&(high is not double hi||Pass(x,hi,false));
            if(metric.Value is double number&&!double.IsFinite(number))missing.Add(new("NONFINITE_METRIC","InvariantValidation",metric.Id,null,"finite metric","비유한 계산값입니다.","계산 모델을 확인하세요."));
            criteria.Add(new(metric.Id,metric.Value,metric.Unit,low,high,valid,metric.Value is null?"QUALITY_NOT_EVALUABLE":valid?null:"QUALITY_OUTSIDE_TARGET"));
        }
        if(missing.Count>0)return response with {CommittedState=null,Result=response.Result with {AfterStateId=null,ExecutionStatus=ProcessExecutionStatus.CalculationError,QualityStatus=QualityStatus.NotEvaluated,Errors=missing,Criteria=criteria}};
        var passed=profile.Targets.Where(t=>t.Required).All(t=>criteria.Any(c=>c.MetricId==t.MetricId&&c.Pass));
        return response with {Result=response.Result with {ExecutionStatus=passed?ProcessExecutionStatus.ProcessSuccess:ProcessExecutionStatus.ProcessFailure,
            QualityStatus=passed?QualityStatus.Pass:QualityStatus.Fail,Criteria=criteria,AppliedQualityProfile=profile}};
    }
}
