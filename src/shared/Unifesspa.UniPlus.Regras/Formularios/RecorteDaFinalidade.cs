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
        DefinicaoEtapa[] etapas = [.. definicao.Etapas.Where(e => e.Codigo == finalidade || e.Codigo.StartsWith(prefixo, StringComparison.Ordinal))];
        DefinicaoTermo[] termos = [.. definicao.Termos.Where(t => t.Codigo.StartsWith(prefixo, StringComparison.Ordinal))];

        HashSet<string> grupos = new(etapas.SelectMany(static e => e.Grupos).Select(static g => g.Codigo), StringComparer.Ordinal);
        HashSet<string> produzidos = new(
            etapas.SelectMany(static e => e.Itens.Select(static i => i.FatoCodigo)
                .Concat(e.Grupos.SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo))),
            StringComparer.Ordinal);

        Dictionary<string, RegrasDerivacaoFato> derivacaoPorFato = definicao.Derivacoes.ToDictionary(static d => d.CodigoFato, StringComparer.Ordinal);
        Dictionary<string, DefinicaoAgregado> agregadoPorFato = definicao.Agregados
            .Where(a => grupos.Contains(a.GrupoCodigo))
            .ToDictionary(static a => a.Codigo, StringComparer.Ordinal);

        HashSet<string> derivacoesCitadas = new(StringComparer.Ordinal);
        HashSet<string> agregadosCitados = new(StringComparer.Ordinal);
        HashSet<string> pressupostos = new(StringComparer.Ordinal);
        Queue<string> aVisitar = new(CitadosPeloFormulario(etapas, termos));
        HashSet<string> visitados = new(StringComparer.Ordinal);
        while (aVisitar.TryDequeue(out string? fato))
        {
            if (!visitados.Add(fato) || produzidos.Contains(fato))
            {
                continue;
            }

            if (derivacaoPorFato.TryGetValue(fato, out RegrasDerivacaoFato? derivacao))
            {
                derivacoesCitadas.Add(fato);
                foreach (string dependencia in derivacao.DependenciasDeclaradas)
                {
                    aVisitar.Enqueue(dependencia);
                }
            }
            else if (agregadoPorFato.ContainsKey(fato))
            {
                agregadosCitados.Add(fato);
            }
            else
            {
                pressupostos.Add(fato);
            }
        }

        DefinicaoFormulario recortada = new(
            etapas,
            termos,
            [.. definicao.Derivacoes.Where(d => derivacoesCitadas.Contains(d.CodigoFato))],
            [.. definicao.Agregados.Where(a => agregadosCitados.Contains(a.Codigo))]);

        IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertasDoRecorte = ofertas?
            .Where(o => produzidos.Contains(o.Key))
            .ToDictionary(static o => o.Key, static o => o.Value, StringComparer.Ordinal);

        return new(FormularioPortavel.De(recortada, ofertasDoRecorte), [.. pressupostos.Order(StringComparer.Ordinal)]);
    }

    private static IEnumerable<string> CitadosPeloFormulario(IEnumerable<DefinicaoEtapa> etapas, IEnumerable<DefinicaoTermo> termos) =>
        etapas.SelectMany(static e => (e.Exibicao?.FatosCitados ?? [])
                .Concat(e.Itens.SelectMany(static i => i.FatosCitados))
                .Concat(e.Grupos.SelectMany(static g => g.FatosDoCandidatoCitados)))
            .Concat(termos.SelectMany(static t => (t.Exibicao?.FatosCitados ?? []).Concat(t.Obrigatoriedade.FatosCitados)));
}
