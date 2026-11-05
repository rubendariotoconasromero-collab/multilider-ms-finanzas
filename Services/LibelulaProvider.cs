using System.Text;
using System.Text.Json;
using Multilider.Operations;
namespace MSMultilider.Finanzas.Services;
public interface ILibelulaProvider {
    void EnsureReady() { }
    Task<JsonElement> Register(object data,CancellationToken ct);
    Task<bool> Verify(string reference,string currency,decimal amount,DateTime created,CancellationToken ct);
}
public sealed class LibelulaProvider(IConfiguration config,IHttpClientFactory clients):ILibelulaProvider {
    public void EnsureReady() {
        var url=config["Libelula:BaseUrl"]??"https://api.libelula.bo";
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||string.IsNullOrEmpty(config["Libelula:ApiKey"]))
            throw new ProtocolException("Configura la integración Libélula existente para Finanzas.");
    }
    private async Task<JsonElement> Send(string path,Dictionary<string,object?> data,CancellationToken ct) {
        EnsureReady();var url=config["Libelula:BaseUrl"]??"https://api.libelula.bo";
        data["appkey"]=config["Libelula:ApiKey"];
        using var request=new HttpRequestMessage(HttpMethod.Post,url.TrimEnd('/')+"/"+path);
        request.Content=new StringContent(JsonSerializer.Serialize(data,OperationProtocol.Json),Encoding.UTF8,"application/json");
        using var response=await clients.CreateClient("libelula").SendAsync(request,ct);
        response.EnsureSuccessStatusCode();
        var bytes=await response.Content.ReadAsByteArrayAsync(ct);
        if(bytes.Length>1024*1024)throw new ProtocolException("Respuesta Libélula inválida.");
        var json=JsonSerializer.Deserialize<JsonElement>(bytes);
        if(!json.TryGetProperty("error",out var error)||error.ToString()!="0")throw new ProtocolException("Libélula no confirmó la consulta.");
        return json;
    }
    public Task<JsonElement> Register(object data,CancellationToken ct)=>Send("rest/deuda/registrar",
        JsonSerializer.Deserialize<Dictionary<string,object?>>(JsonSerializer.Serialize(data,OperationProtocol.Json))!,ct);
    public async Task<bool> Verify(string reference,string currency,decimal amount,DateTime created,CancellationToken ct) {
        var day=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.SpecifyKind(created,DateTimeKind.Utc),"America/La_Paz").Date;
        var json=await Send("rest/deuda/consultar_deudas/por_fechas",new(){["fecha_inicial"]=day.ToString("yyyy-MM-dd HH:mm:ss"),["fecha_final"]=day.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss")},ct);
        if(!json.TryGetProperty("datos",out var rows)||rows.ValueKind!=JsonValueKind.Array)throw new ProtocolException("Contrato de conciliación no reconocido.");
        var matches=rows.EnumerateArray().Where(r=>r.TryGetProperty("identificador",out var id)&&id.GetString()==reference).ToArray();
        return matches.Length==1&&matches[0].TryGetProperty("pagado",out var paid)&&paid.ValueKind==JsonValueKind.True&&
            matches[0].TryGetProperty("moneda",out var money)&&money.GetString()==currency&&
            matches[0].TryGetProperty("valor_total",out var total)&&decimal.TryParse(total.ToString(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var actual)&&actual==amount;
    }
}
