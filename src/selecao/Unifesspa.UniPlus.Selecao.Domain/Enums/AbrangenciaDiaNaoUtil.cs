namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Onde um dia não útil incide — o vocabulário fechado que o calendário de dias úteis do
/// módulo Configuração usa, congelado por valor no envelope de publicação (UNI-REQ-0080).
/// </summary>
/// <remarks>
/// <para>
/// Os tokens são os mesmos que <c>CalendarioVigenteView</c> entrega, em UPPER_SNAKE sem
/// acento. Repeti-los aqui como constantes, em vez de referenciar o enum de Configuração,
/// é o que mantém o Domain de Seleção sem dependência do Domain do outro módulo: o que
/// atravessa a fronteira é o contrato, não o tipo.
/// </para>
/// <para>
/// A abrangência governa quais campos territoriais o dia carrega — nacional e institucional
/// não carregam nenhum, estadual carrega a UF, municipal carrega o trio completo. Essa
/// correspondência é verificada em <see cref="DiaNaoUtilCongelado.Criar"/>, e o decoder do
/// envelope a reaplica sem afrouxar.
/// </para>
/// </remarks>
public static class AbrangenciaDiaNaoUtil
{
    /// <summary>Feriado nacional — incide em todo o território, sem recorte.</summary>
    public const string Nacional = "NACIONAL";

    /// <summary>Feriado estadual — incide na UF declarada.</summary>
    public const string Estadual = "ESTADUAL";

    /// <summary>Feriado municipal — incide no município declarado, identificado pelo código IBGE.</summary>
    public const string Municipal = "MUNICIPAL";

    /// <summary>Recesso ou ponto facultativo declarado por ato da própria instituição.</summary>
    public const string Institucional = "INSTITUCIONAL";

    /// <summary>As quatro abrangências, na ordem canônica de declaração.</summary>
    public static IReadOnlyList<string> Todas { get; } = [Nacional, Estadual, Municipal, Institucional];

    /// <summary>Indica se <paramref name="token"/> pertence ao vocabulário.</summary>
    public static bool EhValida(string? token) =>
        token is not null && Todas.Contains(token, StringComparer.Ordinal);
}
