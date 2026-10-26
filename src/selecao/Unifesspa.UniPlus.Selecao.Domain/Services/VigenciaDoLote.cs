namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Vale o lote mais recente do processo, da etapa e da edição, por inteiro: o retorno do INEP é o
/// retrato completo da edição, então quem não aparece no lote novo fica sem nota, mesmo que tivesse
/// no anterior. A vigência é derivada da ordem dos lotes, não gravada, e quem a resolve é a leitura do repositório.
/// </summary>
public static class VigenciaDoLote
{
    /// <summary>
    /// Reimportar o arquivo do lote vigente não cria outro lote. O arquivo de um lote já substituído
    /// não conta: importá-lo cria um lote novo, que volta a valer.
    /// </summary>
    public static bool ArquivoJaVigente(LoteDeNotasDoEnem? vigente, string arquivoHash) =>
        vigente is not null && string.Equals(vigente.ArquivoHash, arquivoHash, StringComparison.Ordinal);
}
