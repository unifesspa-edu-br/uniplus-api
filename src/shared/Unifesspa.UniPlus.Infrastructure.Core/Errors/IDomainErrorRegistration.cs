namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

public interface IDomainErrorRegistration
{
    IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings();
}
