namespace AnalysisEngine.Core.Models;

public enum ChangeType
{
    PropertyRemoved,
    PropertyAdded,
    PropertyTypeChanged,
    MethodSignatureChanged,
    MethodRemoved,
    MethodAdded,
    ExceptionAdded,
    ExceptionRemoved,
    ValidationChanged,
    ArithmeticChanged,
    ControlFlowChanged,
    ReturnValueChanged,
    MethodBodyChanged,
    ClassRenamed,
    FileAdded,
    FileRemoved,
    Other
}

public enum FileLayer
{
    Models,
    Controllers,
    Services,
    Contracts,
    HttpClients,
    Infrastructure,
    Helpers,
    Tests,
    Unknown
}

public record ChangeAtom(
    ChangeType Type,
    string FilePath,
    FileLayer Layer,
    string Description,
    string? OldValue = null,
    string? NewValue = null,
    int LineNumber = 0
);
