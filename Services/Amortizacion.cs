using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Services;
public static class Amortizacion {
    public static List<Cuota> Generar(decimal capital,int meses,decimal tasaAnual,DateOnly inicio,int desde=1) {
        Api.Require(capital>0 && capital==Api.Money(capital) && meses is >=1 and <=600 && tasaAnual is >=0 and <=100 && inicio!=default,"Condiciones del plan inválidas.");
        Api.Require(capital>=meses*0.01m,"El capital es demasiado pequeño para la cantidad de cuotas.");
        var tasa=tasaAnual/1200m;
        var factor=(decimal)Math.Pow(1+(double)tasa,meses);
        var fija=tasa==0?Api.Money(capital/meses):Api.Money(capital*tasa*factor/(factor-1));
        var saldo=capital;var result=new List<Cuota>();
        for(var i=0;i<meses;i++) {
            var interes=Api.Money(saldo*tasa);
            var principal=i==meses-1?saldo:Math.Min(saldo-0.01m*(meses-i-1),Math.Max(0.01m,Api.Money(fija-interes)));
            Api.Require(principal>0,"Las condiciones no permiten amortizar el capital.");
            saldo=Api.Money(saldo-principal);
            result.Add(new Cuota {NumeroCuota=desde+i,FechaVencimiento=inicio.AddMonths(i),
                MontoCapital=principal,MontoInteres=interes,MontoCuota=principal+interes,SaldoCapital=saldo});
        }
        return result;
    }
}

