namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Interfaces;

/// <summary>
/// A regra única de quando as chaves do armazenamento de um arquivo do candidato podem ser apagadas de vez. A URL
/// pré-assinada de envio vale até <see cref="ArquivoDaApresentacao.ExpiraEm"/> e um PUT nela recria o conteúdo em
/// claro: a chave de envio só se apaga de vez depois do prazo e da tolerância da rotina de remoção. A selada,
/// nunca exposta por URL, pode sair já. O armazenamento nunca é tocado antes do commit de quem agenda.
/// </summary>
public static class AgendaDeLimpezaDoArquivoDoCandidato
{
    /// <summary>Agenda a chave de envio para depois do prazo da URL e, quando <paramref name="comSelada"/>, a selada para <paramref name="agora"/>.</summary>
    public static async Task AgendarAsync(
        IDocumentoDaInscricaoRepository repositorio,
        ArquivoDaApresentacao arquivo,
        bool comSelada,
        DateTimeOffset agora,
        CancellationToken cancellationToken,
        bool envioTambemAgora = false)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(arquivo);

        await repositorio
            .AgendarLimpezaAsync(arquivo.ObjectKey, arquivo.ExpiraEm + RemocaoDeArquivosPendentesVencidos.Tolerancia, cancellationToken)
            .ConfigureAwait(false);
        if (envioTambemAgora)
        {
            // O conteúdo em claro não precisa esperar o prazo para sair da próxima execução da rotina; o
            // PUT que o recriar dentro do prazo cai na agenda de cima.
            await repositorio.AgendarLimpezaAsync(arquivo.ObjectKey, agora, cancellationToken).ConfigureAwait(false);
        }

        if (comSelada)
        {
            await repositorio.AgendarLimpezaAsync(arquivo.ChaveSelada, agora, cancellationToken).ConfigureAwait(false);
        }
    }
}
