namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// A cópia por valor dos leiautes que a declaração cita, tirada do catálogo de leiautes no instante em que a
/// versão congela. Alterar o catálogo depois não muda a versão publicada. O leiaute que o catálogo não tem não
/// entra na cópia: a conferência da declaração o recusa antes.
/// </summary>
public static class CopiaDosLeiautesDeclarados
{
    public static IReadOnlyList<LeiauteDoRelatorio> Copiar(
        DeclaracaoDaImportacaoDoResultado declaracao, IReadOnlyCollection<LeiauteDoRelatorio> catalogo)
    {
        ArgumentNullException.ThrowIfNull(declaracao);
        ArgumentNullException.ThrowIfNull(catalogo);

        return [.. declaracao.Relatorios
            .Select(relatorio => catalogo.FirstOrDefault(leiaute => Cita(relatorio, leiaute)))
            .OfType<LeiauteDoRelatorio>()
            .Select(static leiaute => leiaute with { Dados = [.. leiaute.Dados] })];
    }

    private static bool Cita(RelatorioDeclarado relatorio, LeiauteDoRelatorio leiaute) =>
        string.Equals(relatorio.LeiauteCodigo, leiaute.Codigo, StringComparison.Ordinal)
        && string.Equals(relatorio.LeiauteVersao, leiaute.Versao, StringComparison.Ordinal);
}
