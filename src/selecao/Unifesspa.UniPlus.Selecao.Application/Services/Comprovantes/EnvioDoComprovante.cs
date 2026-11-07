namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>O envio que o comprovante registra, lido do comprovante e apresentado na zona institucional.</summary>
internal static class EnvioDoComprovante
{
    public static EnvioDoComprovanteDto De(InsumosDoComprovante insumos) => new(
        insumos.Comprovante.NumeroInscricao,
        insumos.Comprovante.Instante,
        FusoHorarioDto.Institucional,
        insumos.Comprovante.HashIntegridade);
}
