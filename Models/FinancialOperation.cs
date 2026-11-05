using System.Text.Json.Serialization;
namespace MSMultilider.Finanzas.Models;
public sealed class FinancialOperation {
    public Guid Id{get;set;}
    public string State{get;set;}="Preparada";
    public int VentaId{get;set;}
    public int PropiedadId{get;set;}
    public Guid? ClosedBy{get;set;}
    [JsonIgnore]public string Snapshot{get;set;}="";
    [JsonIgnore]public string ReadSnapshot{get;set;}="";
    [JsonIgnore]public string? CustomerName{get;set;}
    [JsonIgnore]public string? AdviserName{get;set;}
    [JsonIgnore]public string? AdviserEmail{get;set;}
    [JsonIgnore]public string? CustomerCi{get;set;}
    [JsonIgnore]public string SnapshotHash{get;set;}="";
    [JsonIgnore]public string ActorIssuer{get;set;}="";
    [JsonIgnore]public string ActorSubject{get;set;}="";
    public DateTime UpdatedAt{get;set;}=DateTime.UtcNow;
}
