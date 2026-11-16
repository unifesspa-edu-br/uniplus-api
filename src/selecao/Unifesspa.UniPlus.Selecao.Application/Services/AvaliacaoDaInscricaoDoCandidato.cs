namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Queries.Inscricoes;
using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Avalia, sem gravar, as respostas que o candidato tem na tela mescladas ao que a inscrição gravou, na versão em que o rascunho
/// está. A avaliação é prévia: só recusa pela forma do corpo, quando uma ocorrência de grupo não tem identidade própria.
/// </summary>
public sealed class AvaliacaoDaInscricaoDoCandidato(
    VersaoDaInscricao versao,
    IResolvedorFusoInstitucional fuso,
    IDocumentoDaInscricaoRepository documentos,
    IConferenciaEnderecoGeo geo,
    EnderecosDosDocumentosDoAto enderecos)
{
    public async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarAsync(
        Inscricao inscricao, AvaliarInscricaoQuery query, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(query);

        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos, List<FieldError> erros) = GruposRecebidos(query);
        if (erros.Count > 0)
        {
            return Result<AvaliacaoDaInscricaoDto>.ValidationFailure(erros);
        }

        Result<ContextoDaVersaoFixada> contexto = await ContextoDoRascunhoAsync(inscricao, query, cancellationToken).ConfigureAwait(false);
        return contexto.IsFailure
            ? Result<AvaliacaoDaInscricaoDto>.Failure(contexto.Error!)
            : await AvaliarNoContextoAsync(inscricao, query, grupos, contexto.Value!, instante, cancellationToken).ConfigureAwait(false);
    }

    private static (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> Grupos, List<FieldError> Erros) GruposRecebidos(AvaliarInscricaoQuery query) =>
        OcorrenciasRecebidas.Ler(
            query.Request?.Grupos?.Select(static g => KeyValuePair.Create(
                g.Key, (IReadOnlyList<OcorrenciaRecebida>?)g.Value?.Select(static o => new OcorrenciaRecebida(o?.Id, o?.Respostas)).ToList())),
            InscricaoErrorCodes.GrupoIdDuplicado);

    private async Task<Result<ContextoDaVersaoFixada>> ContextoDoRascunhoAsync(
        Inscricao inscricao, AvaliarInscricaoQuery query, CancellationToken cancellationToken)
    {
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        return adotada.IsFailure
            ? Result<ContextoDaVersaoFixada>.Failure(adotada.Error!)
            : await versao.ContextoAsync(
                inscricao, query.Request?.OfertaCursoId, query.Request?.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarNoContextoAsync(
        Inscricao inscricao,
        AvaliarInscricaoQuery query,
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos,
        ContextoDaVersaoFixada contexto,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        Result<TimeZoneInfo> fusoInstitucional = fuso.Resolver();
        if (fusoInstitucional.IsFailure)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(fusoInstitucional.Error!);
        }

        ArquivosDaInscricao arquivos = await ArquivosDaInscricao.CarregarAsync(documentos, inscricao.Id, cancellationToken).ConfigureAwait(false);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = await PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            inscricao, contexto, InscricaoRequestMapper.ParaRespostas(query.Request?.Respostas), grupos,
            new EnvioDasRespostas(instante, fusoInstitucional.Value!), geo, arquivos, cancellationToken).ConfigureAwait(false);
        return avaliacao.IsFailure
            ? Result<AvaliacaoDaInscricaoDto>.Failure(avaliacao.Error!)
            : await ProjetarAsync(query, contexto, avaliacao.Value!, arquivos, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AvaliacaoDaInscricaoDto>> ProjetarAsync(
        AvaliarInscricaoQuery query,
        ContextoDaVersaoFixada contexto,
        ResultadoDaAvaliacaoDaInscricao avaliacao,
        ArquivosDaInscricao arquivos,
        CancellationToken cancellationToken)
    {
        DocumentosDoAtoNoAcervo acervo = await enderecos.DoAtoAsync(query.ProcessoSeletivoId, contexto.AtoCriadorId, cancellationToken).ConfigureAwait(false);
        return ProjecaoDaAvaliacaoDaInscricao.Projetar(avaliacao, contexto, acervo, arquivos.ParaOCandidato(), query.FaseId);
    }
}
