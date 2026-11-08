using AIWork.Infrastructure;
using Xunit;
namespace AIWork.Tests;
public class SecretStoreTests
{
    [Fact]public void WindowsSecretsAreEncryptedAndNeverPlaintext()
    {
        if(!OperatingSystem.IsWindows())return;var folder=Path.Combine(Path.GetTempPath(),"AIWorkSecrets-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try{var secret="test-only-"+Guid.NewGuid().ToString("N");var store=new WindowsSecretStore(folder);store.Write("openai",secret);Assert.Equal(secret,store.Read("openai"));Assert.DoesNotContain(secret,System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,"openai.dpapi"))));Assert.Throws<ArgumentException>(()=>store.Write("../outside","test"));}finally{Directory.Delete(folder,true);}
    }
}
