namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

/// <summary>
/// Mapeamento dos códigos de erro emitidos pelas regras sobre fatos do candidato
/// (<c>Unifesspa.UniPlus.Regras</c>): predicado, condição, cláusula, descritor do fato e
/// derivação. Registrados uma única vez, com código de wire sem prefixo de módulo, porque
/// Configuração e Seleção emitem os mesmos códigos e o registry é global — o último
/// registro de um código venceria sem aviso (ADR-0135).
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, RegrasDomainErrorRegistration>() em AddDomainErrorMapper().")]
internal sealed class RegrasDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. ErrosDaEstruturaDoFormulario.Mapeamentos,
        .. ErrosDoGrafoDoFormulario.Mapeamentos,
        .. ErrosDosGruposDoFormulario.Mapeamentos,
        .. ErrosDoItemDoFormulario.Mapeamentos,
        .. ErrosDoVinculoComOCatalogo.Mapeamentos,
        .. ErrosDosTermosDoFormulario.Mapeamentos,
        .. ErrosDaRestricaoDeValor.Mapeamentos,
        .. ErrosDoPredicado.Mapeamentos,
        .. ErrosDaDerivacaoDosFatos.Mapeamentos,
        .. ErrosDoFormularioPortavel.Mapeamentos,
    ];
}
