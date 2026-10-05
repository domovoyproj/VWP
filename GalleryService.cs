using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace VWP;
public sealed record GalleryItem(string Name,string Author,string Description,string Category,string Url,string Sha256,string Profile,string? Thumbnail=null);
public static class GalleryService
{
    public const string DefaultSource="https://raw.githubusercontent.com/domovoyproj/VWP/main/gallery/catalog.json";
    static readonly HttpClient client=new(){Timeout=TimeSpan.FromMinutes(15)};
    static readonly JsonSerializerOptions options=new(){PropertyNameCaseInsensitive=true};
    static GalleryService(){client.DefaultRequestHeaders.UserAgent.ParseAdd("VWP/"+UpdateService.CurrentVersion);}
    public static async Task<List<GalleryItem>> Catalog(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new InvalidDataException("Каталог должен использовать HTTPS.");
        bool github=uri.Host=="raw.githubusercontent.com";
        if(github)
        {
            string[] parts=uri.AbsolutePath.Trim('/').Split('/');
            if(parts.Length>=4)url="https://api.github.com/repos/"+parts[0]+"/"+parts[1]+"/contents/"+string.Join('/',parts.Skip(3))+"?ref="+Uri.EscapeDataString(Uri.UnescapeDataString(parts[2]));
        }
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.CacheControl=new CacheControlHeaderValue{NoCache=true};
        if(github)request.Headers.Accept.ParseAdd("application/vnd.github.raw+json");
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>1024*1024)throw new InvalidDataException("Большой каталог.");
        string json=await response.Content.ReadAsStringAsync();if(json.Length>1024*1024)throw new InvalidDataException("Большой каталог.");
        var items=JsonSerializer.Deserialize<List<GalleryItem>>(json,options)??new();if(items.Count>500)throw new InvalidDataException("Слишком много наборов.");return items;
    }
    public static async Task<string> Download(GalleryItem item,IProgress<double>? progress=null)
    {
        if(!Uri.TryCreate(item.Url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!Regex.IsMatch(item.Sha256,"^[0-9a-fA-F]{64}$"))throw new InvalidDataException("Неверный адрес или SHA-256 набора.");
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","downloads");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,item.Sha256.ToLowerInvariant()+".vwpbundle");if(File.Exists(file))return file;
        using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
        using var stream=await response.Content.ReadAsStreamAsync();long total=0;byte[] buffer=new byte[81920];
        try
        {
            await using(var output=File.Create(file+".tmp"))
            {int read;while((read=await stream.ReadAsync(buffer))>0){total+=read;if(total>4L*1024*1024*1024)throw new InvalidDataException("Набор превышает 4 ГБ.");await output.WriteAsync(buffer.AsMemory(0,read));progress?.Report(response.Content.Headers.ContentLength is long size?total*100.0/size:0);}}
            using(var input=File.OpenRead(file+".tmp")){if(!Convert.ToHexString(await SHA256.HashDataAsync(input)).Equals(item.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA-256 набора не совпала.");}
            File.Move(file+".tmp",file,true);return file;
        }
        finally{if(File.Exists(file+".tmp"))File.Delete(file+".tmp");}
    }
    public static async Task<string> Publish(string bundle,string repository,string token,string category,IProgress<string>? progress=null)
    {
        if(!Regex.IsMatch(repository,"^[A-Za-z0-9-]+/[A-Za-z0-9_.-]+$"))throw new ArgumentException("Репозиторий: автор/название.");
        using var api=new HttpClient{Timeout=TimeSpan.FromMinutes(15)};api.DefaultRequestHeaders.UserAgent.ParseAdd("VWP/"+UpdateService.CurrentVersion);api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        async Task<JsonDocument> Send(HttpMethod method,string path,object? data=null)
        {
            using var request=new HttpRequestMessage(method,"https://api.github.com/"+path);
            if(data is not null)request.Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json");
            using var response=await api.SendAsync(request);response.EnsureSuccessStatusCode();return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
        using var account=await Send(HttpMethod.Get,"user");string author=account.RootElement.GetProperty("login").GetString()!;
        if(!repository.StartsWith(author+"/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Публикация доступна в собственный репозиторий аккаунта.");
        using var repo=await Send(HttpMethod.Get,"repos/"+repository);if(repo.RootElement.GetProperty("private").GetBoolean())throw new InvalidOperationException("Для галереи нужен публичный репозиторий.");
        BundleManifest manifest;using(var archive=System.IO.Compression.ZipFile.OpenRead(bundle))using(var input=archive.GetEntry("manifest.json")!.Open())manifest=JsonSerializer.Deserialize<BundleManifest>(input)!;
        string tag="vwp-pack-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")[..6];
        progress?.Report("Создаю релиз набора…");using var release=await Send(HttpMethod.Post,"repos/"+repository+"/releases",new{tag_name=tag,name=manifest.Name,body="VWP bundle by @"+author,draft=true,prerelease=true});
        long id=release.RootElement.GetProperty("id").GetInt64();string upload=release.RootElement.GetProperty("upload_url").GetString()!.Split('{')[0]+"?name=collection.vwpbundle";
        progress?.Report("Загружаю набор…");using var content=new StreamContent(File.OpenRead(bundle));content.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
        using var uploaded=await api.PostAsync(upload,content);uploaded.EnsureSuccessStatusCode();using var asset=JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
        string url=asset.RootElement.GetProperty("browser_download_url").GetString()!;using var file=File.OpenRead(bundle);string hash=Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
        string? thumbnail=null;
        if(manifest.Scenes[0].Cover is string cover)
        {
            using var archive=System.IO.Compression.ZipFile.OpenRead(bundle);var entry=archive.GetEntry(cover);
            if(entry is not null && entry.Length<5*1024*1024)
            {
                using var input=entry.Open();using var buffer=new MemoryStream();await input.CopyToAsync(buffer);using var preview=new ByteArrayContent(buffer.ToArray());preview.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
                using var uploadedCover=await api.PostAsync(upload.Split('?')[0]+"?name=cover"+Path.GetExtension(cover),preview);uploadedCover.EnsureSuccessStatusCode();using var coverJson=JsonDocument.Parse(await uploadedCover.Content.ReadAsStringAsync());thumbnail=coverJson.RootElement.GetProperty("browser_download_url").GetString();
            }
        }
        using var published=await Send(HttpMethod.Patch,"repos/"+repository+"/releases/"+id,new{draft=false});
        var publicAssets=published.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        url=publicAssets.First(a=>a.GetProperty("name").GetString()=="collection.vwpbundle").GetProperty("browser_download_url").GetString()!;
        thumbnail=publicAssets.FirstOrDefault(a=>a.GetProperty("name").GetString()!.StartsWith("cover.",StringComparison.Ordinal)).ValueKind==JsonValueKind.Undefined?null:publicAssets.First(a=>a.GetProperty("name").GetString()!.StartsWith("cover.",StringComparison.Ordinal)).GetProperty("browser_download_url").GetString();
        string path="repos/"+repository+"/contents/gallery/catalog.json";string? sha=null;var catalog=new List<GalleryItem>();
        using(var current=await api.GetAsync("https://api.github.com/"+path))
        {
            if(current.IsSuccessStatusCode){using var json=JsonDocument.Parse(await current.Content.ReadAsStringAsync());sha=json.RootElement.GetProperty("sha").GetString();string encoded=json.RootElement.GetProperty("content").GetString()!.Replace("\n","");catalog=JsonSerializer.Deserialize<List<GalleryItem>>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)),options)??new();}
            else if(current.StatusCode!=System.Net.HttpStatusCode.NotFound)current.EnsureSuccessStatusCode();
        }
        catalog.RemoveAll(item=>item.Name==manifest.Name && item.Author.Equals(author,StringComparison.OrdinalIgnoreCase));
        catalog.Add(new(manifest.Name,author,"Коллекция из "+manifest.Scenes.Count+" сцен",category,url,hash,"https://github.com/"+author,thumbnail));
        var payload=new Dictionary<string,object?>{{"message","Publish VWP collection: "+manifest.Name},{"content",Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(catalog)))}};if(sha is not null)payload["sha"]=sha;
        progress?.Report("Обновляю каталог автора…");using var saved=await Send(HttpMethod.Put,path,payload);
        return "https://raw.githubusercontent.com/"+repository+"/"+repo.RootElement.GetProperty("default_branch").GetString()+"/gallery/catalog.json";
    }
}
