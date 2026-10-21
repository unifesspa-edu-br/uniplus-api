namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

/// <summary>
/// Os documentos que o candidato enviou, diante da avaliação da inscrição: o que entra no resolvedor
/// como apresentação e o que o envio da inscrição conserva ou remove (UNI-REQ-0028, UNI-REQ-0168).
/// </summary>
public static class DocumentosEnviadosDaInscricao
{
    /// <summary>
    /// As apresentações confirmadas, por exigência — uma apresentação conta uma vez, qualquer que seja o
    /// número de arquivos dela (a frente e o verso). A ocorrência do grupo repetível é a entidade.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>> ApresentacoesAsync(
        IDocumentoDaInscricaoRepository repositorio, Guid inscricaoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);

        IReadOnlyList<ApresentacaoDeDocumento> confirmadas = await repositorio
            .ListarApresentacoesConfirmadasAsync(inscricaoId, cancellationToken).ConfigureAwait(false);
        return confirmadas
            .GroupBy(static a => a.ExigenciaId)
            .ToDictionary(
                static g => g.Key,
                static g => (IReadOnlyList<ApresentacaoDocumento>)[.. g.Select(static a => new ApresentacaoDocumento(a.Id, a.OcorrenciaId, a.OcorrenciaId))]);
    }

    /// <summary>
    /// O que o envio faz com os arquivos, sobre a avaliação da versão fixada: o arquivo confirmado de
    /// exigência que se aplica passa a constar do envio e nunca mais é apagado; o de exigência que não se
    /// aplica — ou que a versão fixada não tem — é removido, se nunca constou de um envio. Devolve os
    /// arquivos removidos, de que quem grava apaga os objetos do armazenamento depois de salvar. No
    /// rascunho nada disso roda: desmarcar e remarcar uma condição não perde o arquivo.
    /// </summary>
    public static async Task<IReadOnlyList<ArquivoDaApresentacao>> ConciliarNoEnvioAsync(
        IDocumentoDaInscricaoRepository repositorio,
        Guid inscricaoId,
        ResultadoDaAvaliacaoDaInscricao avaliacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(avaliacao);

        HashSet<(Guid Exigencia, string? Ocorrencia)> queSeAplicam = [.. avaliacao.Documentos.Itens
            .Where(static d => d.Situacao != SituacaoDoDocumento.NaoExigido)
            .Select(static d => (d.Documento.Id, d.OcorrenciaId))];
        Dictionary<Guid, ApresentacaoDeDocumento> apresentacoes = (await repositorio
            .ListarApresentacoesDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(static a => a.Id);

        List<ArquivoDaApresentacao> removidos = [];
        foreach (ArquivoDaApresentacao listado in await repositorio.ListarArquivosDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false))
        {
            ApresentacaoDeDocumento apresentacao = apresentacoes[listado.ApresentacaoId];
            ArquivoDaApresentacao arquivo = (await repositorio.ObterArquivoAsync(listado.Id, cancellationToken).ConfigureAwait(false))!;
            if (queSeAplicam.Contains((apresentacao.ExigenciaId, apresentacao.OcorrenciaId)))
            {
                arquivo.MarcarComoConstanteDeEnvio();
            }
            else if (!arquivo.ConstouDeEnvio)
            {
                await repositorio.RemoverArquivoAsync(arquivo, cancellationToken).ConfigureAwait(false);
                removidos.Add(arquivo);
            }
        }

        return removidos;
    }
}
