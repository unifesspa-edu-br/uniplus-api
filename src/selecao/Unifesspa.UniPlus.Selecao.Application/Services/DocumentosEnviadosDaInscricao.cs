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
    /// O que o envio faz com os arquivos, sobre a avaliação da versão fixada: o arquivo verificado de
    /// exigência que se aplica passa a constar do envio e nunca mais é apagado; o de exigência que não se
    /// aplica — ou que a versão fixada não tem — é removido, se nunca constou de um envio, e as chaves
    /// dele ficam agendadas para a rotina de limpeza, na mesma transação (nunca se apaga o armazenamento
    /// antes do commit). No
    /// rascunho nada disso roda: desmarcar e remarcar uma condição não perde o arquivo.
    /// </summary>
    public static async Task ConciliarNoEnvioAsync(
        IDocumentoDaInscricaoRepository repositorio,
        ArquivosDaInscricao arquivos,
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
            // Um arquivo removido em paralelo entre as consultas fica para o envio seguinte.
            if (await repositorio.ObterArquivoAsync(listado.Id, cancellationToken).ConfigureAwait(false) is not { } arquivo)
            {
                continue;
            }

            ApresentacaoDeDocumento apresentacao = arquivos.ApresentacaoDe(listado);

            if (queSeAplicam.Contains((apresentacao.ExigenciaId, apresentacao.OcorrenciaId)))
            {
                // O arquivo verificado depois que o comprovante foi composto fica para o envio seguinte: só consta
                // do envio o que o comprovante registra.
                if (constamDoComprovante.Contains(arquivo.Id))
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
}
