using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Kernel;

public interface IKernelFactory
{
    Microsoft.SemanticKernel.Kernel CreateKernel();
}
