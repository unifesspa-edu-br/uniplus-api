namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Referência de reserva demográfica (UNI-REQ-0065).</summary>
internal static class ReferenciaReservaDemograficaErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(ReferenciaReservaDemograficaErrorCodes.CensoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.censo_obrigatorio",
                "Censo de referência é obrigatório")),

        new(ReferenciaReservaDemograficaErrorCodes.CensoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.censo_tamanho",
                "Tamanho do Censo de referência inválido")),

        new(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.referencia_reserva_demografica.censo_ja_existe",
                "Já existe uma referência ativa para este Censo")),

        new(ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.percentual_fora_de_faixa",
                "Percentual fora do intervalo válido (0 a 100)")),

        new(ReferenciaReservaDemograficaErrorCodes.BaseLegalObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.base_legal_obrigatoria",
                "Base legal é obrigatória")),

        new(ReferenciaReservaDemograficaErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.base_legal_tamanho",
                "Tamanho da base legal inválido")),

        new(ReferenciaReservaDemograficaErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.referencia_reserva_demografica.nao_encontrada",
                "Referência de reserva demográfica não encontrada")),
    ];
}
