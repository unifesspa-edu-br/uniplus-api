namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Endereços das origens que a Portal compõe e as políticas de resiliência das consultas a elas.
/// Mapeia a seção <c>Origens</c> da configuração.
/// </summary>
/// <remarks>
/// Os endpoints consumidos são anônimos: nenhuma credencial é configurada aqui, e nenhuma deve
/// ser. Os limites de tempo são curtos de propósito — a página pública espera por essas consultas
/// a cada leitura, e uma origem lenta vira página lenta.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "Opção vinda de IConfiguration — texto é a forma que o binder lê. O formato é validado em OrigensOptionsValidator.")]
public sealed class OrigensOptions
{
    public const string SectionName = "Origens";

    /// <summary>Teto que a origem de atos impõe ao tamanho de página.</summary>
    public const int MaximoItensPorPaginaDeAtos = 100;

    /// <summary>
    /// Endereço base do módulo Seleção, sem o caminho do recurso (ex.: <c>http://uniplus-api:8080</c>).
    /// </summary>
    public string SelecaoBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Endereço base do módulo Publicações, sem o caminho do recurso.
    /// </summary>
    public string PublicacoesBaseUrl { get; init; } = string.Empty;

    /// <summary>Quantos atos pedir por página.</summary>
    public int ItensPorPaginaDeAtos { get; init; } = MaximoItensPorPaginaDeAtos;

    /// <summary>
    /// Quantas páginas de atos percorrer no máximo. Atingir o teto degrada a linha do tempo em vez
    /// de truncá-la em silêncio.
    /// </summary>
    public int TetoDePaginasDeAtos { get; init; } = 10;

    /// <summary>Tempo limite de uma tentativa isolada.</summary>
    public int TimeoutPorTentativaEmSegundos { get; init; } = 3;

    /// <summary>
    /// Tempo limite de uma consulta inteira, incluindo as retentativas. Precisa ser maior que o de
    /// uma tentativa, senão nenhuma retentativa cabe nele.
    /// </summary>
    public int TimeoutTotalEmSegundos { get; init; } = 8;

    /// <summary>Quantas vezes repetir uma consulta que falhou por causa transitória.</summary>
    public int MaximoDeRetentativas { get; init; } = 2;

    /// <summary>
    /// Espera antes da primeira repetição; as seguintes crescem a partir dela, com variação
    /// aleatória.
    /// </summary>
    public int EsperaBaseEntreTentativasEmMilissegundos { get; init; } = 200;

    /// <summary>
    /// Quantas chamadas precisam ocorrer na janela de amostragem antes de o corte de circuito
    /// considerar abrir.
    /// </summary>
    public int ChamadasMinimasParaAvaliarCorte { get; init; } = 10;

    /// <summary>
    /// Janela em que as falhas são contadas para decidir o corte. Precisa comportar ao menos duas
    /// tentativas, senão não chega a formar amostra.
    /// </summary>
    public int JanelaDeAmostragemDoCorteEmSegundos { get; init; } = 30;

    /// <summary>Proporção de falhas na janela a partir da qual o circuito abre.</summary>
    public double ProporcaoDeFalhaParaAbrirCorte { get; init; } = 0.5;

    /// <summary>Quanto tempo o circuito permanece aberto antes de deixar passar uma chamada de prova.</summary>
    public int DuracaoDoCorteEmSegundos { get; init; } = 15;
}
