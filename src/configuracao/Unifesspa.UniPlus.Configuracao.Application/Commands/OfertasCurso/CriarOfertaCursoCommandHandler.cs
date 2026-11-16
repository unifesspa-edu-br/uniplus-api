namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarOfertaCursoCommand"/> (convention-based Wolverine): valida a declaração sem I/O (422 — validação
/// sempre vence I/O), confere o curso, o local e a unidade e congela a unidade por cópia (ADR-0061), e só então confere o
/// código contra as ofertas vivas (409). A corrida entre dois cadastros com o mesmo código cai no índice único parcial e
/// recebe o mesmo 409.
/// </summary>
public static class CriarOfertaCursoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarOfertaCursoCommand command, ConferenciaDasReferenciasDaOferta referencias, IOfertaCursoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(referencias);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<OfertaCurso> oferta = await MontarAsync(command, referencias, cancellationToken).ConfigureAwait(false);

        return oferta.IsFailure
            ? Result<Guid>.ValidationFailure(oferta.Errors)
            : await GravarAsync(oferta.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<OfertaCurso>> MontarAsync(
        CriarOfertaCursoCommand command, ConferenciaDasReferenciasDaOferta referencias, CancellationToken cancellationToken)
    {
        DeclaracaoDaOfertaDeCurso declaracao = command.Declaracao();
        Result validada = OfertaCurso.ValidarDeclaracao(declaracao);
        if (validada.IsFailure)
        {
            return Result<OfertaCurso>.ValidationFailure(validada.Errors);
        }

        Result<UnidadeOfertante> unidade = await referencias
            .ConferirAsync(command.CursoId, command.LocalOfertaId, command.UnidadeOfertanteOrigemId, cancellationToken)
            .ConfigureAwait(false);

        return unidade.IsFailure
            ? Result<OfertaCurso>.ValidationFailure(unidade.Errors)
            : OfertaCurso.Criar(command.CursoId, command.LocalOfertaId, unidade.Value!, declaracao);
    }

    private static async Task<Result<Guid>> GravarAsync(
        OfertaCurso oferta, IOfertaCursoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteEntreVivosAsync(oferta.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoDaOfertaEmUso.Recusa(oferta.Codigo));
        }

        await repository.AdicionarAsync(oferta, cancellationToken).ConfigureAwait(false);

        return await SalvarAsync(oferta, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> SalvarAsync(OfertaCurso oferta, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(oferta.Id);
        }
        catch (Exception exception) when (CodigoDaOfertaEmUso.ViolaOIndice(exception))
        {
            // Sem descartar, a inserção continua rastreada e o SaveChanges do outbox a repete fora deste catch (500 em vez de 409).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoDaOfertaEmUso.Recusa(oferta.Codigo));
        }
    }
}
