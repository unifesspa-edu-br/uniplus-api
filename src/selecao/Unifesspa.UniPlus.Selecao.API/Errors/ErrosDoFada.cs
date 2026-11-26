namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas do FADA. Predicado, derivação e descritor do fato vivem em Unifesspa.UniPlus.Regras (ADR-0135): os códigos deles são registrados uma única vez em RegrasDomainErrorRegistration.
/// </summary>
internal static class ErrosDoFada
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new("ConfiguracaoDoFada.RegraInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.regra_invalida", "A regra referenciada não é do código FADA_MULTIPLICATIVO")),
        new("ConfiguracaoDoFada.FatorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.fator_invalido", "O multiplicador do Fator de Atenuação das Desigualdades de Acesso (FADA) deve ser maior que zero")),
        new("ConfiguracaoDoFada.TetoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.teto_invalido", "O teto do Fator de Atenuação das Desigualdades de Acesso (FADA), quando informado, deve ser maior que zero")),
        new("ConfiguracaoDoFada.FatoElegibilidadeInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.fato_elegibilidade_invalido", "O código do fato de elegibilidade do acréscimo é inválido")),
        new("ConfiguracaoDoFada.CamposObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.campos_obrigatorios", "regra.versao, fator e baseLegal.id são obrigatórios quando regra.codigo é informado")),
        new("ConfiguracaoDoFada.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.regra_nao_encontrada", "Regra do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada no catálogo de regras")),
        new("ConfiguracaoDoFada.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.regra_tipo_invalido", "A regra referenciada não é uma regra do Fator de Atenuação das Desigualdades de Acesso (FADA)")),
        new("ConfiguracaoDoFada.BaseLegalNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.base_legal_nao_encontrada", "Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada ou desativada")),
        new("ConfiguracaoDoFada.SemMunicipios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_fada.sem_municipios", "A base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) deve abranger pelo menos um município")),
    ];
}
