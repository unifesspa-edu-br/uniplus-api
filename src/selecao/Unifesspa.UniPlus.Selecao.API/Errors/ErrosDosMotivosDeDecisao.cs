namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas do cadastro único de motivos de decisão (UNI-REQ-0283), da escolha dos motivos pelo
/// processo (UNI-REQ-0285) e do portão de cobertura dos motivos na publicação (UNI-REQ-0123).
/// </summary>
internal static class ErrosDosMotivosDeDecisao
{
    private const string Prefixo = "uniplus.selecao.motivo_de_decisao.";

    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. CamposDoCadastro(), .. SituacaoDoCadastro(), .. EscolhaECobertura()];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CamposDoCadastro() =>
    [
        Recusa(MotivoDeDecisaoErrorCodes.TipoObrigatorio, "tipo_obrigatorio", "Tipo do motivo é obrigatório"),
        Recusa(MotivoDeDecisaoErrorCodes.CodigoObrigatorio, "codigo_obrigatorio", "Código do motivo é obrigatório"),
        Recusa(MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido, "codigo_formato_invalido", "Código do motivo fora do formato aceito"),
        Mapear(MotivoDeDecisaoErrorCodes.CodigoJaExiste, StatusCodes.Status409Conflict, "codigo_ja_existe", "Já existe motivo com este código neste tipo"),
        Recusa(MotivoDeDecisaoErrorCodes.DescricaoObrigatoria, "descricao_obrigatoria", "Descrição do motivo é obrigatória"),
        Recusa(MotivoDeDecisaoErrorCodes.DescricaoTamanho, "descricao_tamanho", "Descrição do motivo fora do tamanho permitido"),
        Recusa(MotivoDeDecisaoErrorCodes.DescricaoCaractereInvalido, "descricao_caractere_invalido", "Descrição do motivo contém caractere não permitido"),
        Recusa(MotivoDeDecisaoErrorCodes.SentidoObrigatorio, "sentido_obrigatorio", "Motivo deve declarar exatamente um sentido"),
        Recusa(MotivoDeDecisaoErrorCodes.SentidoNaoAdmitido, "sentido_nao_admitido", "O tipo não admite motivo neste sentido"),
        Recusa(MotivoDeDecisaoErrorCodes.SubclassificacaoObrigatoria, "subclassificacao_obrigatoria", "O tipo exige subclassificação"),
        Recusa(MotivoDeDecisaoErrorCodes.SubclassificacaoInvalida, "subclassificacao_invalida", "Subclassificação desconhecida"),
        Recusa(MotivoDeDecisaoErrorCodes.SubclassificacaoNaoAdmitida, "subclassificacao_nao_admitida", "O tipo não tem subclassificação"),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> SituacaoDoCadastro() =>
    [
        Recusa(MotivoDeDecisaoErrorCodes.JaAtivo, "ja_ativo", "O motivo já está ativo"),
        Recusa(MotivoDeDecisaoErrorCodes.JaInativo, "ja_inativo", "O motivo já está inativo"),
        Mapear(MotivoDeDecisaoErrorCodes.NaoEncontrado, StatusCodes.Status404NotFound, "nao_encontrado", "Motivo de decisão não encontrado"),
        new(MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente, new DomainErrorMapping(StatusCodes.Status409Conflict, Prefixo + "situacao_alterada_concorrentemente", "O motivo foi alterado por outra operação", RetryableConflict: true)),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> EscolhaECobertura() =>
    [
        // Escolha pelo processo.
        Recusa(MotivoDeDecisaoErrorCodes.MotivoInativo, "motivo_inativo", "Motivo desativado não entra em nova escolha"),
        Mapear(MotivoDeDecisaoErrorCodes.JaEscolhido, StatusCodes.Status409Conflict, "ja_escolhido", "O processo já escolheu este motivo"),
        Mapear(MotivoDeDecisaoErrorCodes.NaoEscolhido, StatusCodes.Status404NotFound, "nao_escolhido", "O processo não escolheu este motivo"),
        Recusa(MotivoDeDecisaoErrorCodes.JustificativaObrigatoria, "justificativa_obrigatoria", "Inclusão depois da publicação exige justificativa"),
        Recusa(MotivoDeDecisaoErrorCodes.JustificativaTamanho, "justificativa_tamanho", "Justificativa fora do tamanho permitido"),
        Recusa(MotivoDeDecisaoErrorCodes.AvaliacaoIniciada, "avaliacao_iniciada", "A avaliação desta decisão já começou"),
        Recusa(MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao, "retirada_apos_publicacao", "Motivo publicado não é retirado do processo"),

        // Publicação, retificação e fechamento da sessão.
        Recusa(MotivoDeDecisaoErrorCodes.MotivoDesativado, "motivo_desativado", "Motivo escolhido foi desativado no cadastro"),
        Recusa(MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, "cobertura_insuficiente", "Motivos insuficientes para as decisões do processo"),
    ];

    private static KeyValuePair<string, DomainErrorMapping> Recusa(string codigo, string sufixo, string titulo) =>
        Mapear(codigo, StatusCodes.Status422UnprocessableEntity, sufixo, titulo);

    private static KeyValuePair<string, DomainErrorMapping> Mapear(string codigo, int status, string sufixo, string titulo) =>
        new(codigo, new DomainErrorMapping(status, Prefixo + sufixo, titulo));
}
