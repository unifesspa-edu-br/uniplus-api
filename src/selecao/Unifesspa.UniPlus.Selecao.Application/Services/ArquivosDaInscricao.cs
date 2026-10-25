namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

/// <summary>
/// Os arquivos que o candidato enviou, lidos do banco uma vez, e as visões que o fluxo da inscrição precisa deles: o
/// que atende as exigências, o que o comprovante registra, o que a tela mostra e o que ainda espera o resultado.
/// </summary>
public sealed class ArquivosDaInscricao
{
    private readonly IReadOnlyDictionary<Guid, ApresentacaoDeDocumento> _apresentacoes;

    private ArquivosDaInscricao(IReadOnlyDictionary<Guid, ApresentacaoDeDocumento> apresentacoes, IReadOnlyList<ArquivoDaApresentacao> arquivos)
    {
        _apresentacoes = apresentacoes;
        Arquivos = arquivos;
    }

    /// <summary>Os arquivos que têm apresentação, em qualquer situação, sem rastreamento.</summary>
    public IReadOnlyList<ArquivoDaApresentacao> Arquivos { get; }

    public static async Task<ArquivosDaInscricao> CarregarAsync(
        IDocumentoDaInscricaoRepository repositorio, Guid inscricaoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);

        Dictionary<Guid, ApresentacaoDeDocumento> apresentacoes = (await repositorio
            .ListarApresentacoesDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false)).ToDictionary(static a => a.Id);
        ArquivoDaApresentacao[] arquivos = [.. (await repositorio
            .ListarArquivosDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false))
            .Where(a => apresentacoes.ContainsKey(a.ApresentacaoId))];
        return new ArquivosDaInscricao(apresentacoes, arquivos);
    }

    public ApresentacaoDeDocumento ApresentacaoDe(ArquivoDaApresentacao arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        return _apresentacoes[arquivo.ApresentacaoId];
    }

    /// <summary>
    /// As apresentações com ao menos um arquivo verificado, por exigência; uma conta uma vez, qualquer que seja o
    /// número de arquivos (frente e verso). Em verificação, recusado ou a reenviar não atende (UNI-REQ-0027). A
    /// chave de distinção é a própria apresentação, que distingue as ocorrências da exigência que conta distintas.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentacoesVerificadas() =>
        Arquivos
            .Where(static a => a.Situacao == SituacaoDoArquivoDoCandidato.Verificado)
            .Select(a => ApresentacaoDe(a))
            .DistinctBy(static p => p.Id)
            .GroupBy(static p => p.ExigenciaId)
            .ToDictionary(
                static g => g.Key,
                static g => (IReadOnlyList<ApresentacaoDocumento>)[.. g.Select(static p => new ApresentacaoDocumento(p.Id, p.Id.ToString("D"), p.OcorrenciaId))]);

    /// <summary>Os arquivos verificados, do mais antigo ao mais recente.</summary>
    public IReadOnlyList<ArquivoConfirmado> Verificados() =>
    [
        .. Arquivos
            .Where(static a => a.Situacao == SituacaoDoArquivoDoCandidato.Verificado)
            .OrderBy(static a => a.ConfirmadoEm)
            .Select(a => new ArquivoConfirmado(
                a.Id, a.ApresentacaoId, ApresentacaoDe(a).ExigenciaId, ApresentacaoDe(a).OcorrenciaId,
                a.Formato, a.TamanhoBytes!.Value, a.HashSha256!, a.ConfirmadoEm!.Value)),
    ];

    /// <summary>
    /// Os arquivos confirmados em qualquer situação, como o candidato os enxerga, em ordem de confirmação. A posição de cada
    /// um (o "Arquivo N" do rótulo e do nome de download) conta dentro da mesma exigência e ocorrência.
    /// </summary>
    public IReadOnlyList<ArquivoDaApresentacaoDto> ParaOCandidato() =>
    [
        .. Arquivos
            .Where(static a => a.Situacao != SituacaoDoArquivoDoCandidato.Pendente)
            .GroupBy(a => (ApresentacaoDe(a).ExigenciaId, ApresentacaoDe(a).OcorrenciaId))
            .SelectMany(grupo => grupo
                .OrderBy(static a => a.ConfirmadoEm)
                .ThenBy(static a => a.Id)
                .Select((a, indice) => ArquivoDaApresentacaoDto.De(a, ApresentacaoDe(a), indice + 1)))
            .OrderBy(static d => d.ConfirmadoEm)
            .ThenBy(static d => d.ArquivoId),
    ];

    /// <summary>As exigências com arquivo em verificação e nenhum verificado: o envio espera o resultado (UNI-REQ-0028).</summary>
    public IReadOnlySet<(Guid ExigenciaId, string? OcorrenciaId)> EmVerificacao()
    {
        HashSet<Guid> comVerificado = [.. Arquivos
            .Where(static a => a.Situacao == SituacaoDoArquivoDoCandidato.Verificado)
            .Select(static a => a.ApresentacaoId)];
        return Arquivos
            .Where(a => a.Situacao == SituacaoDoArquivoDoCandidato.EmVerificacao && !comVerificado.Contains(a.ApresentacaoId))
            .Select(a => (ApresentacaoDe(a).ExigenciaId, ApresentacaoDe(a).OcorrenciaId))
            .ToHashSet();
    }
}
