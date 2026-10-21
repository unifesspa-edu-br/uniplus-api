namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

/// <summary>
/// Os documentos que o candidato enviou, diante da avaliação da inscrição: o que entra no resolvedor
/// como apresentação e o que o envio da inscrição conserva ou remove (UNI-REQ-0028, UNI-REQ-0168).
/// </summary>
public static class DocumentosEnviadosDaInscricao
{
    /// <summary>
    /// As apresentações confirmadas, por exigência — uma apresentação conta uma vez, qualquer que seja o
    /// número de arquivos dela (a frente e o verso). A ocorrência do grupo repetível é a entidade; a chave de distinção é a própria apresentação, que
    /// distingue as ocorrências da exigência que só conta apresentações distintas.
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
                static g => (IReadOnlyList<ApresentacaoDocumento>)[.. g.Select(static a => new ApresentacaoDocumento(a.Id, a.Id.ToString("D"), a.OcorrenciaId))]);
    }

    /// <summary>Os arquivos confirmados da inscrição, por exigência e ocorrência, do mais antigo ao mais recente.</summary>
    public static async Task<IReadOnlyList<ArquivoConfirmado>> ConfirmadosAsync(
        IDocumentoDaInscricaoRepository repositorio, Guid inscricaoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);

        Dictionary<Guid, ApresentacaoDeDocumento> apresentacoes = (await repositorio
            .ListarApresentacoesDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false)).ToDictionary(static a => a.Id);
        return [.. (await repositorio.ListarArquivosDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false))
            .Where(a => a.Status == StatusArquivoEnviado.Confirmado && apresentacoes.ContainsKey(a.ApresentacaoId))
            .OrderBy(static a => a.ConfirmadoEm)
            .Select(a => new ArquivoConfirmado(
                a.Id, a.ApresentacaoId, apresentacoes[a.ApresentacaoId].ExigenciaId, apresentacoes[a.ApresentacaoId].OcorrenciaId,
                a.Formato, a.TamanhoBytes!.Value, a.HashSha256!, a.ConfirmadoEm!.Value))];
    }

    /// <summary>Os arquivos confirmados da inscrição, por exigência e ocorrência, como o candidato os enxerga.</summary>
    public static async Task<IReadOnlyList<ArquivoDaApresentacaoDto>> ArquivosAsync(
        IDocumentoDaInscricaoRepository repositorio, Guid inscricaoId, CancellationToken cancellationToken) =>
        [.. (await ConfirmadosAsync(repositorio, inscricaoId, cancellationToken).ConfigureAwait(false))
            .Select(static a => new ArquivoDaApresentacaoDto(
                a.ArquivoId, a.ApresentacaoId, a.ExigenciaId, a.OcorrenciaId,
                ArquivoDaApresentacao.TipoDeMidiaDe(a.Formato), a.TamanhoBytes, a.HashSha256, a.ConfirmadoEm))];

    /// <summary>
    /// O que o envio faz com os arquivos, sobre a avaliação da versão fixada: o arquivo confirmado de
    /// exigência que se aplica passa a constar do envio e nunca mais é apagado; o de exigência que não se
    /// aplica — ou que a versão fixada não tem — é removido, se nunca constou de um envio, e as chaves
    /// dele ficam agendadas para a rotina de limpeza, na mesma transação (nunca se apaga o armazenamento
    /// antes do commit). No
    /// rascunho nada disso roda: desmarcar e remarcar uma condição não perde o arquivo.
    /// </summary>
    public static async Task ConciliarNoEnvioAsync(
        IDocumentoDaInscricaoRepository repositorio,
        Guid inscricaoId,
        ResultadoDaAvaliacaoDaInscricao avaliacao,
        IReadOnlyList<ArquivoConfirmado> noComprovante,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(noComprovante);

        HashSet<Guid> constamDoComprovante = [.. noComprovante.Select(static a => a.ArquivoId)];
        HashSet<(Guid Exigencia, string? Ocorrencia)> queSeAplicam = [.. avaliacao.Documentos.Itens
            .Where(static d => d.Situacao != SituacaoDoDocumento.NaoExigido)
            .Select(static d => (d.Documento.Id, d.OcorrenciaId))];
        Dictionary<Guid, ApresentacaoDeDocumento> apresentacoes = (await repositorio
            .ListarApresentacoesDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(static a => a.Id);

        foreach (ArquivoDaApresentacao listado in await repositorio.ListarArquivosDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false))
        {
            // Um arquivo removido ou criado em paralelo entre as consultas fica para o envio seguinte.
            if (!apresentacoes.TryGetValue(listado.ApresentacaoId, out ApresentacaoDeDocumento? apresentacao)
                || await repositorio.ObterArquivoAsync(listado.Id, cancellationToken).ConfigureAwait(false) is not { } arquivo)
            {
                continue;
            }

            if (queSeAplicam.Contains((apresentacao.ExigenciaId, apresentacao.OcorrenciaId)))
            {
                // O arquivo confirmado depois que o comprovante foi composto fica para o envio seguinte: só consta
                // do envio o que o comprovante registra.
                if (constamDoComprovante.Contains(arquivo.Id))
                {
                    arquivo.MarcarComoConstanteDeEnvio();
                }
            }
            else if (!arquivo.ConstouDeEnvio)
            {
                await repositorio.RemoverArquivoAsync(arquivo, cancellationToken).ConfigureAwait(false);
                await AgendaDeLimpezaDoArquivoDoCandidato.AgendarAsync(repositorio, arquivo, comSelada: true, agora, cancellationToken).ConfigureAwait(false);
            }
        }

    }
}
