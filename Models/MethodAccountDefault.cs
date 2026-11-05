using MSMultilider.Finanzas.Infrastructure;
namespace MSMultilider.Finanzas.Models;
public sealed class MethodAccountDefault:Entity {
    public int MetodoPagoId{get;set;}
    public int CuentaBancariaId{get;set;}
}
