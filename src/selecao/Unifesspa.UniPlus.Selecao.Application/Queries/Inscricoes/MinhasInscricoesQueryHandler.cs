namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Lista as inscrições do dono do token — nunca de outro CPF, mesmo que o <c>sub</c> coincida por
/// engano de emissor (<see cref="Inscricao.ConferirDono"/> confere o <c>sub</c> gravado em cada
/// uma).
/// </summary>
public static class MinhasInscricoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<MinhaInscricaoDto>>> Handle(
        MinhasInscricoesQuery query,
        IInscricaoRepository inscricaoRepository,
        ProcessosDasInscricoes processos,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<IReadOnlyList<Inscricao>> doDono = await InscricoesDoDonoAsync(inscricaoRepository, userContext, cancellationToken).ConfigureAwait(false);

        return doDono.IsFailure
            ? Result<IReadOnlyList<MinhaInscricaoDto>>.Failure(doDono.Error!)
            : Result<IReadOnlyList<MinhaInscricaoDto>>.Success(
                await ListarAsync(doDono.Value!, inscricaoRepository, processos, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// As inscrições do CPF do token cujo <c>sub</c> é o do token. O mesmo CPF cifrado pode pertencer a um sub diferente do gravado
    /// (engano de emissor, ver <see cref="Inscricao.ConferirDono"/>): uma inscrição assim não é do dono, e listá-la vazaria estado
    /// de outro candidato. Sem sub não há dono a conferir, e a recusa é a das demais rotas, e não a lista vazia, que diria ao
    /// candidato que ele não tem inscrição.
    /// </summary>
    private static async Task<Result<IReadOnlyList<Inscricao>>> InscricoesDoDonoAsync(
        IInscricaoRepository inscricaoRepository, IUserContext userContext, CancellationToken cancellationToken)
    {
        Result<Cpf> cpf = InscricaoDoDono.CpfDoToken(userContext);
        if (cpf.IsFailure)
        {
            return Result<IReadOnlyList<Inscricao>>.Failure(cpf.Error!);
        }

        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            return Result<IReadOnlyList<Inscricao>>.Failure(new DomainError(
                InscricaoErrorCodes.SubObrigatorio, "O token precisa trazer o claim sub para listar as inscrições."));
        }

        IReadOnlyList<Inscricao> inscricoes = await inscricaoRepository.ObterTodasPorCpfAsync(cpf.Value!, cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<Inscricao>>.Success([.. inscricoes.Where(i => i.ConferirDono(userContext.UserId) is null)]);
    }

    private static async Task<IReadOnlyList<MinhaInscricaoDto>> ListarAsync(
        IReadOnlyList<Inscricao> inscricoes, IInscricaoRepository inscricaoRepository, ProcessosDasInscricoes processos, CancellationToken cancellationToken)
    {
        ILookup<Guid, DateTimeOffset> emissoes = (await inscricaoRepository
            .ObterEmissoesAsync([.. inscricoes.Select(static i => i.Id)], cancellationToken).ConfigureAwait(false))
            .ToLookup(static e => e.InscricaoId, static e => e.Instante);
        Dictionary<Guid, SituacaoDoProcessoDaInscricao> situacoes = await processos.SituacoesAsync(inscricoes, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>> ofertas = await processos.OfertasDasVersoesAsync(inscricoes, cancellationToken).ConfigureAwait(false);

        return
        [
            .. inscricoes.Select(i => MinhaInscricaoDto.De(
                i,
                situacoes[i.ProcessoSeletivoId].Nome,
                situacoes[i.ProcessoSeletivoId].InscricoesAbertas,
                [.. emissoes[i.Id]],
                ofertas.GetValueOrDefault(i.VersaoFormularioId, []))),
        ];
    }
}
