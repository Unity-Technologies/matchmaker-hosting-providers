using Unity.Services.CloudCode.Core;

namespace HelloWorld;

public class MyModule
{
    [CloudCodeFunction("SayHello")]
    public string Hello(string name)
    {
        return $"Hello, {name}!";
    }
}


