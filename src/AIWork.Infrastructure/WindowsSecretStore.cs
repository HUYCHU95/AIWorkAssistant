using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;
using AIWork.Application;
namespace AIWork.Infrastructure;
/// <summary>Secrets follow Windows CurrentUser DPAPI; never backed up or rendered. Re-enter on another machine.</summary>
public class WindowsSecretStore(string directory):ISecretStore
{
    public string? Read(string name)
    {
        if(!OperatingSystem.IsWindows())return null;
        var path=Path.Combine(directory,SafeName(name)+".dpapi");if(!File.Exists(path))return null;
        try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));}catch(CryptographicException){return null;}
    }
    public void Write(string name,string value)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Secret store requires Windows DPAPI.");
        if(value.Length>2048||value.Contains('\n')||value.Contains('\r'))throw new ArgumentException("Secret format invalid.");
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,SafeName(name)+".dpapi");var temp=path+".tmp";
        File.WriteAllBytes(temp,ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));File.Move(temp,path,true);
    }
    private static string SafeName(string name)=>name is "openai" or "smtp"?name:throw new ArgumentException("Unknown secret name");
}
