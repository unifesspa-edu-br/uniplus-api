namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O fluxo comum das edições de um fato existente: carregar, aplicar a mutação do agregado e
/// gravar, traduzindo a corrida de concorrência otimista (xmin) em conflito.
/// </summary>
internal static class MutacaoDoFato
{
    private const string IndiceUnicoDoValor = "ux_fato_valor_dominio_fato_codigo";

    public static async Task<Result> AplicarAsync(
        Guid id,
        Func<FatoCandidato, Result> mutacao,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        FatoCandidato? fato = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (fato is null)
        {
            return Result.Failure(new DomainError(FatoCandidatoErrorCodes.NaoEncontrado, "Fato do candidato não encontrado."));
        }

        Result resultado = mutacao(fato);
        if (resultado.IsFailure)
        {
            return resultado;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.EhConflito(ex, IndiceUnicoDoValor))
        {
            // Dois acréscimos simultâneos do mesmo código de valor: a linha nova do valor não toca
            // o fato, então o token de concorrência não os separa, e quem chega por último bate no
            // índice único.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.CodigoDuplicado,
                "Já existe um valor de domínio com o código informado neste fato."));
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Os endpoints têm Idempotency-Key (ADR-0119): captura local e descarte, para o
            // SaveChangesAsync do outbox não reencontrar a entidade modificada.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                FatoCandidatoErrorCodes.ConflitoDeConcorrencia,
                "Este fato foi alterado concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }
}
