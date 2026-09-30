namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação de um fato recém-cadastrado, declarado ou derivado: o código é único entre todos os
/// fatos, desativados inclusive, e nunca é reutilizado.
/// </summary>
internal static class NovoFato
{
    private const string IndiceUnicoDoCodigo = "ux_rol_de_fatos_candidato_codigo";

    public static async Task<Result<Guid>> GravarAsync(
        FatoCandidato fato,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteAsync(fato.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        await repository.AdicionarAsync(fato, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflito(exception, IndiceUnicoDoCodigo))
        {
            // Sem descartar, a inserção continua rastreada e o SaveChangesAsync do outbox a repete
            // fora deste catch, e o 409 vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        return Result<Guid>.Success(fato.Id);
    }

    private static DomainError CodigoJaExiste() => new(
        FatoCandidatoErrorCodes.CodigoJaExiste,
        "Já existe um fato com o código informado; o código de um fato nunca é reutilizado.");
}
