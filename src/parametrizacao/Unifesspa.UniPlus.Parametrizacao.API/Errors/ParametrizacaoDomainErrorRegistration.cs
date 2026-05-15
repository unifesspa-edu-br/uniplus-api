namespace Unifesspa.UniPlus.Parametrizacao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Registry de mapeamentos de erros de domínio do Parametrizacao para wire
/// codes / status HTTP. Vazio em V1 — as entidades (Modalidade,
/// NecessidadeEspecial, TipoDocumento, Endereco) entram em F2 com seus
/// próprios códigos <c>uniplus.parametrizacao.*</c>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, ParametrizacaoDomainErrorRegistration>().")]
internal sealed class ParametrizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() => [];
}
