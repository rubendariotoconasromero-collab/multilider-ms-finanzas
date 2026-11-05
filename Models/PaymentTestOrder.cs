using System.Text.Json.Serialization;
namespace MSMultilider.Finanzas.Models;
public sealed class PaymentTestOrder {
    public Guid Id{get;set;}
    public string Reference{get;set;}="";
    public int ClienteId{get;set;}
    [JsonIgnore]public string Hash{get;set;}="";
    [JsonIgnore]public string Selection{get;set;}="";
    public string State{get;set;}="Registrando";
    public decimal Amount{get;set;}
    public string Currency{get;set;}="BOB";
    public string? ProviderId{get;set;}
    public string? PaymentUrl{get;set;}
    public string? QrUrl{get;set;}
    public DateTime CreatedAt{get;set;}=DateTime.UtcNow;
    public DateTime UpdatedAt{get;set;}=DateTime.UtcNow;
}
public sealed class PaymentTestInbox {
    public string Id{get;set;}="";
    public string? Reference{get;set;}
    public string? ProviderId{get;set;}
    public string State{get;set;}="Pendiente";
    public DateTime CreatedAt{get;set;}=DateTime.UtcNow;
}
public sealed class PaymentTestObligation {
    public Guid OrderId{get;set;}
    public int VentaId{get;set;}
}
