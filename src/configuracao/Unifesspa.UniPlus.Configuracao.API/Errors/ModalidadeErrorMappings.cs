namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Modalidade de concorrência (UNI-REQ-0011).</summary>
internal static class ModalidadeErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(ModalidadeErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.codigo_obrigatorio",
                "Código da modalidade é obrigatório")),

        new(ModalidadeErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.codigo_formato_invalido",
                "Código da modalidade em formato inválido")),

        new(ModalidadeErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.modalidade.codigo_ja_existe",
                "Já existe uma modalidade ativa com este código")),

        new(ModalidadeErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.descricao_tamanho",
                "Tamanho da descrição da modalidade inválido")),

        new(ModalidadeErrorCodes.NaturezaInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.natureza_invalida",
                "Natureza legal da modalidade fora do domínio fechado")),

        new(ModalidadeErrorCodes.ComposicaoVagasInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.composicao_vagas_invalida",
                "Composição de vagas da modalidade fora do domínio fechado")),

        new(ModalidadeErrorCodes.RegraRemanejamentoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_remanejamento_invalida",
                "Regra de remanejamento da modalidade fora do domínio fechado")),

        new(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.natureza_remanejamento_incoerente",
                "Regra de remanejamento incoerente com a natureza legal da modalidade")),

        new(ModalidadeErrorCodes.OrigemObrigatoriaParaRetiraDe,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.origem_obrigatoria_para_retira_de",
                "A modalidade com composição 'retira de' exige a modalidade de origem")),

        new(ModalidadeErrorCodes.ComposicaoOrigemCircular,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.composicao_origem_circular",
                "A modalidade não pode ser a origem da própria composição")),

        new(ModalidadeErrorCodes.RemanejamentoDestinoCircular,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.remanejamento_destino_circular",
                "A modalidade não pode ser o destino do próprio remanejamento")),

        new(ModalidadeErrorCodes.RemanejamentoParCircular,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.remanejamento_par_circular",
                "A modalidade não pode ser o par do próprio remanejamento")),

        new(ModalidadeErrorCodes.RemanejamentoFallbackCircular,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.remanejamento_fallback_circular",
                "A modalidade não pode ser o fallback do próprio remanejamento")),

        new(ModalidadeErrorCodes.OrigemApenasParaRetiraDe,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.origem_apenas_para_retira_de",
                "Somente a composição 'retira de' admite modalidade de origem")),

        new(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.argumento_remanejamento_obrigatorio",
                "Argumentos de remanejamento incompatíveis com a regra declarada")),

        new(ModalidadeErrorCodes.AcaoIndeferimentoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.acao_indeferimento_invalida",
                "Ação no indeferimento fora do domínio fechado")),

        new(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.referencia_inexistente_ou_inativa",
                "Modalidade referenciada (origem/destino/par/fallback) inexistente ou inativa")),

        new(ModalidadeErrorCodes.RemocaoBloqueadaPorReferencia,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.modalidade.remocao_bloqueada_por_referencia",
                "Não é possível remover uma modalidade referenciada por outra modalidade ativa")),

        new(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.estrutura_protegida_nao_editavel",
                "A modalidade legal fixa admite alteração apenas de descrição e base legal")),

        new(ModalidadeErrorCodes.RemocaoBloqueadaCodigoProtegido,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.modalidade.remocao_bloqueada_codigo_protegido",
                "A modalidade legal fixa não pode ser removida")),

        new(ModalidadeErrorCodes.CriacaoBloqueadaCodigoProtegido,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.modalidade.criacao_bloqueada_codigo_protegido",
                "Código reservado ao catálogo legal fixo de modalidades")),

        new(ModalidadeErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.base_legal_tamanho",
                "Tamanho da base legal da modalidade inválido")),

        new(ModalidadeErrorCodes.RegraDeAplicabilidadeObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_de_aplicabilidade_obrigatoria",
                "A modalidade precisa da regra que diz quem concorre a ela")),

        new(ModalidadeErrorCodes.RegraDeAplicabilidadeMalformada,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_de_aplicabilidade_malformada",
                "A regra de aplicabilidade traz condição nula")),

        new(ModalidadeErrorCodes.AmplaConcorrenciaComCondicao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.ampla_concorrencia_com_condicao",
                "A ampla concorrência vale para todo candidato e não admite condição")),

        new(ModalidadeErrorCodes.CotaSemOpcaoPelasCotasDaLei,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.cota_sem_opcao_pelas_cotas_da_lei",
                "A cota da lei exige a opção pelas cotas da lei em toda cláusula")),

        new(ModalidadeErrorCodes.AcaoAfirmativaComOpcaoPelasCotasDaLei,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.acao_afirmativa_com_opcao_pelas_cotas_da_lei",
                "A ação afirmativa exige não ter optado pelas cotas da lei em toda cláusula")),

        new(ModalidadeErrorCodes.RegraCitaFatoNaoCitavel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_cita_fato_nao_citavel",
                "A regra da modalidade cita fato que não pode entrar em regra")),

        new(ModalidadeErrorCodes.RegraCitaFatoDesativado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_cita_fato_desativado",
                "A regra da modalidade cita fato desativado")),

        new(ModalidadeErrorCodes.RegraCitaValorDesativado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_cita_valor_desativado",
                "A regra da modalidade cita valor desativado")),

        new(ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_cita_fato_posterior_a_inscricao",
                "A regra da modalidade cita fato que só é conhecido depois da inscrição")),

        new(ModalidadeErrorCodes.RegraDependeDaModalidade,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_depende_da_modalidade",
                "A regra da modalidade depende da própria modalidade")),

        new(ModalidadeErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.modalidade.nao_encontrada",
                "Modalidade não encontrada")),
    ];
}
