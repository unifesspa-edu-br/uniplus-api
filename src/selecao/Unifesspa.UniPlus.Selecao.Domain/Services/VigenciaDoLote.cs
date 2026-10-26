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
    /// Reimportar o arquivo do lote vigente, sob a mesma versão divulgada do edital, não cria outro
    /// lote. O arquivo de um lote já substituído não conta: importá-lo cria um lote novo, que volta
    /// a valer. Sob outra versão (uma retificação divulgada) o mesmo arquivo também cria lote novo,
    /// porque os pesos que apuram a nota podem ter mudado.
    /// </summary>
    public static bool ArquivoJaVigente(LoteDeNotasDoEnem? vigente, string arquivoHash, Guid versaoConfiguracaoId) =>
        vigente is not null
        && vigente.VersaoConfiguracaoId == versaoConfiguracaoId
        && string.Equals(vigente.ArquivoHash, arquivoHash, StringComparison.Ordinal);
}
