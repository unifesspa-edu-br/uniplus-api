namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Avalia, sem gravar, as respostas que o dono do token tem na tela, mescladas ao que a inscrição
/// gravou. A consulta é prévia: nunca recusa por restrição, obrigatoriedade ou opção fora da oferta —
/// isso é do envio —, só pela forma do corpo, quando uma ocorrência de grupo não tem identidade
/// própria. Responde só ao titular, dentro do período da inscrição, como o rascunho.
/// </summary>
public static class AvaliarInscricaoQueryHandler
{
    public static async Task<Result<AvaliacaoDaInscricaoDto>> Handle(
        AvaliarInscricaoQuery query,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IConferenciaEnderecoGeo conferenciaEnderecoGeo,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);

        if (userContext.Cpf is not { } cpfDoToken || Cpf.Criar(cpfDoToken) is not { IsSuccess: true } cpf)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz um claim cpf válido — só um candidato autenticado com CPF pode consultar a inscrição."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterPorProcessoECpfAsync(query.ProcessoSeletivoId, cpf.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(erroDono);
        }

        // O mesmo instante confere o período e é o envio de que se contam os limites da faixa de data.
        DateTimeOffset instante = timeProvider.GetUtcNow();
        DomainError? foraDoPeriodo = await PeriodoDaInscricaoResolver.ConferirAsync(
            query.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso,
            instante, cancellationToken).ConfigureAwait(false);
        if (foraDoPeriodo is not null)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(foraDoPeriodo);
        }

        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos, List<FieldError> erros) = OcorrenciasRecebidas.Ler(
            query.Request?.Grupos?.Select(static g => KeyValuePair.Create(
                g.Key, (IReadOnlyList<OcorrenciaRecebida>?)g.Value?.Select(static o => new OcorrenciaRecebida(o?.Id, o?.Respostas)).ToList())),
            InscricaoErrorCodes.GrupoIdDuplicado);
        if (erros.Count > 0)
        {
            return Result<AvaliacaoDaInscricaoDto>.ValidationFailure(erros);
        }

        Result adotada = await VersaoDaInscricaoResolver
            .AdotarAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(adotada.Error!);
        }

        // A oferta do corpo é o seletor da tela, ainda sem salvar: nula, nenhuma está escolhida, como no salvar.
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, query.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs,
            query.Request?.OfertaCursoId, query.Request?.OfertaCursoSegundaOpcaoId, cancellationToken)
            .ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(contexto.Error!);
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(fuso.Error!);
        }

        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = await PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            inscricao,
            contexto.Value!,
            InscricaoRequestMapper.ParaRespostas(query.Request?.Respostas),
            grupos,
            new EnvioDasRespostas(instante, fuso.Value!),
            conferenciaEnderecoGeo,
            cancellationToken).ConfigureAwait(false);
        if (avaliacao.IsFailure)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(avaliacao.Error!);
        }

        return ProjecaoDaAvaliacaoDaInscricao.Projetar(avaliacao.Value!, contexto.Value!, query.ProcessoSeletivoId, enderecoNoAcervo, query.FaseId);
    }
}
