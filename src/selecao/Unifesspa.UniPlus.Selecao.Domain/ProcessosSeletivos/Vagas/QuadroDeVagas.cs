namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// O quadro de vagas de uma oferta de curso (ADR-0115): a quantidade por modalidade e o total que o
/// edital publica. Cada ramo da distribuição o produz com a forma própria, e a forma de um ramo não
/// carrega o campo do outro.
/// </summary>
public abstract record QuadroDeVagas(IReadOnlyList<VagasDaModalidade> Vagas, int TotalDeVagasDoEdital)
{
    /// <summary>A quantidade da modalidade no quadro; zero quando o quadro não a tem.</summary>
    public int QuantidadeDe(string modalidadeCodigo) =>
        Vagas.FirstOrDefault(vagas => string.Equals(vagas.ModalidadeCodigo, modalidadeCodigo, StringComparison.Ordinal))?.Quantidade ?? 0;

    /// <summary>As vagas por modalidade na ordem ordinal do código da modalidade, a ordem em que o quadro é projetado.</summary>
    public IReadOnlyList<VagasDaModalidade> VagasPeloCodigoDaModalidade() => PeloCodigoDaModalidade(Vagas);

    protected static IReadOnlyList<VagasDaModalidade> PeloCodigoDaModalidade(IEnumerable<VagasDaModalidade> linhas) =>
        [.. linhas.OrderBy(static linha => linha.ModalidadeCodigo, StringComparer.Ordinal)];
}
