namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas da forma do documento da configuração do processo seletivo: o que o formato e os limites dele recusam, antes de
/// qualquer regra de negócio. <c>forma_invalida</c> é o grupo da recusa; os demais apontam a causa de cada item no
/// <c>errors[]</c>, pelo ponteiro do documento.
/// </summary>
internal static class ErrosDoDocumentoDaConfiguracao
{
    private const int Invalido = StatusCodes.Status422UnprocessableEntity;

    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new(ProcessoSeletivoErrorCodes.RegraCitadaNaoCorrespondeAoCatalogo, new DomainErrorMapping(Invalido, "uniplus.selecao.processo_seletivo.regra_citada_nao_corresponde_ao_catalogo", "A regra citada pela configuração não corresponde à regra atual do catálogo")),
        .. Grupo(),
        .. CausasDoEsquema(),
        .. CausasDaEstrutura(),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Grupo() =>
    [
        new(DocumentoDaConfiguracaoErrorCodes.FormaInvalida, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.forma_invalida", "O documento não respeita o formato da configuração do processo seletivo")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] CausasDoEsquema() =>
    [
        new(DocumentoDaConfiguracaoErrorCodes.CampoDesconhecido, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.campo_desconhecido", "O documento tem um campo que o formato não define")),
        new(DocumentoDaConfiguracaoErrorCodes.TipoInvalido, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.tipo_invalido", "O valor de um campo do documento tem o tipo errado")),
        new(DocumentoDaConfiguracaoErrorCodes.ValorInvalido, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.valor_invalido", "O valor de um campo do documento não respeita o formato")),
        new(DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.campo_obrigatorio_ausente", "O documento não traz um campo que o formato exige")),
        new(DocumentoDaConfiguracaoErrorCodes.VersaoDoFormatoNaoSuportada, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.versao_do_formato_nao_suportada", "O formato ou a versão do formato do documento não é suportado")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] CausasDaEstrutura() =>
    [
        new(DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.propriedade_repetida", "Um objeto do documento repete uma propriedade")),
        new(DocumentoDaConfiguracaoErrorCodes.ChaveRepetida, new DomainErrorMapping(Invalido, "uniplus.selecao.documento_da_configuracao.chave_repetida", "Duas partes do mesmo tipo têm a mesma chave")),
        new(DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite, new DomainErrorMapping(StatusCodes.Status413PayloadTooLarge, "uniplus.selecao.documento_da_configuracao.acima_do_limite", "O documento passa de um limite do formato")),
    ];
}
