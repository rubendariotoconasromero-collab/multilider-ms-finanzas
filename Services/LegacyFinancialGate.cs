using MSMultilider.Finanzas.Infrastructure;
namespace MSMultilider.Finanzas.Services;
public interface ILegacyFinancialGate { void RequireLegacy(); }
// The former HTTP registration/cancellation chain remains available only to characterization fixtures.
public sealed class LegacyFinancialGate:ILegacyFinancialGate {
    public void RequireLegacy()=>throw new BusinessException("Usa la venta/anulación coordinada mediante Comercial.",503);
}
