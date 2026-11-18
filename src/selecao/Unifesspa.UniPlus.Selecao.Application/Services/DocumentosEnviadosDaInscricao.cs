namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

/// <summary>
/// Os documentos que o candidato enviou, diante da avaliação da inscrição: o que entra no resolvedor
/// como apresentação e o que o envio da inscrição conserva ou remove (UNI-REQ-0028, UNI-REQ-0168).
/// </summary>
public sealed class DocumentosEnviadosDaInscricao(IDocumentoDaInscricaoRepository repositorio)
{
    public async Task<ArquivosDaInscricao> CarregarAsync(
        Guid inscricaoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);

        Dictionary<Guid, ApresentacaoDeDocumento> apresentacoes = (await repositorio
            .ListarApresentacoesDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false)).ToDictionary(static a => a.Id);
        ArquivoDaApresentacao[] arquivos = [.. (await repositorio
            .ListarArquivosDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false))
            .Where(a => apresentacoes.ContainsKey(a.ApresentacaoId))];
        return ArquivosDaInscricao.Criar(apresentacoes, arquivos);
    }

    /// <summary>
    /// O que o envio faz com os arquivos, sobre a avaliação da versão fixada: o arquivo verificado de
    /// exigência que se aplica passa a constar do envio e nunca mais é apagado; o de exigência que não se
    /// aplica — ou que a versão fixada não tem — é removido, se nunca constou de um envio, e as chaves
    /// dele ficam agendadas para a rotina de limpeza, na mesma transação (nunca se apaga o armazenamento
    /// antes do commit). No
    /// rascunho nada disso roda: desmarcar e remarcar uma condição não perde o arquivo.
    /// </summary>
    public async Task ConciliarNoEnvioAsync(ArquivosDaInscricao arquivos,
        ResultadoDaAvaliacaoDaInscricao avaliacao,
        IReadOnlyList<ArquivoConfirmado> noComprovante,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(arquivos);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(noComprovante);

        HashSet<Guid> constamDoComprovante = [.. noComprovante.Select(static a => a.ArquivoId)];
        HashSet<(Guid Exigencia, string? Ocorrencia)> queSeAplicam = [.. avaliacao.Documentos.Itens
            .Where(static d => d.Situacao != SituacaoDoDocumento.NaoExigido)
            .Select(static d => (d.Documento.Id, d.OcorrenciaId))];
        foreach (ArquivoDaApresentacao listado in arquivos.Arquivos)
        {
            await ConciliarArquivoAsync(arquivos.ApresentacaoDe(listado), listado.Id, constamDoComprovante, queSeAplicam, agora, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConciliarArquivoAsync(
        ApresentacaoDeDocumento apresentacao, Guid arquivoId, HashSet<Guid> constamDoComprovante,
        HashSet<(Guid Exigencia, string? Ocorrencia)> queSeAplicam, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (await repositorio.ObterArquivoAsync(arquivoId, cancellationToken).ConfigureAwait(false) is not { } arquivo)
        {
            return;
        }
        bool aplicavel = queSeAplicam.Contains((apresentacao.ExigenciaId, apresentacao.OcorrenciaId));
        await ConservarOuRemoverAsync(arquivo, aplicavel, constamDoComprovante.Contains(arquivo.Id), agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task ConservarOuRemoverAsync(
        ArquivoDaApresentacao arquivo, bool aplicavel, bool constaDoComprovante, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (aplicavel)
        {
            if (constaDoComprovante)
            {
                arquivo.MarcarComoConstanteDeEnvio();
            }
        }
        else if (!arquivo.ConstouDeEnvio)
        {
            await repositorio.RemoverArquivoAsync(arquivo, cancellationToken).ConfigureAwait(false);
            await AgendaDeLimpezaDoArquivoDoCandidato.AgendarAsync(repositorio, arquivo, agora, cancellationToken).ConfigureAwait(false);
        }
    }
}
