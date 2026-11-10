namespace SemiconductorEdu.Domain.Processes;

public enum ProcessType
{
    WaferInitialization,
    Oxidation,
    Photolithography,
    Etch,
    Deposition,
    IonImplantation,
    Anneal,
    Cmp,
    Cleaning,
    Metallization,
    Inspection
}

public enum ProcessExecutionStatus
{
    ConfigError,
    InvalidInput,
    PreconditionFailed,
    Interlock,
    ModelOutOfDomain,
    CalculationError,
    Cancelled,
    ProcessFailure,
    ProcessSuccess
}

public enum QualityStatus
{
    Pass,
    Fail,
    NotEvaluated
}

public enum ExecutionStage
{
    Configuration,
    InputValidation,
    Preconditions,
    EquipmentInterlock,
    ModelDomain,
    Calculation,
    InvariantValidation,
    QualityEvaluation,
    Commit
}

public static class EducationalModelNotice
{
    public const string SourceNote = "검증되지 않은 교육용 예시 모델. 실제 제조 레시피, 장비 운전 또는 공정 보증에 사용하지 않음.";
}
