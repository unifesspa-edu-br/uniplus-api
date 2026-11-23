namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

/// <summary>
/// Mapeamento dos códigos de erro emitidos pelas regras sobre fatos do candidato
/// (<c>Unifesspa.UniPlus.Regras</c>): predicado, condição, cláusula, descritor do fato e
/// derivação. Registrados uma única vez porque Configuração e Seleção emitem os mesmos códigos
/// e o registro de erros é global — o último
/// registro de um código venceria sem aviso (ADR-0135).
/// Só entram os códigos que alcançam a resposta HTTP (ADR-0024); cada família vive na sua classe.
/// </summary>
public sealed class RegrasDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. ErrosDaEstruturaDoFormulario.Mapeamentos,
        .. ErrosDoGrafoDoFormulario.Mapeamentos,
        .. ErrosDosGruposDoFormulario.Mapeamentos,
        .. ErrosDoItemDoFormulario.Mapeamentos,
        .. ErrosDosDocumentos.Mapeamentos,
        .. ErrosDoVinculoComOCatalogo.Mapeamentos,
        .. ErrosDosTermosDoFormulario.Mapeamentos,
        .. ErrosDaRestricaoDeValor.Mapeamentos,
        .. ErrosDoPredicado.Mapeamentos,
        .. ErrosDaDerivacaoDosFatos.Mapeamentos,
        .. ErrosDoFormularioPortavel.Mapeamentos,
    ];
}
