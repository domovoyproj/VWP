using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace VWP;

// The account comes from /user, never from a name supplied by the launcher.
public sealed class StarAccess : IDisposable
{
    sealed record Credential(string Token,string Login,long UserId,DateTime VerifiedUtc);
    readonly HttpClient client;
    readonly string file;
    Credential? credential;
    readonly SemaphoreSlim mutex=new(1);
    readonly CancellationTokenSource lifetime=new();
    int generation;
    bool disposed;
    public string Login=>credential?.Login??"";
    public bool Unlocked=>credential is not null && credential.VerifiedUtc<=DateTime.UtcNow.AddMinutes(5) && DateTime.UtcNow-credential.VerifiedUtc<TimeSpan.FromHours(24);
    public string Status {get;private set;}="Подключи GitHub и поставь звезду VWP, чтобы открыть город.";
    public StarAccess():this(new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","github-access.bin")){}
    internal StarAccess(HttpClient client,string file)
    {
        this.client=client;this.file=file;client.Timeout=TimeSpan.FromSeconds(20);
        try{if(File.Exists(file))credential=JsonSerializer.Deserialize<Credential>(ProtectedData.Unprotect(File.ReadAllBytes(file),Encoding.UTF8.GetBytes("VWP-GitHub-Star-v1"),DataProtectionScope.CurrentUser));}catch{credential=null;}
    }
    public async Task<bool> CheckAsync(string? token=null,CancellationToken cancel=default)
    {
        if(disposed)return false;
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancel,lifetime.Token);
        await mutex.WaitAsync(linked.Token);
        try
        {
            int checkingGeneration=Volatile.Read(ref generation);
            bool newAccount=token is not null;string key=(token??credential?.Token??"").Trim();
            if(key.Length<20||key.Length>512){Status="Вставь ключ GitHub с правом Starring: Read-only.";return false;}
            if(newAccount){credential=null;Delete();}
            using var user=await Request("user",key,linked.Token);
            if(disposed||checkingGeneration!=Volatile.Read(ref generation))return false;
            if(user.StatusCode==HttpStatusCode.Unauthorized){Disconnect();Status="Ключ истёк или отозван. Подключи GitHub снова.";return false;}
            if(!user.IsSuccessStatusCode){Status="GitHub временно недоступен. Повтори проверку позже.";return Unlocked;}
            using var json=JsonDocument.Parse(await user.Content.ReadAsStringAsync(linked.Token));
            if(!json.RootElement.TryGetProperty("login",out var name)||name.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(name.GetString())||!json.RootElement.TryGetProperty("id",out var userId)||!userId.TryGetInt64(out long id)||id<=0)throw new JsonException("Missing authenticated account");
            string login=name.GetString()!;
            if(credential is not null&&credential.UserId!=id)credential=null;
            using var star=await Request("user/starred/domovoyproj/VWP",key,linked.Token);
            if(disposed||checkingGeneration!=Volatile.Read(ref generation))return false;
            if(star.StatusCode==HttpStatusCode.NoContent){credential=new(key,login,id,DateTime.UtcNow);Save();Status="Город открыт · @"+login;return true;}
            if(star.StatusCode==HttpStatusCode.NotFound){credential=new(key,login,id,DateTime.MinValue);Save();Status="@"+login+": поставь звезду репозиторию и нажми «Проверить».";return false;}
            if(star.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden){credential=null;Delete();Status="GitHub не разрешил проверку. Нужны Starring: Read-only и доступ к публичному VWP.";return false;}
            Status="GitHub временно недоступен.";return Unlocked;
        }
        catch(OperationCanceledException) when(disposed){return false;}
        catch(OperationCanceledException) when(!cancel.IsCancellationRequested){Status=Unlocked?"Офлайн · последняя проверка действительна до 24 часов":"GitHub не ответил. Нужна проверка звезды.";return Unlocked;}
        catch(HttpRequestException){Status=Unlocked?"Офлайн · последняя проверка действительна до 24 часов":"Нет соединения с GitHub.";return Unlocked;}
        catch(JsonException){Status="Не удалось прочитать ответ GitHub.";return false;}
        finally{mutex.Release();}
    }
    async Task<HttpResponseMessage> Request(string path,string token,CancellationToken cancel)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/"+path);
        request.Headers.UserAgent.ParseAdd("VWP/0.7.0");request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
        return await client.SendAsync(request,cancel);
    }
    void Save(){Directory.CreateDirectory(Path.GetDirectoryName(file)!);byte[] bytes=ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(credential),Encoding.UTF8.GetBytes("VWP-GitHub-Star-v1"),DataProtectionScope.CurrentUser);File.WriteAllBytes(file+".tmp",bytes);File.Move(file+".tmp",file,true);}
    void Delete(){if(File.Exists(file))File.Delete(file);}
    public void Disconnect(){Interlocked.Increment(ref generation);credential=null;Delete();Status="GitHub отключён. Город заблокирован.";}
    // Pending checks still release the semaphore in their finally block.
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();client.Dispose();}
}
