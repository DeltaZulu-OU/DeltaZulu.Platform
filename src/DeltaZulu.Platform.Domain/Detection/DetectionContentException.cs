namespace DeltaZulu.DetectionContent;

/// <summary>Exception raised when shared detection-content contracts reject invalid input.</summary>
public sealed class DetectionContentException : Exception
{
    /// <summary>Machine-readable error code.</summary>
    public string Code { get; }

    /// <summary>Creates a detection-content contract exception.</summary>
    public DetectionContentException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}