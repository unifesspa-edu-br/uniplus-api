namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;

/// <summary>Inicia a inscrição do titular ou devolve a existente, inclusive após colisão no índice único.</summary>
public static class IniciarInscricaoCommandHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        IniciarInscricaoCommand command, IInscricaoRepository inscricoes, IUserContext usuario,
        InicioDaInscricao inicio, LeituraDaInscricao leitura, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricoes);
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(inicio);
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<Cpf> cpf = InscricaoDoDono.CpfDoToken(usuario, "se inscrever");
        if (cpf.IsFailure)
        {
            return Result<InscricaoDto>.Failure(cpf.Error!);
        }
        Result<Inscricao> resultado = await IniciarOuLerAsync(command.ProcessoSeletivoId, cpf.Value!, inscricoes, usuario, inicio, unitOfWork, cancellationToken).ConfigureAwait(false);
        return await LerResultadoAsync(resultado, leitura, usuario, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Inscricao>> IniciarOuLerAsync(
        Guid processoId, Cpf cpf, IInscricaoRepository inscricoes, IUserContext usuario,
        InicioDaInscricao inicio, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Inscricao? existente = await inscricoes.ObterAtivaPorProcessoECpfAsync(processoId, cpf, cancellationToken).ConfigureAwait(false);
        if (existente is not null)
        {
            return Result<Inscricao>.Success(existente);
        }
        DomainError? donoDivergente = await ConferirCanceladasAsync(processoId, cpf, inscricoes, usuario, cancellationToken).ConfigureAwait(false);
        return donoDivergente is not null
            ? Result<Inscricao>.Failure(donoDivergente)
            : await IniciarEConfirmarAsync(processoId, cpf, inscricoes, inicio, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DomainError?> ConferirCanceladasAsync(
        Guid processoId, Cpf cpf, IInscricaoRepository inscricoes, IUserContext usuario, CancellationToken cancellationToken)
    {
        IReadOnlyList<Inscricao> doCpf = await inscricoes.ObterTodasPorCpfAsync(cpf, cancellationToken).ConfigureAwait(false);
        return doCpf.Where(i => i.ProcessoSeletivoId == processoId)
            .Select(i => i.Titular.ConferirDono(usuario.UserId ?? string.Empty)).FirstOrDefault(static erro => erro is not null);
    }

    private static async Task<Result<Inscricao>> IniciarEConfirmarAsync(
        Guid processoId, Cpf cpf, IInscricaoRepository inscricoes, InicioDaInscricao inicio,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result<Inscricao> criada = await inicio.IniciarAsync(processoId, cpf, cancellationToken).ConfigureAwait(false);
        if (criada.IsFailure)
        {
            return criada;
        }
        return await ConfirmarAsync(criada, processoId, cpf, inscricoes, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Inscricao>> ConfirmarAsync(
        Result<Inscricao> criada, Guid processoId, Cpf cpf, IInscricaoRepository inscricoes,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return criada;
        }
        catch (Exception ex) when (ConstraintVioladaNaGravacao.NomeDaConstraintViolada(ex) == InscricaoConstraintViolation.ProcessoCpfConstraint)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return await RelerVencedoraAsync(processoId, cpf, inscricoes, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Result<Inscricao>> RelerVencedoraAsync(
        Guid processoId, Cpf cpf, IInscricaoRepository inscricoes, CancellationToken cancellationToken)
    {
        Inscricao? vencedora = await inscricoes.ObterAtivaPorProcessoECpfAsync(processoId, cpf, cancellationToken).ConfigureAwait(false);
        return vencedora is null
            ? Result<Inscricao>.Failure(new DomainError("Inscricao.CorridaInconsistente", "A inscrição colidiu no índice único, mas não foi possível relê-la."))
            : Result<Inscricao>.Success(vencedora);
    }

    private static async Task<Result<InscricaoDto>> LerResultadoAsync(
        Result<Inscricao> resultado, LeituraDaInscricao leitura, IUserContext usuario, CancellationToken cancellationToken)
    {
        if (resultado.IsFailure)
        {
            return Result<InscricaoDto>.Failure(resultado.Error!);
        }
        return resultado.Value!.Titular.ConferirDono(usuario.UserId ?? string.Empty) is { } erroDono
            ? Result<InscricaoDto>.Failure(erroDono)
            : await leitura.LerAsync(resultado.Value!, cancellationToken).ConfigureAwait(false);
    }
}
