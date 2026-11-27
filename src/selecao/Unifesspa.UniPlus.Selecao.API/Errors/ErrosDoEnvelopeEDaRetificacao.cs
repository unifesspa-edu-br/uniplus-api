namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

/// <summary>
/// Recusas do codec do envelope, da conferência da retificação e do estado editável da configuração.
/// </summary>
internal static class ErrosDoEnvelopeEDaRetificacao
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. DoCodecDoEnvelope(), .. DaRetificacaoEDoEstadoEditavel(), .. DoCicloNovoDaRetificacao()];

    private static KeyValuePair<string, DomainErrorMapping>[] DoCodecDoEnvelope() =>
    [
        // A leitura distingue versão desconhecida de evidência inválida.
        new("EnvelopeCodec.VersaoDesconhecida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.versao_desconhecida", "O sistema não reconhece a versão da configuração publicada")),
        new("EnvelopeCodec.AlgoritmoNaoSuportado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.algoritmo_nao_suportado", "O algoritmo de hash da versão não é o que o codec daquela forma emite")),
        new("EnvelopeCodec.IntegridadeViolada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.integridade_violada", "Os bytes congelados não produzem o hash registrado na versão")),
        new("EnvelopeCodec.EnvelopeMalformado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.envelope_malformado", "O envelope congelado não tem a forma que a versão dele declara")),
        new("EnvelopeCodec.EnvelopeIncoerenteComAVersao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.envelope_incoerente_com_a_versao", "O envelope contradiz a versão que o guarda")),
        new("EnvelopeCodec.BlocosDerivadosIncoerentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.blocos_derivados_incoerentes", "Distribuição, modalidades e ofertas não declaram o mesmo conjunto de ofertas de curso")),
        new("EnvelopeCodec.RegraDesconhecida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.regra_desconhecida", "O envelope referencia um código de regra fora do rol conhecido")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] DaRetificacaoEDoEstadoEditavel() =>
    [
        // O If-Match da inscrição que a gramática da RFC 9110 recusa: 400, antes de a operação ser tentada.
        new("Precondicao.Malformada", new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.selecao.precondicao_malformada", "If-Match sintaticamente inválido (RFC 9110 §8.8.3)")),
        // Fatos das versões publicadas (UNI-REQ-0144): o candidato pode ter preenchido um formulário
        // em qualquer versão publicada, então a retificação não usa, de outro formulário, fato que não foi
        // coletado em todas elas, e não retira da inscrição o fato que ela já coletava.
        new("Retificacao.FatoAusenteDeVersaoPublicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.fato_ausente_de_versao_publicada", "Fato de outro formulário não coletado em todas as versões publicadas")),
        new("Retificacao.FatoJaColetadoRemovido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.fato_ja_coletado_removido", "A retificação não retira da inscrição fato que ela já coleta")),
        // O candidato pode ter respondido o fato em qualquer versão publicada: a retificação não troca
        // o eixo que a versão publicada congelou, e a mudança vale para processo novo.
        new("Retificacao.EixoDoFatoJaColetadoAlterado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.eixo_do_fato_ja_coletado_alterado", "A retificação não troca o eixo de fato já coletado por versão publicada")),
        // A allowlist que falha FECHADA: só o rascunho aceita mutação da configuração, e todo estado
        // novo nasce imutável por omissão.
        new("ProcessoSeletivo.MutacaoForaDeEstadoEditavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.mutacao_fora_de_estado_editavel", "O processo não está em um estado que aceite mutação da configuração")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] DoCicloNovoDaRetificacao() =>
    [
        // A retificação que muda a regra ou os dados de um preliminar publicado declara o novo ciclo
        // (UNI-REQ-0157, UNI-REQ-0282), e só o preliminar publicado com ciclo aberto aceita a declaração.
        new(RetificacaoErrorCodes.CicloNovoNaoDeclarado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.ciclo_novo_nao_declarado", "A retificação exige declarar o novo ciclo do resultado preliminar já publicado")),
        new(RetificacaoErrorCodes.CicloNovoDeItemSemPreliminarAberto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.ciclo_novo_de_item_sem_preliminar_aberto", "O ciclo novo declarado não tem resultado preliminar publicado com o ciclo aberto")),
    ];
}
