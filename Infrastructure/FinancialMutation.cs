using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Infrastructure;
public sealed class FinancialMutation(RequestDelegate next) {
    public async Task Invoke(HttpContext context,AppDbContext db) {
        if(context.User.Identity?.IsAuthenticated!=true||!context.Request.Path.StartsWithSegments("/api")||HttpMethods.IsGet(context.Request.Method)||HttpMethods.IsHead(context.Request.Method)||HttpMethods.IsOptions(context.Request.Method)) {
            await next(context);return;
        }
        context.Request.EnableBuffering();
        using var reader=new StreamReader(context.Request.Body,leaveOpen:true);var body=await reader.ReadToEndAsync(context.RequestAborted);context.Request.Body.Position=0;
        Guid? command=null;var raw=context.Request.Headers["Idempotency-Key"].ToString();
        JsonElement parsed=string.IsNullOrWhiteSpace(body)?JsonSerializer.SerializeToElement(new{}):JsonSerializer.Deserialize<JsonElement>(body);
        if(raw==""&&parsed.TryGetProperty("solicitud_id",out var id))raw=id.ToString();
        if(raw!="") {
            if(!Guid.TryParse(raw,out var key)||key==Guid.Empty)throw new BusinessException("Clave idempotente inválida.");
            command=key;
        }
        var hash=OperationProtocol.Hash(new{method=context.Request.Method,path=context.Request.Path.Value,body=parsed});
        var actor=int.Parse(context.User.FindFirstValue("sub")!);
        await using var tx=await db.Database.BeginTransactionAsync(context.RequestAborted);await OperationProtocol.Lock(db,"finance:operations",context.RequestAborted);
        if(command.HasValue&&await db.Set<FinancialCommand>().FindAsync([command.Value],context.RequestAborted) is {} receipt) {
            Api.Require(receipt.ActorId==actor,"Comando de otro actor.",403);Api.Require(receipt.Hash==hash,"UUID utilizado con otros datos.",409);
            context.Response.StatusCode=receipt.Status;context.Response.ContentType="application/json";
            context.Response.Headers["Idempotency-Replayed"]="true";await tx.CommitAsync(context.RequestAborted);await context.Response.WriteAsync(receipt.Result,context.RequestAborted);return;
        }
        var output=context.Response.Body;await using var buffer=new MemoryStream();context.Response.Body=buffer;
        try {
            await next(context);
            if(context.Response.StatusCode<400) {
                if(command.HasValue) {
                    buffer.Position=0;var result=await new StreamReader(buffer,leaveOpen:true).ReadToEndAsync(context.RequestAborted);
                    db.Add(new FinancialCommand{Id=command.Value,OperationId=parsed.TryGetProperty("operacion_id",out var operation)&&operation.TryGetGuid(out var op)?op:command.Value,
                        ActorId=actor,Hash=hash,Status=context.Response.StatusCode,Result=result});
                    await db.SaveChangesAsync(context.RequestAborted);
                }
                await tx.CommitAsync(context.RequestAborted);
            } else await tx.RollbackAsync(context.RequestAborted);
            context.Response.Body=output;buffer.Position=0;await buffer.CopyToAsync(output,context.RequestAborted);
        } finally {context.Response.Body=output;}
    }
}
