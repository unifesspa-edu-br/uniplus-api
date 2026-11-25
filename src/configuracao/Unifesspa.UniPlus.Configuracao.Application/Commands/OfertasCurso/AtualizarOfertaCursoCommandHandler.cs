namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarOfertaCursoCommand"/>. Só toca os atributos editáveis — curso, local e unidade ofertante são
/// imutáveis (o agregado nem expõe a mutação). Valida a declaração (422, sem I/O) ANTES de buscar a oferta por Id — validação
/// sempre vence 404 —, confere o código contra as outras ofertas vivas (409) e protege a corrida traduzindo a violação do
/// índice único parcial no mesmo 409.
/// </summary>
public static class AtualizarOfertaCursoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarOfertaCursoCommand command, IOfertaCursoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<OfertaCurso> carregada = await CarregarAsync(command, repository, cancellationToken).ConfigureAwait(false);

        return carregada.IsFailure
            ? Result.ValidationFailure(carregada.Errors)
            : await AtualizarAsync(carregada.Value!, command.Declaracao(), repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<OfertaCurso>> CarregarAsync(
        AtualizarOfertaCursoCommand command, IOfertaCursoRepository repository, CancellationToken cancellationToken)
    {
        Result validada = OfertaCurso.ValidarDeclaracao(command.Declaracao());
        if (validada.IsFailure)
        {
            return Result<OfertaCurso>.ValidationFailure(validada.Errors);
        }

        OfertaCurso? oferta = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        return oferta is null
            ? Result<OfertaCurso>.Failure(new DomainError(OfertaCursoErrorCodes.NaoEncontrada, "Oferta de curso não encontrada."))
            : Result<OfertaCurso>.Success(oferta);
    }

    private static async Task<Result> AtualizarAsync(
        OfertaCurso oferta, DeclaracaoDaOfertaDeCurso declaracao, IOfertaCursoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteEntreVivosAsync(declaracao.Codigo!, oferta.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoDaOfertaEmUso.Recusa());
        }

        Result atualizada = oferta.Atualizar(declaracao);

        return atualizada.IsFailure
            ? atualizada
            : await SalvarAsync(declaracao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> SalvarAsync(DeclaracaoDaOfertaDeCurso declaracao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception exception) when (CodigoDaOfertaEmUso.ViolaOIndice(exception))
        {
            // Sem descartar, a atualização continua rastreada e o SaveChanges do outbox a repete fora deste catch (500 em vez de 409).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CodigoDaOfertaEmUso.Recusa());
        }
    }
}
