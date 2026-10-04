namespace Unifesspa.UniPlus.Selecao.API.Rotinas;

/// <summary>
/// Configuração da rotina que remove os envios de arquivo pendentes vencidos (seção
/// <c>Selecao:RemocaoDeArquivosPendentes</c>). Opcional: sem a seção, vale o padrão.
/// </summary>
internal sealed class RemocaoDeArquivosPendentesOptions
{
    public const string SectionName = "Selecao:RemocaoDeArquivosPendentes";

    /// <summary>Menor intervalo aceito: abaixo dele a varredura vira carga constante sem ganho.</summary>
    public static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maior intervalo aceito: um dia já deixa a sobra por tempo demais, e bem antes do teto do
    /// <c>PeriodicTimer</c>, que recusaria o intervalo só na partida da rotina.
    /// </summary>
    public static readonly TimeSpan IntervaloMaximo = TimeSpan.FromDays(1);

    /// <summary>
    /// Intervalo entre as execuções. A URL de envio vale minutos; uma hora mantém a sobra curta
    /// sem varrer as tabelas a todo instante.
    /// </summary>
    public TimeSpan Intervalo { get; set; } = TimeSpan.FromHours(1);
}
