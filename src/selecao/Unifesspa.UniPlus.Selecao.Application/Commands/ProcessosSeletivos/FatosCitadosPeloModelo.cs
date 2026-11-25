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
    public static string[] Todos(ConteudoDoModeloInput conteudo, FormularioSelecionado formulario, TermosSelecionados termos)
    {
        IEnumerable<DocumentoExigidoDoModeloInput> documentos = (conteudo.ConjuntoDeDocumentos?.TodosOsDocumentos() ?? []).Select(static c => c.Documento);
        return [.. DasPartes(formulario.Itens.Entradas, formulario.Grupos.Entradas, conteudo.Paginas ?? [], termos.Entradas)
            .Concat(documentos.SelectMany(static d => Do(d.Quando)))
            .Concat(documentos.Select(static d => d.PorOpcao?.FatoCodigo).OfType<string>())
            .Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> DasPartes(
        IEnumerable<FatoColetadoInput> itens,
        IEnumerable<GrupoColetadoInput> grupos,
        IEnumerable<PaginaDoFormularioInput> paginas,
        IEnumerable<TermoExigidoInput> termos) =>
        itens.Concat(grupos.SelectMany(static g => g.Subitens ?? []))
            .SelectMany(static i => DasRegras(i?.Regras, i?.FatoCodigo))
            .Concat(grupos.SelectMany(static g => DasRegrasDeExibicao(g.Regras)))
            .Concat(paginas.SelectMany(static e => Do(e?.Exibicao)))
            .Concat(termos.SelectMany(static t => DasRegrasDeExibicao(t.Regras)));

    private static IEnumerable<string> DasRegrasDeExibicao(ExibicaoEObrigatoriedadeInput? regras) =>
        Do(regras?.Exibicao).Concat(Do(regras?.Obrigatoriedade?.Predicado));

    private static IEnumerable<string> DasRegras(RegrasDoCampoInput? regras, string? fatoDoCampo) =>
        Do(regras?.Exibicao).Concat(Do(regras?.Obrigatoriedade?.Predicado))
            .Concat(Do(regras?.Impedimento?.Quando).Where(f => !string.Equals(f, fatoDoCampo, StringComparison.Ordinal)))
            .Concat(DasRestricoes(regras?.Restricoes ?? []));

    private static IEnumerable<string> DasRestricoes(IEnumerable<RestricaoValorInput> restricoes) =>
        restricoes.SelectMany(static r => (r.Fatos ?? []).Concat((r.Entradas ?? []).SelectMany(static e => Do(e.Quando))));

    private static IEnumerable<string> Do(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado) =>
        (predicado ?? []).SelectMany(static clausula => clausula ?? []).Select(static c => c?.Fato).OfType<string>();
}
