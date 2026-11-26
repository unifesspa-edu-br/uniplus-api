namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras de um formulário, recortadas da definição do processo, que junta todos os formulários
/// porque o formulário seguinte cita os fatos e os derivados do anterior. O recorte traz as páginas e os
/// termos do formulário, as derivações e os agregados que eles citam — com o fechamento das
/// dependências — e, como <see cref="Pressupostos"/>, os fatos citados que o recorte não produz: os de
/// outro formulário e os do sistema, que o interpretador recebe como já conhecidos (ADR-0139).
/// </summary>
/// <remarks>
/// Só o que o formulário cita sai: a derivação que nenhuma regra do recorte alcança fica de fora, como
/// o resto das regras de derivação do processo. O agregado sobre grupo de outro formulário não se
/// calcula no recorte, que não tem o grupo; ele é pressuposto, como o fato que vem de antes.
/// </remarks>
public sealed record RecorteDoFormulario(FormularioPortavel Regras, IReadOnlyList<string> Pressupostos)
{
    /// <summary>
    /// O recorte do formulário cujo código prefixa as páginas e os termos na definição do processo: a
    /// página do formulário se chama como ele, ou <c>CODIGO:SECAO</c>, e o termo, <c>CODIGO:TERMO</c>.
    /// </summary>
    public static RecorteDoFormulario De(
        DefinicaoFormulario definicao, string codigoDoFormulario, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas = null)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDoFormulario);

        string prefixo = codigoDoFormulario + ":";
        return Recortar(
            definicao,
            [.. definicao.Paginas.Where(e => e.Codigo == codigoDoFormulario || e.Codigo.StartsWith(prefixo, StringComparison.Ordinal))],
            [.. definicao.Termos.Where(t => t.Codigo.StartsWith(prefixo, StringComparison.Ordinal))],
            ofertas);
    }

    /// <summary>
    /// O recorte de uma definição que é um formulário só, como a do modelo: todas as páginas e todos os
    /// termos, com o fechamento das derivações e dos agregados que eles citam e os pressupostos.
    /// </summary>
    public static RecorteDoFormulario DaDefinicaoInteira(DefinicaoFormulario definicao, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas = null)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        return Recortar(definicao, [.. definicao.Paginas], [.. definicao.Termos], ofertas);
    }

    private static RecorteDoFormulario Recortar(
        DefinicaoFormulario definicao, DefinicaoPagina[] paginas, DefinicaoTermo[] termos, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas)
    {
        ProducaoDasPaginas producao = ProducaoDasPaginas.De(definicao, paginas);
        FechamentoDasDependencias fechamento = producao.Fechamento(CitadosPeloFormulario(paginas, termos));

        DefinicaoFormulario recortada = new(
            paginas,
            termos,
            [.. definicao.Derivacoes.Where(d => fechamento.Derivacoes.Contains(d.CodigoFato))],
            [.. definicao.Agregados.Where(a => fechamento.Agregados.Contains(a.Codigo))]);

        IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertasDoRecorte = ofertas?
            .Where(o => producao.Produzidos.Contains(o.Key))
            .ToDictionary(static o => o.Key, static o => o.Value, StringComparer.Ordinal);

        return new(FormularioPortavel.De(recortada, ofertasDoRecorte), fechamento.Pressupostos);
    }

    private static IEnumerable<string> CitadosPeloFormulario(IEnumerable<DefinicaoPagina> paginas, IEnumerable<DefinicaoTermo> termos) =>
        paginas.SelectMany(static e => (e.Exibicao?.FatosCitados ?? [])
                .Concat(e.Itens.SelectMany(static i => i.FatosCitados))
                .Concat(e.Grupos.SelectMany(static g => g.FatosDoCandidatoCitados)))
            .Concat(termos.SelectMany(static t => (t.Exibicao?.FatosCitados ?? []).Concat(t.Obrigatoriedade.FatosCitados)));
}
