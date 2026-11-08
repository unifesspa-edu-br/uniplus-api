namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O cancelamento que o comprovante informa: só a inscrição cancelada o traz, com o instante, o motivo quando houver e o aviso
/// pronto para exibir, com a data e a hora, até o segundo, na zona institucional. A zona só é resolvida quando há cancelamento; se não se resolve, é
/// defeito da instalação e o comprovante não sai com a data errada.
/// </summary>
public sealed class CancelamentoDoComprovante
{
    private readonly IResolvedorFusoInstitucional _resolvedorFuso;

    public CancelamentoDoComprovante(IResolvedorFusoInstitucional resolvedorFuso)
    {
        ArgumentNullException.ThrowIfNull(resolvedorFuso);

        _resolvedorFuso = resolvedorFuso;
    }

    /// <summary>O cancelamento da inscrição, ou sucesso sem valor quando ela não foi cancelada.</summary>
    public Result<CancelamentoNoComprovanteDto?> Ler(Inscricao inscricao)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        if (inscricao.CanceladaEm is not { } canceladaEm)
        {
            return Result<CancelamentoNoComprovanteDto?>.Success(null);
        }

        Result<TimeZoneInfo> fuso = _resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<CancelamentoNoComprovanteDto?>.Failure(fuso.Error!);
        }

        string instante = TimeZoneInfo.ConvertTime(canceladaEm, fuso.Value!).ToString("dd/MM/yyyy 'às' HH:mm:ss", CultureInfo.InvariantCulture);

        return Result<CancelamentoNoComprovanteDto?>.Success(
            new CancelamentoNoComprovanteDto(canceladaEm, inscricao.MotivoDoCancelamento, $"Documento sem validade: inscrição cancelada em {instante}"));
    }
}
