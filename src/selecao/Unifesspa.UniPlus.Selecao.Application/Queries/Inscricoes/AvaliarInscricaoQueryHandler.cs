namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>Avalia o preenchimento da tela depois de conferir o titular da inscrição ativa.</summary>
public static class AvaliarInscricaoQueryHandler
{
    public static async Task<Result<AvaliacaoDaInscricaoDto>> Handle(
        AvaliarInscricaoQuery query, IInscricaoRepository inscricoes, IUserContext usuario,
        ConsultaDaAvaliacaoDaInscricao consulta, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricoes);
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Result<Inscricao> acesso = await ObterInscricaoAsync(query.ProcessoSeletivoId, inscricoes, usuario, cancellationToken).ConfigureAwait(false);
        return acesso.IsFailure
            ? Result<AvaliacaoDaInscricaoDto>.Failure(acesso.Error!)
            : await consulta.AvaliarAsync(acesso.Value!, query, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Inscricao>> ObterInscricaoAsync(
        Guid processoId, IInscricaoRepository inscricoes, IUserContext usuario, CancellationToken cancellationToken)
    {
        if (usuario.Cpf is not { } cpfDoToken || Cpf.Criar(cpfDoToken) is not { IsSuccess: true } cpf)
        {
            return Result<Inscricao>.Failure(new DomainError("Inscricao.CpfAusenteNoToken",
                "O token não traz um claim cpf válido — só um candidato autenticado com CPF pode consultar a inscrição."));
        }
        Inscricao? inscricao = await inscricoes.ObterAtivaPorProcessoECpfAsync(processoId, cpf.Value!, cancellationToken).ConfigureAwait(false);
        return InscricaoDoDono.DoDono(inscricao, usuario);
    }
}
