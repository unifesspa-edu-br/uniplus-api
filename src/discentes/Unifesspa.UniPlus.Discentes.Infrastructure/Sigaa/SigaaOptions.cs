namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Configuração de acesso à API do SIGAA, de onde vêm os vínculos de discentes.
/// Mapeia a seção <c>Sigaa</c> da configuração.
/// </summary>
/// <remarks>
/// <see cref="Senha"/> é credencial de sistema institucional e por isso não tem chave
/// correspondente em nenhum arquivo versionado — nem vazia. Chega ao processo pela
/// variável de ambiente <c>Sigaa__Senha</c>, populada a partir do cofre pelo operador de
/// segredos do cluster; em máquina de desenvolvimento, por <c>dotnet user-secrets</c>.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "Opção vinda de IConfiguration — texto é a forma que o binder lê. O formato é validado em SigaaOptionsValidator.")]
public sealed class SigaaOptions
{
    public const string SectionName = "Sigaa";

    /// <summary>
    /// Teto que a origem impõe ao tamanho de página. Pedir mais que isso é recusado por ela.
    /// </summary>
    public const int MaximoItensPorPagina = 200;

    /// <summary>
    /// Endereço base da API, sem o caminho do recurso
    /// (ex.: <c>https://api-sigaa-v2.unifesspa.edu.br</c>).
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Usuário de serviço que autentica a sincronização.
    /// </summary>
    public string Usuario { get; init; } = string.Empty;

    /// <summary>
    /// Senha do usuário de serviço.
    /// </summary>
    public string Senha { get; init; } = string.Empty;

    /// <summary>
    /// Quantos vínculos pedir por página, limitado a <see cref="MaximoItensPorPagina"/>.
    /// </summary>
    public int ItensPorPagina { get; init; } = MaximoItensPorPagina;

    /// <summary>
    /// Quantas páginas buscar ao mesmo tempo. Um significa varredura sequencial.
    /// </summary>
    public int GrauDeParalelismo { get; init; } = 5;

    /// <summary>
    /// Validade assumida para o token quando ele não permite descobrir a própria
    /// expiração. É rede de segurança, não o caminho normal.
    /// </summary>
    public int ValidadeAssumidaDoTokenEmSegundos { get; init; } = 3600;

    /// <summary>
    /// Teto da validade aceita para um token, qualquer que seja a que ele declare. Protege
    /// contra uma expiração absurda vinda da origem fixar um token morto indefinidamente.
    /// </summary>
    public int ValidadeMaximaDoTokenEmSegundos { get; init; } = 86_400;

    /// <summary>
    /// Margem antes da expiração em que o token é renovado, para que uma requisição não
    /// saia com um token que vence no caminho.
    /// </summary>
    public int MargemDeRenovacaoDoTokenEmSegundos { get; init; } = 60;

    /// <summary>
    /// Tempo limite de uma tentativa isolada.
    /// </summary>
    public int TimeoutPorTentativaEmSegundos { get; init; } = 30;

    /// <summary>
    /// Tempo limite da operação inteira, incluindo as retentativas. Precisa ser
    /// estritamente menor que o tempo limite do próprio cliente HTTP, senão o
    /// cancelamento observado vem do cliente e o diagnóstico aponta a causa errada.
    /// </summary>
    public int TimeoutTotalEmSegundos { get; init; } = 120;

    /// <summary>
    /// Quantas vezes repetir uma requisição que falhou por causa transitória.
    /// </summary>
    public int MaximoDeRetentativas { get; init; } = 3;

    /// <summary>
    /// Espera antes da primeira repetição. As seguintes crescem a partir dela, com
    /// variação aleatória para que várias páginas que falharam juntas não voltem todas no
    /// mesmo instante.
    /// </summary>
    public int EsperaBaseEntreTentativasEmMilissegundos { get; init; } = 500;

    /// <summary>
    /// Quantas chamadas precisam ocorrer na janela de amostragem antes de o corte de
    /// circuito considerar abrir. O padrão da biblioteca pressupõe tráfego de API pública
    /// e nunca seria alcançado por uma sincronização diária de poucas dezenas de páginas.
    /// </summary>
    public int ChamadasMinimasParaAvaliarCorte { get; init; } = 10;

    /// <summary>
    /// Janela em que as falhas são contadas para decidir o corte de circuito. Precisa ser
    /// pelo menos o dobro do limite de uma tentativa: uma janela que mal comporta uma
    /// tentativa não chega a formar amostra, e o corte nunca teria como reagir.
    /// </summary>
    public int JanelaDeAmostragemDoCorteEmSegundos { get; init; } = 120;

    /// <summary>
    /// Proporção de falhas na janela a partir da qual o circuito abre.
    /// </summary>
    public double ProporcaoDeFalhaParaAbrirCorte { get; init; } = 0.5;

    /// <summary>
    /// Quanto tempo o circuito permanece aberto antes de deixar passar uma chamada de prova.
    /// </summary>
    public int DuracaoDoCorteEmSegundos { get; init; } = 15;
}
