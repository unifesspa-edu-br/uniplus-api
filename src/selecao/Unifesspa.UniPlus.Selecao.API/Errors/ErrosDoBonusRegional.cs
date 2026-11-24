namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas do bônus regional. Predicado, derivação e descritor do fato vivem em Unifesspa.UniPlus.Regras (ADR-0135): os códigos deles são registrados uma única vez em RegrasDomainErrorRegistration.
/// </summary>
internal static class ErrosDoBonusRegional
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new("ConfiguracaoBonusRegional.RegraInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.regra_invalida", "A regra referenciada não é do código FADA_MULTIPLICATIVO")),
        new("ConfiguracaoBonusRegional.FatorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.fator_invalido", "O fator do bônus deve ser maior que zero")),
        new("ConfiguracaoBonusRegional.TetoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.teto_invalido", "O teto do bônus, quando informado, deve ser maior que zero")),
        new("ConfiguracaoBonusRegional.FatoElegibilidadeInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.fato_elegibilidade_invalido", "O código do fato de elegibilidade do acréscimo é inválido")),
        new("ConfiguracaoBonusRegional.CamposObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.campos_obrigatorios", "RegraVersao, Fator e BaseLegalBonusRegionalId são obrigatórios quando RegraCodigo é informado")),
        new("ConfiguracaoBonusRegional.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.regra_nao_encontrada", "Regra de bônus não encontrada no catálogo de regras")),
        new("ConfiguracaoBonusRegional.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.regra_tipo_invalido", "A regra referenciada não é do tipo regra_fada")),
        new("ConfiguracaoBonusRegional.BaseLegalNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.base_legal_nao_encontrada", "Base legal de bônus regional não encontrada ou desativada")),
        new("ConfiguracaoBonusRegional.SemMunicipios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.sem_municipios", "A base legal do bônus regional deve abranger pelo menos um município")),
    ];
}
