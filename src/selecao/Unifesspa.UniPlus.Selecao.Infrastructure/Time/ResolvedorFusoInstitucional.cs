namespace Unifesspa.UniPlus.Selecao.Infrastructure.Time;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Resolve <see cref="FusoInstitucional.ZoneId"/> contra a base de fusos do runtime.
/// </summary>
/// <remarks>
/// O resultado é memoizado porque a zona é constante e a busca percorre a base de fusos do sistema;
/// a falha também é memoizada, já que uma base ausente não aparece no meio da execução.
/// </remarks>
internal sealed class ResolvedorFusoInstitucional : IResolvedorFusoInstitucional
{
    private readonly Lazy<Result<TimeZoneInfo>> _zona = new(Buscar);

    public Result<TimeZoneInfo> Resolver() => _zona.Value;

    private static Result<TimeZoneInfo> Buscar()
    {
        try
        {
            return Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));
        }
        catch (Exception excecao) when (excecao is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Deixar a exceção subir daria 500 genérico sem código de causa; o consumidor do erro
            // precisa distinguir "a instalação não resolve o fuso" de qualquer outra falha interna,
            // e o catálogo público publica essa distinção.
            return Result<TimeZoneInfo>.Failure(new DomainError(
                "ProcessoSeletivo.FusoInstitucionalNaoReconhecido",
                $"O fuso institucional '{FusoInstitucional.ZoneId}' não é reconhecido pela base de fusos deste ambiente."));
        }
    }
}
