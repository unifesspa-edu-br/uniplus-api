namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// Os parâmetros do recurso como o pedido os traz, para o teste montar a mesma declaração na fase e na etapa.
/// </summary>
internal sealed record ParametrosDoPedidoDoRecurso(
    decimal? PrazoValor,
    string? PrazoUnidade,
    bool AdmiteInstanciaSuperior,
    decimal? PrazoSegundaInstanciaValor,
    string? PrazoSegundaInstanciaUnidade,
    string? EfeitoSuspensivo,
    string? GuardaDaVaga)
{
    /// <summary>Declaração aceita: 2 dias úteis, sem instância superior, sem efeito suspensivo e sem guarda declarada.</summary>
    internal static ParametrosDoPedidoDoRecurso Valido { get; } = new(2m, "DIAS_UTEIS", false, null, null, "SEM_EFEITO_SUSPENSIVO", null);
}

/// <summary>
/// Cada causa de recusa dos parâmetros do recurso, com a declaração que a provoca, o campo recusado e o sufixo do código de
/// erro, igual na fase (<c>RegraRecursoFase</c>) e na etapa (<c>RecursoDaEtapa</c>).
/// </summary>
internal static class CausasDeRecusaDosParametrosDoRecurso
{
    private static readonly ParametrosDoPedidoDoRecurso ComInstanciaSuperior = ParametrosDoPedidoDoRecurso.Valido with
    {
        AdmiteInstanciaSuperior = true,
        PrazoSegundaInstanciaValor = 3m,
        PrazoSegundaInstanciaUnidade = "DIAS_UTEIS",
    };

    internal static readonly (string Codigo, string Campo, ParametrosDoPedidoDoRecurso Pedido)[] Todas =
    [
        ("PrazoNaoPositivo", "prazoValor", ParametrosDoPedidoDoRecurso.Valido with { PrazoValor = 0m }),
        ("PrazoSemUnidadeDeclaravel", "prazoUnidade", ParametrosDoPedidoDoRecurso.Valido with { PrazoUnidade = null }),
        ("PrazoEmDiasCorridos", "prazoUnidade", ParametrosDoPedidoDoRecurso.Valido with { PrazoUnidade = "DIAS" }),
        ("PrazoEmFracaoDeDiaUtil", "prazoValor", ParametrosDoPedidoDoRecurso.Valido with { PrazoValor = 1.5m }),
        ("EfeitoSuspensivoNaoDeclarado", "efeitoSuspensivo", ParametrosDoPedidoDoRecurso.Valido with { EfeitoSuspensivo = null }),
        ("PrazoSegundaInstanciaNaoDeclarado", "prazoSegundaInstanciaValor", ComInstanciaSuperior with { PrazoSegundaInstanciaValor = null, PrazoSegundaInstanciaUnidade = null }),
        ("PrazoSegundaInstanciaSemInstanciaSuperior", "prazoSegundaInstanciaValor", ParametrosDoPedidoDoRecurso.Valido with { PrazoSegundaInstanciaValor = 3m, PrazoSegundaInstanciaUnidade = "DIAS_UTEIS" }),
        ("NaoGuardaComInstanciaSuperior", "guardaDaVaga", ComInstanciaSuperior with { GuardaDaVaga = "NAO_GUARDA" }),
        ("GuardaDaVagaNaoDeclaravel", "guardaDaVaga", ParametrosDoPedidoDoRecurso.Valido with { GuardaDaVaga = "talvez" }),
    ];

    /// <summary>Os nove sufixos, um por linha de teste, para o teste buscar a causa pelo nome.</summary>
    public static TheoryData<string> Nomes => [.. Todas.Select(static causa => causa.Codigo)];

    internal static (string Codigo, string Campo, ParametrosDoPedidoDoRecurso Pedido) Da(string codigo) =>
        Array.Find(Todas, causa => string.Equals(causa.Codigo, codigo, StringComparison.Ordinal));
}
