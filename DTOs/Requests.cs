using System.ComponentModel.DataAnnotations;
namespace MSMultilider.Finanzas.DTOs;
public sealed record VentaFinanciera(int Id,int ClienteId,int AsesorId,int PropiedadId,DateOnly Fecha,string Estado,
    string TipoVenta,string Moneda,decimal MontoLiquido,decimal CuotaInicial,decimal SaldoCredito,
    decimal MontoComision,int NumeroCuotas,decimal TasaInteres,decimal TasaMora,DateOnly? FechaInicioPago);
public sealed record PagoRequest(Guid SolicitudId,[Range(1,int.MaxValue)]int NotaVentaId,
    [Range(typeof(decimal),"0.01","1000000000000",ParseLimitsInInvariantCulture=true,ConvertValueInInvariantCulture=true)]decimal Monto,[Range(1,int.MaxValue)]int MetodoPagoId,
    [Range(1,int.MaxValue)]int CuentaId,DateOnly FechaPago,[Range(1,int.MaxValue)]int? CuotaId=null,
    [StringLength(1000)]string? Observaciones=null);
public sealed record ProcesarPagoRequest(Guid SolicitudId,[Range(1,int.MaxValue)]int MetodoPagoId,
    [Range(1,int.MaxValue)]int CuentaId,DateOnly FechaPago,[StringLength(1000)]string? Observaciones=null);
public sealed record AmortizarRequest(Guid SolicitudId,[Range(typeof(decimal),"0.01","1000000000000",ParseLimitsInInvariantCulture=true,ConvertValueInInvariantCulture=true)]decimal Monto,
    [Range(1,int.MaxValue)]int MetodoPagoId,[Range(1,int.MaxValue)]int CuentaId,DateOnly FechaPago,
    [StringLength(1000)]string? Observaciones=null);
public sealed record ReprogramarRequest(Guid SolicitudId,[Required,StringLength(255)]string Motivo,
    DateOnly FechaReprogramacion,[Range(1,int.MaxValue)]int CuotaDesde,
    [Range(typeof(decimal),"0","100",ParseLimitsInInvariantCulture=true,ConvertValueInInvariantCulture=true)]decimal NuevaTasaInteres,[Range(1,600)]int NuevoNumeroCuotas,DateOnly NuevaFechaInicio);
public sealed record MetodoRequest([Required,StringLength(100)]string Nombre,bool Estado=true);
public sealed record CuentaRequest([Required,StringLength(150)]string Nombre,[Required,StringLength(150)]string Banco,
    [Required,StringLength(100)]string NumeroCuenta,[Required,StringLength(150)]string Titular,
    [Required]string Moneda,[Range(typeof(decimal),"0","1000000000000",ParseLimitsInInvariantCulture=true,ConvertValueInInvariantCulture=true)]decimal SaldoInicial=0,bool Estado=true);
public sealed record MovimientoRequest(DateOnly Fecha,[Required,StringLength(255)]string Concepto,[Required,StringLength(100)]string Categoria,
    [Range(typeof(decimal),"0.01","1000000000000",ParseLimitsInInvariantCulture=true,ConvertValueInInvariantCulture=true)]decimal Monto,[Required]string Moneda,
    [Range(1,int.MaxValue)]int CuentaBancariaId,[StringLength(1000)]string? Observaciones=null,
    [StringLength(150)]string? Beneficiario=null);

