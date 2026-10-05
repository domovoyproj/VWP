using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
namespace VWP;

public sealed record UpdateRelease(Version Version,string Url,string Digest,string Name);
public static class UpdateService
{
    public static Version CurrentVersion=>new(0,2,0);
    static readonly HttpClient client=new(){Timeout=TimeSpan.FromMinutes(10)};
    static UpdateService(){client.DefaultRequestHeaders.UserAgent.ParseAdd("VWP/"+CurrentVersion);}
    public static async Task<UpdateRelease?> Check()
    {
        using var response=await client.GetAsync("https://api.github.com/repos/domovoyproj/VWP/releases/latest");response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return ParseRelease(json.RootElement);
    }
    internal static UpdateRelease? ParseRelease(JsonElement json)
    {
        if(json.GetProperty("draft").GetBoolean()||json.GetProperty("prerelease").GetBoolean())return null;
        string tag=json.GetProperty("tag_name").GetString()??"";
        if(!Version.TryParse(tag.TrimStart('v'),out var version)||version<=CurrentVersion)return null;
        foreach(var asset in json.GetProperty("assets").EnumerateArray())
        {
            string name=asset.GetProperty("name").GetString()??"";
            if(name!=$"VWP-{version.ToString(3)}-Setup.exe")continue;
            string url=asset.GetProperty("browser_download_url").GetString()??"";
            string? digest=asset.TryGetProperty("digest",out var value)?value.GetString():null;
            if(!TrustedUrl(url)||digest is null||!digest.StartsWith("sha256:",StringComparison.Ordinal)||digest.Length!=71)throw new InvalidDataException("Нет проверяемого установщика в релизе.");
            return new(version,url,digest[7..],name);
        }
        return null;
    }
    static bool TrustedUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&uri.AbsolutePath.StartsWith("/domovoyproj/VWP/releases/download/",StringComparison.Ordinal);
    public static async Task<string> Download(UpdateRelease release,IProgress<double>? progress=null)
    {
        if(!TrustedUrl(release.Url)||release.Name!=System.IO.Path.GetFileName(release.Name))throw new InvalidDataException("Неверный адрес обновления.");
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","updates");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,release.Name),temp=path+".download";
        try
        {
            using var response=await client.GetAsync(release.Url,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
            using var stream=await response.Content.ReadAsStreamAsync();
            await using(var file=File.Create(temp))
            {
                byte[] buffer=new byte[81920];long total=0;int read;
                while((read=await stream.ReadAsync(buffer))>0){await file.WriteAsync(buffer.AsMemory(0,read));total+=read;progress?.Report(response.Content.Headers.ContentLength is long size?100.0*total/size:0);}
            }
            using var input=File.OpenRead(temp);string digest=Convert.ToHexString(await SHA256.HashDataAsync(input));
            if(!string.Equals(digest,release.Digest,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Контрольная сумма обновления не совпала.");
            input.Close();File.Move(temp,path,true);return path;
        }
        catch{if(File.Exists(temp))File.Delete(temp);throw;}
    }
}
