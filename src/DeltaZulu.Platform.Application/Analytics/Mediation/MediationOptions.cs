namespace DeltaZulu.Platform.Application.Analytics.Mediation;

public sealed class MediationOptions
{
    /// <summary>
    /// Name of the Proton stream that receives all alert payloads — both NRT (via ALERT UDF)
    /// and scheduled (via Task INTO clause).
    /// </summary>
    public string AlertDispatchChannel { get; set; } = "alert_dispatch";
}
