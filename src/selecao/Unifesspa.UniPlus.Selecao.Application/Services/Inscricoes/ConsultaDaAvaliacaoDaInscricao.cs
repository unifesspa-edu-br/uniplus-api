namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Queries.Inscricoes;
using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Avalia a tela do candidato na versão fixada, sem gravar o preenchimento.</summary>
public sealed class ConsultaDaAvaliacaoDaInscricao(
    PeriodoDaInscricaoResolver periodo, VersaoDaInscricaoResolver versao,
    ContextoDaVersaoFixadaResolver contextos, DocumentosEnviadosDaInscricao documentos,
    PreparacaoDaAvaliacaoDaInscricao preparacao, IResolvedorFusoInstitucional resolvedorFuso,
    EnderecosDosDocumentosDoAto enderecos)
{
    public async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarAsync(
        Inscricao inscricao, AvaliarInscricaoQuery query, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(query);
        DomainError? foraDoPeriodo = (await periodo.ConferirAsync(query.ProcessoSeletivoId, instante, cancellationToken).ConfigureAwait(false))?.Recusa;
        if (foraDoPeriodo is not null)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(foraDoPeriodo);
        }
        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos, List<FieldError> erros) = LerGrupos(query);
        return erros.Count > 0
            ? Result<AvaliacaoDaInscricaoDto>.ValidationFailure(erros)
            : await AdotarEAvaliarAsync(inscricao, query, grupos, instante, cancellationToken).ConfigureAwait(false);
    }

    private static (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>, List<FieldError>) LerGrupos(AvaliarInscricaoQuery query) =>
        OcorrenciasRecebidas.Ler(query.Request?.Grupos?.Select(static g => KeyValuePair.Create(
            g.Key, (IReadOnlyList<OcorrenciaRecebida>?)g.Value?.Select(static o => new OcorrenciaRecebida(o?.Id, o?.Respostas)).ToList())),
            InscricaoErrorCodes.GrupoIdDuplicado);

    private async Task<Result<AvaliacaoDaInscricaoDto>> AdotarEAvaliarAsync(
        Inscricao inscricao, AvaliarInscricaoQuery query, Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos,
        DateTimeOffset instante, CancellationToken cancellationToken)
    {
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(adotada.Error!);
        }
        Result<ContextoDaVersaoFixada> contexto = await contextos.ResolverAsync(inscricao.Formulario.VersaoFormularioId,
            query.ProcessoSeletivoId, query.Request?.OfertaCursoId, query.Request?.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);
        return contexto.IsFailure
            ? Result<AvaliacaoDaInscricaoDto>.Failure(contexto.Error!)
            : await AvaliarNaVersaoAsync(inscricao, query, grupos, instante, contexto.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarNaVersaoAsync(
        Inscricao inscricao, AvaliarInscricaoQuery query, Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos,
        DateTimeOffset instante, ContextoDaVersaoFixada contexto, CancellationToken cancellationToken)
    {
        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(fuso.Error!);
        }
        ArquivosDaInscricao arquivos = await documentos.CarregarAsync(inscricao.Id, cancellationToken).ConfigureAwait(false);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = await preparacao.AvaliarAsync(inscricao, contexto,
            InscricaoRequestMapper.ParaRespostas(query.Request?.Respostas), grupos, new EnvioDasRespostas(instante, fuso.Value!), arquivos, cancellationToken).ConfigureAwait(false);
        return avaliacao.IsFailure
            ? Result<AvaliacaoDaInscricaoDto>.Failure(avaliacao.Error!)
            : await ProjetarAsync(query, contexto, avaliacao.Value!, arquivos, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AvaliacaoDaInscricaoDto>> ProjetarAsync(
        AvaliarInscricaoQuery query, ContextoDaVersaoFixada contexto, ResultadoDaAvaliacaoDaInscricao avaliacao,
        ArquivosDaInscricao arquivos, CancellationToken cancellationToken)
    {
        DocumentosDoAtoNoAcervo acervo = await enderecos.DoAtoAsync(query.ProcessoSeletivoId, contexto.AtoCriadorId, cancellationToken).ConfigureAwait(false);
        return ProjecaoDaAvaliacaoDaInscricao.Projetar(avaliacao, contexto, acervo, arquivos.ParaOCandidato(), query.FaseId);
    }
}
