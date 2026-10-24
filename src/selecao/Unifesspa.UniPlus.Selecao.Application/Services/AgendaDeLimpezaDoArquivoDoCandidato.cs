namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

/// <summary>
/// Quando a chave definitiva de um arquivo removido pode ser apagada. O objeto já promovido sai já; o arquivo em
/// verificação pode ser promovido a qualquer momento, e a chave dele espera o prazo da URL de envio e a tolerância.
/// O bucket de envio não entra: a expiração dele cuida do que sobra. O armazenamento só é tocado depois do commit.
/// </summary>
public static class AgendaDeLimpezaDoArquivoDoCandidato
{
    public static async Task AgendarAsync(
        IDocumentoDaInscricaoRepository repositorio,
        ArquivoDaApresentacao arquivo,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(arquivo);

        switch (arquivo.Situacao)
        {
            case SituacaoDoArquivoDoCandidato.Verificado:
                await repositorio.AgendarLimpezaAsync(arquivo.ChaveDefinitivaPrevista, agora, cancellationToken).ConfigureAwait(false);
                break;
            case SituacaoDoArquivoDoCandidato.EmVerificacao:
                await repositorio.AgendarLimpezaAsync(
                    arquivo.ChaveDefinitivaPrevista, arquivo.ExpiraEm + RemocaoDeArquivosPendentesVencidos.Tolerancia, cancellationToken).ConfigureAwait(false);
                break;
        }
    }
}
