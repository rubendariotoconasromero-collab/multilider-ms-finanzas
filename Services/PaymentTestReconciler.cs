namespace MSMultilider.Finanzas.Services;
public sealed class PaymentTestReconciler(IServiceScopeFactory scopes,IConfiguration config,ILogger<PaymentTestReconciler> logger):BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(!config.GetValue<bool>("PaymentTests:ReconciliationEnabled"))return;
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(1));
        while(await timer.WaitForNextTickAsync(stoppingToken)) {
            try {await using var scope=scopes.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<PaymentTests>().Reconcile(null,stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
            catch(Exception e){logger.LogWarning("Test-payment reconciliation pending: {FailureType}",e.GetType().Name);}
        }
    }
}
