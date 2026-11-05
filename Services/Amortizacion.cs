using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Services;
public static class Amortizacion {
    public static DateOnly MonthOverflow(DateOnly date,int months=1) {
        var target=new DateOnly(date.Year,date.Month,1).AddMonths(months);
        return target.AddDays(date.Day-1);
    }
    public static List<Cuota> GenerarLaravel(decimal capital,int meses,decimal tasaAnual,DateOnly inicio,int desde=1) {
        Api.Require(capital>0&&capital==Api.Money(capital)&&meses is >=1 and <=600&&tasaAnual>=0&&inicio!=default,"Condiciones del plan inválidas.");
        var tasa=tasaAnual/1200m;var power=Math.Pow(1+(double)tasa,meses);
        Api.Require(double.IsFinite(power)&&power<1e28,"Las condiciones exceden la precisión admitida.");
        var factor=(decimal)power;var fija=factor==1?capital/meses:capital*tasa*factor/(factor-1);
        var saldo=capital;var booked=capital;var date=inicio;var result=new List<Cuota>();
        for(var i=0;i<meses;i++) {
            var interes=saldo*tasa;var principal=fija-interes;
            if(i==meses-1)principal=saldo;
            saldo-=principal;
            var bookedPrincipal=i==meses-1?booked:Math.Min(booked,Math.Max(0,Api.Money(principal)));
            var bookedInterest=Math.Max(0,Api.Money(interes));booked-=bookedPrincipal;
            result.Add(new Cuota{NumeroCuota=desde+i,FechaVencimiento=date,MontoCuota=bookedPrincipal+bookedInterest,MontoCapital=bookedPrincipal,
                MontoInteres=bookedInterest,SaldoCapital=booked});
            date=MonthOverflow(date);
        }
        return result;
    }
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

