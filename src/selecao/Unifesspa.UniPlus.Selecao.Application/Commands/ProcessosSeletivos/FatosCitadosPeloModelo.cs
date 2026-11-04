namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>
/// Os fatos que o modelo cita nas regras das partes que entram na cópia — exibições, obrigatoriedades,
/// restrições e impedimentos —, lidos da entrada. Entram também as partes que serão recusadas: o que falta
/// configurar aparece junto das outras recusas, e o que saiu da cópia não pesa.
/// </summary>
internal static class FatosCitadosPeloModelo
{
    public static string[] Todos(ConteudoDoModeloInput conteudo, FormularioCopiado formulario, TermosSelecionados termos)
    {
        IReadOnlyList<DocumentoExigidoDoModeloInput> documentos = conteudo.DocumentosExigidos ?? [];
        return [.. DasPartes(formulario.Itens.Entradas, formulario.Grupos.Entradas, conteudo.Etapas ?? [], termos.Entradas)
            .Concat(documentos.SelectMany(static d => Do(d.Quando)))
            .Concat(documentos.Select(static d => d.PorOpcao?.FatoCodigo).OfType<string>())
            .Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> DasPartes(
        IEnumerable<FatoColetadoInput> itens,
        IEnumerable<GrupoColetadoInput> grupos,
        IEnumerable<EtapaFormularioInput> etapas,
        IEnumerable<TermoExigidoInput> termos) =>
        itens.Concat(grupos.SelectMany(static g => g.Subitens ?? []))
            .SelectMany(static i => Do(i?.Precondicao).Concat(Do(i?.PredicadoObrigatoriedade))
                .Concat(Do(i?.Impedimento?.Quando).Where(f => !string.Equals(f, i?.FatoCodigo, StringComparison.Ordinal)))
                .Concat((i?.Restricoes ?? []).SelectMany(static r => (r.Fatos ?? []).Concat((r.Entradas ?? []).SelectMany(static e => Do(e.Quando))))))
            .Concat(grupos.SelectMany(static g => Do(g.Exibicao).Concat(Do(g.PredicadoObrigatoriedade))))
            .Concat(etapas.SelectMany(static e => Do(e?.Exibicao)))
            .Concat(termos.SelectMany(static t => Do(t.Exibicao).Concat(Do(t.PredicadoObrigatoriedade))));

    private static IEnumerable<string> Do(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado) =>
        (predicado ?? []).SelectMany(static clausula => clausula ?? []).Select(static c => c?.Fato).OfType<string>();
}
