using System.Text.Json.Serialization;
namespace MSMultilider.Finanzas.Models;
public sealed class FinancialCommand {
    public Guid Id{get;set;}
    public Guid OperationId{get;set;}
    public int ActorId{get;set;}
    [JsonIgnore]public string Hash{get;set;}="";
    public int Status{get;set;}
    [JsonIgnore]public string Result{get;set;}="";
    public DateTime CreatedAt{get;set;}=DateTime.UtcNow;
}
