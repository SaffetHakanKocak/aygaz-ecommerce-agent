using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Playground;

public sealed class MathPlugin
{
    [KernelFunction("add_numbers")]
    [Description("İki tam sayıyı toplar ve sonucu döndürür.")]
    public int AddNumbers(
        [Description("Toplanacak birinci tam sayı.")] int a,
        [Description("Toplanacak ikinci tam sayı.")] int b)
    {
        return a + b;
    }
}
