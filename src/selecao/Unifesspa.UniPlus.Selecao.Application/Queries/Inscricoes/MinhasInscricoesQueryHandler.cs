namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

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
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        TimeProvider timeProvider,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<IReadOnlyList<MinhaInscricaoDto>>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode listar as próprias inscrições."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<IReadOnlyList<MinhaInscricaoDto>>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        // Sem sub não há dono a conferir: a recusa é a mesma das demais rotas da inscrição, e não a
        // lista vazia, que diria ao candidato que ele não tem inscrição.
        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            return Result<IReadOnlyList<MinhaInscricaoDto>>.Failure(new DomainError(
                InscricaoErrorCodes.SubObrigatorio, "O token precisa trazer o claim sub para listar as inscrições."));
        }

        IReadOnlyList<Inscricao> inscricoes = await inscricaoRepository
            .ObterTodasPorCpfAsync(cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);

        // O mesmo CPF cifrado pode pertencer a um sub diferente do gravado (engano de emissor,
        // ver Inscricao.ConferirDono) — uma inscrição assim não é do dono do token, e listá-la
        // vazaria estado de outro candidato.
        string sub = userContext.UserId;
        List<Inscricao> doDono = [.. inscricoes.Where(i => i.ConferirDono(sub) is null)];

        ILookup<Guid, ComprovanteInscricao> comprovantes = (await inscricaoRepository
            .ObterComprovantesAsync([.. doDono.Select(static i => i.Id)], cancellationToken)
            .ConfigureAwait(false))
            .ToLookup(static c => c.InscricaoId);

        // As ações dependem do período, que é do processo: uma conferência por processo, no mesmo instante.
        // Processo sem versão divulgada legível (ou sem janela) conta como fechado: nada a oferecer.
        DateTimeOffset agora = timeProvider.GetUtcNow();
        Dictionary<Guid, bool> abertas = [];
        Dictionary<Guid, string?> nomes = [];
        foreach (Guid processo in doDono.Select(static i => i.ProcessoSeletivoId).Distinct())
        {
            CertameDivulgado? divulgado = await certameDivulgadoRepository
                .ObterParaLeituraAsync(processo, cancellationToken)
                .ConfigureAwait(false);
            nomes[processo] = divulgado?.Nome;
            abertas[processo] = await Services.PeriodoDaInscricaoResolver.ConferirAsync(
                processo, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso, agora, cancellationToken)
                .ConfigureAwait(false) is null;
        }

        Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>> ofertasPorVersao = await OfertasDasVersoesAsync(
            doDono, processoSeletivoRepository, registroCodecs, cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<MinhaInscricaoDto>>.Success(
            [.. doDono.Select(i => MinhaInscricaoDto.De(
                i, nomes[i.ProcessoSeletivoId], abertas[i.ProcessoSeletivoId], [.. comprovantes[i.Id]],
                ofertasPorVersao.GetValueOrDefault(i.VersaoFormularioId, [])))]);
    }

    /// <summary>
    /// As ofertas de curso de cada versão em que alguma inscrição está — a dela, nunca a vigente. Versão
    /// que não se lê não derruba a lista: a inscrição segue sem os cursos, como o processo sem divulgação
    /// segue sem nome.
    /// </summary>
    private static async Task<Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>>> OfertasDasVersoesAsync(
        List<Inscricao> inscricoes,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>> ofertas = [];
        foreach (IGrouping<Guid, Inscricao> doProcesso in inscricoes.GroupBy(static i => i.ProcessoSeletivoId))
        {
            IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
                .ObterVersoesAsync(doProcesso.Key, cancellationToken)
                .ConfigureAwait(false);
            foreach (Guid versaoId in doProcesso.Select(static i => i.VersaoFormularioId).Distinct())
            {
                VersaoConfiguracao? versao = versoes.FirstOrDefault(v => v.Id == versaoId);
                if (versao is null)
                {
                    continue;
                }

                Result<EnvelopeReidratado> reidratado = Services.ContextoDaVersaoFixadaResolver.Reidratar(versao, registroCodecs);
                if (reidratado.IsSuccess)
                {
                    ofertas[versaoId] = Services.ContextoDaVersaoFixadaResolver.OfertasDaVersao(reidratado.Value!.Grafo);
                }
            }
        }

        return ofertas;
    }
}
