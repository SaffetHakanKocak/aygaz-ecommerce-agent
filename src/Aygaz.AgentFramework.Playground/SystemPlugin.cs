using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Playground;

public sealed class SystemPlugin
{
    [KernelFunction("get_system_name")]
    [Description("Sistemin adını döndürür.")]
    public string GetSystemName()
    {
        return "Aygaz Agent Framework";
    }
}
