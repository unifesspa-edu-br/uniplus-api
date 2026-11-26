namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Registry de mapeamentos de erros de domínio do Configuracao para wire codes / status HTTP.
/// Agrega as tabelas de cada cadastro, uma por arquivo <c>*ErrorMappings</c>, na ordem em que o
/// <c>DomainErrorMappingRegistry</c> as aplica. A referência de cidade do Geo (ADR-0090), a de
/// endereço estruturado (ADR-0096) e os parâmetros de consulta das listagens são compartilhados
/// com outros módulos e registrados uma única vez em <c>KernelDomainErrorRegistration</c>: o
/// registry agrega todo <c>IDomainErrorRegistration</c> num dicionário por código, e repeti-los
/// aqui sobrescreveria em silêncio o mapeamento de outro módulo, conforme a ordem de DI.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, ConfiguracaoDomainErrorRegistration>().")]
internal sealed class ConfiguracaoDomainErrorRegistration : IDomainErrorRegistration
{
    // A ordem vale: o registro global guarda o último mapeamento de um código repetido.
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
        [.. DaOfertaEDoCandidato(), .. DoProcessoSeletivo(), .. DosCursosEDemaisCadastros()];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaOfertaEDoCandidato() =>
    [
        .. CampusErrorMappings.Todos,
        .. LocalOfertaErrorMappings.Todos,
        .. ReferenciaReservaDemograficaErrorMappings.Todos,
        .. PesoAreaEnemErrorMappings.Todos,
        .. TipoDocumentoErrorMappings.Todos,
        .. CategoriaDocumentoErrorMappings.Todos,
        .. CondicaoAtendimentoErrorMappings.Todos,
        .. RecursoAcessibilidadeErrorMappings.Todos,
        .. TipoDeficienciaErrorMappings.Todos,
        .. FatoCandidatoErrorMappings.Todos,
        .. FatoValorDominioErrorMappings.Todos,
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoProcessoSeletivo() =>
    [
        .. ModalidadeErrorMappings.Todos,
        .. FaseCanonicaErrorMappings.Todos,
        .. PrecedenciaFaseErrorMappings.Todos,
        .. TipoBancaErrorMappings.Todos,
        .. TipoProcessoErrorMappings.Todos,
        .. ModeloFormularioErrorMappings.Todos,
        .. TipoEtapaErrorMappings.Todos,
        .. TipoConceitoErrorMappings.Todos,
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DosCursosEDemaisCadastros() =>
    [
        .. CursoErrorMappings.Todos,
        .. OfertaCursoErrorMappings.Todos,
        .. CalendarioDiasUteisErrorMappings.Todos,
        .. TermoConsentimentoErrorMappings.Todos,
        .. BaseLegalDoFadaErrorMappings.Todos,
        .. ProtecaoDoCatalogoErrorMappings.Todos,
    ];
}
