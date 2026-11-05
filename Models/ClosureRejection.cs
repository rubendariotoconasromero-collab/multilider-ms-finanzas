namespace MSMultilider.Finanzas.Models;
// A denied command cannot become admissible when a later payment is reversed.
public sealed class ClosureRejection {
    public Guid Id{get;set;}
    public Guid ParentOperationId{get;set;}
    public int PropertyId{get;set;}
    public string Reason{get;set;}="";
    public DateTime RejectedAt{get;set;}=DateTime.UtcNow;
}
