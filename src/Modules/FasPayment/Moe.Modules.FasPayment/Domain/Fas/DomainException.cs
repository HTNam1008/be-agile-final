using System;

namespace Moe.Modules.FasPayment.Domain.Fas;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
