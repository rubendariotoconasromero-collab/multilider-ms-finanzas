using MSMultilider.Finanzas.Infrastructure;
namespace MSMultilider.Finanzas.Models;
public sealed class CuentaVenta:Entity {
    public int NotaVentaId {get;set;}
    public int ClienteId {get;set;}
    public int AsesorId {get;set;}
    public string TipoVenta {get;set;}="";
    public string Moneda {get;set;}="BOB";
    public decimal MontoLiquido {get;set;}
    public decimal CuotaInicial {get;set;}
    public decimal MontoComision {get;set;}
    public string Estado {get;set;}="Activa";
}
public sealed class VentaAnulada:Entity { public int NotaVentaId {get;set;} }
public sealed class PlanPago:Entity {
    public int CuentaVentaId {get;set;}
    public decimal Monto {get;set;}
    public int NumeroCuotas {get;set;}
    public decimal TasaInteres {get;set;}
    public decimal TasaMora {get;set;}
    public DateOnly FechaInicio {get;set;}
    public DateOnly FechaFinal {get;set;}
}
public sealed class Cuota:Entity {
    public int PlanPagoId {get;set;}
    public int NumeroCuota {get;set;}
    public int? ReprogramacionId {get;set;}
    public DateOnly FechaVencimiento {get;set;}
    public decimal MontoCuota {get;set;}
    public decimal MontoCapital {get;set;}
    public decimal MontoInteres {get;set;}
    public decimal InteresAcreditado {get;set;}
    public decimal SaldoCapital {get;set;}
    public string Estado {get;set;}="Pendiente";
}
public sealed class Reprogramacion:Entity {
    public int PlanPagoId {get;set;}
    public int UsuarioId {get;set;}
    public Guid SolicitudId {get;set;}
    public string Motivo {get;set;}="";
    public DateOnly FechaReprogramacion {get;set;}
    public int CuotaDesde {get;set;}
    public decimal SaldoCapitalMomento {get;set;}
    public decimal NuevaTasaInteres {get;set;}
    public int NuevoNumeroCuotas {get;set;}
    public DateOnly NuevaFechaInicio {get;set;}
}
public sealed class Pago:Entity {
    public Guid? SolicitudId {get;set;}
    public int CuentaVentaId {get;set;}
    public int? CuotaId {get;set;}
    public int? MetodoPagoId {get;set;}
    public int? CuentaId {get;set;}
    public string ConceptoPago {get;set;}="";
    public DateOnly? FechaPago {get;set;}
    public decimal Monto {get;set;}
    public string Estado {get;set;}="PENDIENTE_PAGO";
    public string? Observaciones {get;set;}
    public int? UsuarioId {get;set;}
}
public sealed class Ingreso:Entity {
    public string? Comprobante{get;set;}
    public DateOnly Fecha {get;set;}
    public string Concepto {get;set;}="";
    public string Categoria {get;set;}="OTRO";
    public decimal Monto {get;set;}
    public string Moneda {get;set;}="BOB";
    public string Origen {get;set;}="MANUAL";
    public int? PagoId {get;set;}
    public int? CuentaVentaId {get;set;}
    public int? CuentaBancariaId {get;set;}
    public int UserId {get;set;}
    public string? Observaciones {get;set;}
    public string Estado {get;set;}="CONFIRMADO";
}
public sealed class Egreso:Entity {
    public DateOnly Fecha {get;set;}
    public string Concepto {get;set;}="";
    public string Categoria {get;set;}="OTRO";
    public decimal Monto {get;set;}
    public string Moneda {get;set;}="BOB";
    public string Origen {get;set;}="MANUAL";
    public int? CuentaVentaId {get;set;}
    public int? AsesorId {get;set;}
    public int? CuentaBancariaId {get;set;}
    public int UserId {get;set;}
    public string? Comprobante{get;set;}
    public string? Beneficiario {get;set;}
    public string? Observaciones {get;set;}
    public string Estado {get;set;}="PENDIENTE";
}
public sealed class MetodoPago:Entity { public string Nombre {get;set;}="";public bool Estado {get;set;}=true; }
public sealed class CuentaBancaria:Entity {
    public string Tipo{get;set;}="BANCARIA";
    public string? Descripcion{get;set;}
    public string? Iban{get;set;}
    public string? Proveedor{get;set;}
    public string? CodigoIntegracion{get;set;}
    public string Nombre {get;set;}="";
    public string Banco {get;set;}="";
    public string NumeroCuenta {get;set;}="";
    public string Titular {get;set;}="";
    public string Moneda {get;set;}="BOB";
    public decimal SaldoInicial {get;set;}
    public bool Estado {get;set;}=true;
}

