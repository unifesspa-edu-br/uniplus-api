namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Fase canônica (UNI-REQ-0139).</summary>
internal static class FaseCanonicaErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(FaseCanonicaErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.codigo_obrigatorio",
                "Código da fase canônica é obrigatório")),

        new(FaseCanonicaErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.codigo_formato_invalido",
                "Código da fase canônica em formato inválido")),

        new(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.codigo_fora_do_conjunto_canonico",
                "Código da fase fora do conjunto canônico")),

        new(FaseCanonicaErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.fase_canonica.codigo_ja_existe",
                "Já existe uma fase canônica ativa com este código")),

        new(FaseCanonicaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.nome_obrigatorio",
                "Nome da fase canônica é obrigatório")),

        new(FaseCanonicaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.nome_tamanho",
                "Tamanho do nome da fase canônica inválido")),

        new(FaseCanonicaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.descricao_tamanho",
                "Tamanho da descrição da fase canônica inválido")),

        new(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.agrupa_etapas_apenas_avaliacao",
                "Apenas a fase de avaliação agrupa etapas pontuadas")),

        new(FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.complementacao_apenas_fases_permitidas",
                "Complementação documental só é permitida nas fases de homologação e recursos")),

        new(FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.solicitacao_isencao_apenas_na_fase_de_isencao",
                "Apenas a fase de solicitação de isenção abre a janela de pedido de isenção")),

        new(FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.coleta_inscricao_e_isencao_mutuamente_exclusivas",
                "Uma fase não coleta inscrição e solicitação de isenção ao mesmo tempo")),

        new(FaseCanonicaErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.base_legal_tamanho",
                "Tamanho da base legal da fase canônica inválido")),

        new(FaseCanonicaErrorCodes.OrigemDataObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.origem_data_obrigatoria",
                "Origem da data da fase canônica é obrigatória")),

        new(FaseCanonicaErrorCodes.OrigemDataInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.origem_data_invalida",
                "Origem da data da fase canônica fora do domínio fechado")),

        new(FaseCanonicaErrorCodes.UnidadeSugeridaNaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.unidade_sugerida_nao_encontrada",
                "Unidade sugerida da fase canônica não encontrada ou não está mais viva")),

        new(FaseCanonicaErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.fase_canonica.nao_encontrada",
                "Fase canônica não encontrada")),
    ];
}
