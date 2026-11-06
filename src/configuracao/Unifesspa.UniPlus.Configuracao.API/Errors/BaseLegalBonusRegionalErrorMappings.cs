namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal static class BaseLegalBonusRegionalErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.tipo_instrumento_invalido",
                "Tipo de instrumento normativo inválido ou não informado")),

        new(BaseLegalBonusRegionalErrorCodes.IdentificacaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.identificacao_obrigatoria",
                "Identificação da base legal de bônus regional é obrigatória")),

        new(BaseLegalBonusRegionalErrorCodes.IdentificacaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.identificacao_tamanho",
                "Tamanho da identificação da base legal de bônus regional inválido")),

        new(BaseLegalBonusRegionalErrorCodes.IdentificacaoCaractereNulo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.identificacao_caractere_nulo",
                "Identificação da base legal de bônus regional não pode conter o caractere nulo")),

        new(BaseLegalBonusRegionalErrorCodes.DescricaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.descricao_obrigatoria",
                "Descrição da base legal de bônus regional é obrigatória")),

        new(BaseLegalBonusRegionalErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.descricao_tamanho",
                "Tamanho da descrição da base legal de bônus regional inválido")),

        new(BaseLegalBonusRegionalErrorCodes.DescricaoCaractereNulo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.descricao_caractere_nulo",
                "Descrição da base legal de bônus regional não pode conter o caractere nulo")),

        new(BaseLegalBonusRegionalErrorCodes.SemMunicipios,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.sem_municipios",
                "A base legal de bônus regional deve abranger pelo menos um município")),

        new(BaseLegalBonusRegionalErrorCodes.MunicipioInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_bonus_regional.municipio_invalido",
                "Município informado é inválido")),

        new(BaseLegalBonusRegionalErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.base_legal_bonus_regional.nao_encontrado",
                "Base legal de bônus regional não encontrada")),
    ];
}
