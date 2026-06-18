using System.Threading.Channels;

namespace Moe.Modules.EducationAccountTopUp.IGateway;

public interface ITopUpRunQueueReader
{
    ChannelReader<long> Reader { get; }
}
