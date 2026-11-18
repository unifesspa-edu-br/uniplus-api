namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Projeta o preenchimento gravado com a avaliação e os documentos da versão fixada.</summary>
public sealed class LeituraDaInscricao(
    VersaoDaInscricaoResolver versao,
    ContextoDaVersaoFixadaResolver contexto,
    DocumentosEnviadosDaInscricao documentos,
    PreparacaoDaAvaliacaoDaInscricao preparacao,
    EnderecosDosDocumentosDoAto enderecos,
    IResolvedorFusoInstitucional resolvedorFuso,
    TimeProvider timeProvider)
{
    public async Task<Result<InscricaoDto>> LerAsync(Inscricao inscricao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        DateTimeOffset instante = timeProvider.GetUtcNow();
        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<InscricaoDto>.Failure(fuso.Error!);
        }
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        return adotada.IsFailure
            ? Result<InscricaoDto>.Failure(adotada.Error!)
            : await LerNaVersaoAsync(inscricao, new EnvioDasRespostas(instante, fuso.Value!), cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<InscricaoDto>> LerNaVersaoAsync(Inscricao inscricao, EnvioDasRespostas envio, CancellationToken cancellationToken)
    {
        Result<ContextoDaVersaoFixada> fixado = await contexto.ResolverAsync(
            inscricao.Formulario.VersaoFormularioId, inscricao.ProcessoSeletivoId,
            inscricao.Formulario.Preenchimento.Opcoes.Primeira, inscricao.Formulario.Preenchimento.Opcoes.Segunda, cancellationToken).ConfigureAwait(false);
        return fixado.IsFailure
            ? Result<InscricaoDto>.Failure(fixado.Error!)
            : await AvaliarEProjetarAsync(inscricao, fixado.Value!, envio, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<InscricaoDto>> AvaliarEProjetarAsync(
        Inscricao inscricao, ContextoDaVersaoFixada fixado, EnvioDasRespostas envio, CancellationToken cancellationToken)
    {
        ArquivosDaInscricao arquivos = await documentos.CarregarAsync(inscricao.Id, cancellationToken).ConfigureAwait(false);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = await preparacao.AvaliarAsync(inscricao, fixado,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal), envio, arquivos, cancellationToken).ConfigureAwait(false);
        return avaliacao.IsFailure
            ? Result<InscricaoDto>.Failure(avaliacao.Error!)
            : await ProjetarAsync(inscricao, fixado, avaliacao.Value!, arquivos, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<InscricaoDto>> ProjetarAsync(
        Inscricao inscricao, ContextoDaVersaoFixada fixado, ResultadoDaAvaliacaoDaInscricao avaliacao,
        ArquivosDaInscricao arquivos, CancellationToken cancellationToken)
    {
        DocumentosDoAtoNoAcervo acervo = await enderecos.DoAtoAsync(inscricao.ProcessoSeletivoId, fixado.AtoCriadorId, cancellationToken).ConfigureAwait(false);
        Result<AvaliacaoDaInscricaoDto> projetada = ProjecaoDaAvaliacaoDaInscricao.Projetar(avaliacao, fixado, acervo, arquivos.ParaOCandidato());
        return projetada.IsFailure
            ? Result<InscricaoDto>.Failure(projetada.Error!)
            : Result<InscricaoDto>.Success(InscricaoDto.De(inscricao, projetada.Value!, fixado.Admitidas.NumeroDeOpcoesDeCurso, fixado.OfertasDeCurso));
    }
}
