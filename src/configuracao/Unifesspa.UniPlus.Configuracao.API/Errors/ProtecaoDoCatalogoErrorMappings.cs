namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Aprovação da proteção de dados do catálogo de tipos de documento e de fatos do candidato (ADR-0153).</summary>
internal static class ProtecaoDoCatalogoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(ProtecaoDoCatalogoErrorCodes.SolicitacaoSemItem,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.solicitacao_sem_item",
                "A solicitação não traz nenhum item")),

        new(ProtecaoDoCatalogoErrorCodes.ItemNaoPendente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.item_nao_pendente",
                "O item da solicitação não existe ou não está pendente")),

        new(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.item_fora_de_solicitacao",
                "O item não está numa solicitação da proteção de dados")),

        new(ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.recomendacao_obrigatoria",
                "O ajuste exige a classificação de proteção de dados recomendada")),

        new(ProtecaoDoCatalogoErrorCodes.SemRecomendacaoParaAceitar,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.sem_recomendacao_para_aceitar",
                "O item não tem ajuste recomendado pelo Representante do controlador: não há recomendação a aceitar")),

        new(ProtecaoDoCatalogoErrorCodes.RecomendacaoIncompativelComAHipoteseLegal,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.recomendacao_incompativel_com_a_hipotese_legal",
                "A hipótese legal do item não cabe na classificação recomendada")),

        new(ProtecaoDoCatalogoErrorCodes.SemQuemAprove,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.sem_quem_aprove",
                "Ninguém tem a permissão de aprovar a proteção de dados do catálogo de tipos de documento e de fatos do candidato")),

        new(ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo,
            new DomainErrorMapping(
                StatusCodes.Status403Forbidden,
                "uniplus.configuracao.protecao_do_catalogo.sem_permissao",
                "Sem permissão sobre a proteção de dados do catálogo de tipos de documento e de fatos do candidato")),

        new(ProtecaoDoCatalogoErrorCodes.IdentidadeIncompleta,
            new DomainErrorMapping(
                StatusCodes.Status401Unauthorized,
                "uniplus.configuracao.protecao_do_catalogo.identidade_incompleta",
                "Identidade do solicitante incompleta")),

        new(ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.protecao_do_catalogo.conflito_de_concorrencia",
                "Um tipo de documento ou fato do candidato foi alterado concorrentemente",
                RetryableConflict: true)),

        new(TipoDocumentoErrorCodes.ConflitoDeConcorrencia,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_documento.conflito_de_concorrencia",
                "O tipo de documento foi alterado concorrentemente",
                RetryableConflict: true)),

        new(ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.protecao_conferida_invalida",
                "A decisão exige a proteção conferida, dentro do vocabulário fechado")),

        new(ProtecaoDoCatalogoErrorCodes.ProtecaoMudou,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.protecao_do_catalogo.protecao_mudou",
                "A proteção do item mudou depois de conferida; releia o item")),

        new(ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.recomendacao_igual_a_atual",
                "A classificação recomendada é a que o item já tem")),

        new(ProtecaoDoCatalogoErrorCodes.SituacaoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.situacao_invalida",
                "Situação fora do vocabulário da aprovação da proteção de dados")),

        new(ProtecaoDoCatalogoErrorCodes.CadastroInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.cadastro_invalido",
                "Cadastro do item fora do vocabulário da aprovação da proteção de dados")),

        new(ProtecaoDoCatalogoErrorCodes.DecisaoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.decisao_invalida",
                "Decisão fora do vocabulário da aprovação da proteção de dados")),

        new(ProtecaoDoCatalogoErrorCodes.JustificativaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.justificativa_obrigatoria",
                "A reprovação exige a justificativa")),

        new(ProtecaoDoCatalogoErrorCodes.JustificativaMuitoLonga,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.justificativa_muito_longa",
                "A justificativa passa do limite de 2.000 caracteres")),

        new(ProtecaoDoCatalogoErrorCodes.DadoDaDecisaoNaoCabe,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.dado_da_decisao_nao_cabe",
                "A decisão traz classificação recomendada que ela não admite")),

        new(ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.justificativa_exigida_pela_manifestacao",
                "Aprovar ou recomendar ajuste diante de manifestação desfavorável vigente exige a justificativa")),

        new(ProtecaoDoCatalogoErrorCodes.ParecerInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.parecer_invalido",
                "Parecer fora do vocabulário da manifestação")),

        new(ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.texto_da_manifestacao_obrigatorio",
                "Os pareceres recomenda ajuste e inadequada exigem o texto")),

        new(ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoMuitoLongo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.texto_da_manifestacao_muito_longo",
                "O texto da manifestação passa do limite de 2.000 caracteres")),

        new(ProtecaoDoCatalogoErrorCodes.CicloInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.ciclo_invalido",
                "Ciclo de aprovação ausente ou inválido")),

        new(ProtecaoDoCatalogoErrorCodes.ManifestacoesNoCicloInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.manifestacoes_no_ciclo_invalida",
                "Contagem de manifestações lida no item ausente ou inválida")),

        new(ProtecaoDoCatalogoErrorCodes.ManifestacaoNaoLida,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.protecao_do_catalogo.manifestacao_nao_lida",
                "Há manifestação técnica registrada depois da leitura; releia o item")),

        new(ProtecaoDoCatalogoErrorCodes.DecisaoJaTomada,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.protecao_do_catalogo.decisao_ja_tomada",
                "O item foi decidido depois da leitura; releia o item")),

        new(ProtecaoDoCatalogoErrorCodes.CicloMudou,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.protecao_do_catalogo.ciclo_mudou",
                "O ciclo de aprovação do item mudou depois de lido; releia o item")),

        new(ProtecaoDoCatalogoErrorCodes.AceiteDeItemReprovado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.aceite_de_item_reprovado",
                "O item reprovado não tem recomendação a aceitar")),

        new(ProtecaoDoCatalogoErrorCodes.AtoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.ato_invalido",
                "Decisão fora do vocabulário das decisões registradas da proteção de dados")),

        new(ProtecaoDoCatalogoErrorCodes.PeriodoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.protecao_do_catalogo.periodo_invalido",
                "A data inicial do período é posterior à final")),
    ];
}
