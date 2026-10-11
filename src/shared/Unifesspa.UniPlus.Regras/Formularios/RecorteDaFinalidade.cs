namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras de um formulário, recortadas da definição do processo, que junta todas as finalidades
/// porque o formulário seguinte cita os fatos e os derivados do anterior. O recorte traz as etapas e os
/// termos da finalidade, as derivações e os agregados que eles citam — com o fechamento das
/// dependências — e, como <see cref="Pressupostos"/>, os fatos citados que o recorte não produz: os de
/// outra finalidade e os do sistema, que o interpretador recebe como já conhecidos (ADR-0139).
/// </summary>
/// <remarks>
/// Só o que o formulário cita sai: a derivação que nenhuma regra do recorte alcança fica de fora, como
/// o resto das regras de derivação do processo. O agregado sobre grupo de outra finalidade não se
/// calcula no recorte, que não tem o grupo; ele é pressuposto, como o fato que vem de antes.
/// </remarks>
public sealed record RecorteDaFinalidade(FormularioPortavel Regras, IReadOnlyList<string> Pressupostos)
{
    /// <summary>
    /// O recorte da finalidade cujo código prefixa as etapas e os termos na definição do processo: a
    /// etapa da finalidade se chama como ela, ou <c>FINALIDADE:SECAO</c>, e o termo, <c>FINALIDADE:TERMO</c>.
    /// </summary>
    public static RecorteDaFinalidade De(
        DefinicaoFormulario definicao, string finalidade, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas = null)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalidade);

        string prefixo = finalidade + ":";
        return Recortar(
            definicao,
            [.. definicao.Etapas.Where(e => e.Codigo == finalidade || e.Codigo.StartsWith(prefixo, StringComparison.Ordinal))],
            [.. definicao.Termos.Where(t => t.Codigo.StartsWith(prefixo, StringComparison.Ordinal))],
            ofertas);
    }

    /// <summary>
    /// O recorte de uma definição que é um formulário só, como a do modelo: todas as etapas e todos os
    /// termos, com o fechamento das derivações e dos agregados que eles citam e os pressupostos.
    /// </summary>
    public static RecorteDaFinalidade DoFormulario(DefinicaoFormulario definicao, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas = null)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        return Recortar(definicao, [.. definicao.Etapas], [.. definicao.Termos], ofertas);
    }

    private static RecorteDaFinalidade Recortar(
        DefinicaoFormulario definicao, DefinicaoEtapa[] etapas, DefinicaoTermo[] termos, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas)
    {
        ProducaoDasEtapas producao = ProducaoDasEtapas.De(definicao, etapas);
        FechamentoDasDependencias fechamento = producao.Fechamento(CitadosPeloFormulario(etapas, termos));

        DefinicaoFormulario recortada = new(
            etapas,
            termos,
            [.. definicao.Derivacoes.Where(d => fechamento.Derivacoes.Contains(d.CodigoFato))],
            [.. definicao.Agregados.Where(a => fechamento.Agregados.Contains(a.Codigo))]);

        IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertasDoRecorte = ofertas?
            .Where(o => producao.Produzidos.Contains(o.Key))
            .ToDictionary(static o => o.Key, static o => o.Value, StringComparer.Ordinal);

        return new(FormularioPortavel.De(recortada, ofertasDoRecorte), fechamento.Pressupostos);
    }

    private static IEnumerable<string> CitadosPeloFormulario(IEnumerable<DefinicaoEtapa> etapas, IEnumerable<DefinicaoTermo> termos) =>
        etapas.SelectMany(static e => (e.Exibicao?.FatosCitados ?? [])
                .Concat(e.Itens.SelectMany(static i => i.FatosCitados))
                .Concat(e.Grupos.SelectMany(static g => g.FatosDoCandidatoCitados)))
            .Concat(termos.SelectMany(static t => (t.Exibicao?.FatosCitados ?? []).Concat(t.Obrigatoriedade.FatosCitados)));
}
