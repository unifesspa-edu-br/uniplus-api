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
/// Lista as inscrições do dono do token — nunca de outro CPF, mesmo que o <c>contaId</c> coincida por
/// engano de emissor (<see cref="Inscricao.ConferirDono"/> confere o <c>contaId</c> gravado em cada
/// uma).
/// </summary>
public static class MinhasInscricoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<MinhaInscricaoDto>>> Handle(
        MinhasInscricoesQuery query,
        IInscricaoRepository inscricaoRepository,
        IComprovanteDaInscricaoRepository comprovanteRepository,
        ProcessosDasInscricoes processos,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(comprovanteRepository);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<IReadOnlyList<Inscricao>> doDono = await InscricoesDoDonoAsync(inscricaoRepository, userContext, cancellationToken).ConfigureAwait(false);

        return doDono.IsFailure
            ? Result<IReadOnlyList<MinhaInscricaoDto>>.Failure(doDono.Error!)
            : Result<IReadOnlyList<MinhaInscricaoDto>>.Success(
                await ListarAsync(doDono.Value!, comprovanteRepository, processos, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// As inscrições do CPF do token cujo <c>contaId</c> é o do token. O mesmo CPF cifrado pode pertencer a uma conta diferente do gravado
    /// (engano de emissor, ver <see cref="Inscricao.ConferirDono"/>): uma inscrição assim não é do dono, e listá-la vazaria estado
    /// de outro candidato. Sem conta não há dono a conferir, e a recusa é a das demais rotas, e não a lista vazia, que diria ao
    /// candidato que ele não tem inscrição.
    /// </summary>
    private static async Task<Result<IReadOnlyList<Inscricao>>> InscricoesDoDonoAsync(
        IInscricaoRepository inscricaoRepository, IUserContext userContext, CancellationToken cancellationToken)
    {
        Result<Cpf> cpf = InscricaoDoDono.CpfDoToken(userContext, "acessar as próprias inscrições");
        DomainError? recusa = cpf.IsFailure ? cpf.Error : ContaAusenteNoToken(userContext);
        if (recusa is not null)
        {
            return Result<IReadOnlyList<Inscricao>>.Failure(recusa);
        }

        IReadOnlyList<Inscricao> inscricoes = await inscricaoRepository.ObterTodasPorCpfAsync(cpf.Value!, cancellationToken).ConfigureAwait(false);
        return Result<IReadOnlyList<Inscricao>>.Success([.. inscricoes.Where(i => i.Titular.ConferirDono(userContext.UserId) is null)]);
    }

    private static DomainError? ContaAusenteNoToken(IUserContext userContext) =>
        string.IsNullOrWhiteSpace(userContext.UserId)
            ? new DomainError(InscricaoErrorCodes.ContaIdObrigatorio, "O token precisa trazer o claim sub para listar as inscrições.")
            : null;

    private static async Task<IReadOnlyList<MinhaInscricaoDto>> ListarAsync(
        IReadOnlyList<Inscricao> inscricoes, IComprovanteDaInscricaoRepository comprovanteRepository, ProcessosDasInscricoes processos, CancellationToken cancellationToken)
    {
        ILookup<Guid, DateTimeOffset> emissoes = await EmissoesPorInscricaoAsync(inscricoes, comprovanteRepository, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, SituacaoDoProcessoDaInscricao> situacoes = await processos.SituacoesAsync(inscricoes, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>> ofertas = await processos.OfertasDasVersoesAsync(inscricoes, cancellationToken).ConfigureAwait(false);

        return
        [
            .. inscricoes.Select(i => MinhaInscricaoDto.De(
                i,
                situacoes[i.ProcessoSeletivoId].Nome,
                situacoes[i.ProcessoSeletivoId].MotivoDoFechamento,
                [.. emissoes[i.Id]],
                ofertas.GetValueOrDefault(i.Formulario.VersaoFormularioId, []))),
        ];
    }

    private static async Task<ILookup<Guid, DateTimeOffset>> EmissoesPorInscricaoAsync(
        IReadOnlyList<Inscricao> inscricoes, IComprovanteDaInscricaoRepository comprovanteRepository, CancellationToken cancellationToken) =>
        (await comprovanteRepository.ObterEmissoesAsync([.. inscricoes.Select(static i => i.Id)], cancellationToken).ConfigureAwait(false))
            .ToLookup(static e => e.InscricaoId, static e => e.Instante);
}
