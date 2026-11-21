namespace Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

internal static class ConferenciaDasDescricoesDeErros
{
    internal static string[] Conferir(DocumentoDeDescricoesDeErros documento, IEnumerable<string> publicados)
    {
        DescricaoDeErro?[] descricoes = [.. documento.Descricoes ?? []];
        HashSet<string> esperados = new(publicados, StringComparer.Ordinal);
        string[] codigos = [.. descricoes.Select(static descricao => CodigoSeguro(descricao?.Codigo))];
        return
        [
            .. ConferirDocumento(documento),
            .. ConferirPares(esperados, codigos),
            .. descricoes.SelectMany(ConferirDescricao).Distinct(StringComparer.Ordinal),
        ];
    }

    private static IEnumerable<string> ConferirDocumento(DocumentoDeDescricoesDeErros documento)
    {
        if (documento.VersaoDoFormato != DocumentoDeDescricoesDeErros.VersaoAtual)
            yield return "descricoes-de-erros.json: versão do formato desconhecida.";
        if (documento.Descricoes is null)
            yield return "descricoes-de-erros.json: falta a lista de descrições.";
    }

    private static IEnumerable<string> ConferirPares(HashSet<string> esperados, string[] descritos)
    {
        foreach (string codigo in esperados.Except(descritos).Order(StringComparer.Ordinal))
            yield return $"{codigo}: código sem descrição.";
        foreach (string codigo in descritos.Except(esperados).Where(static codigo => codigo.Length > 0).Order(StringComparer.Ordinal))
            yield return $"{codigo}: descrição sem código.";
        foreach (IGrouping<string, string> repetido in descritos.GroupBy(static codigo => codigo).Where(static grupo => grupo.Key.Length > 0 && grupo.Skip(1).Any()))
            yield return $"{repetido.Key}: descrição repetida.";
    }

    private static IEnumerable<string> ConferirDescricao(DescricaoDeErro? descricao)
    {
        string codigo = CodigoSeguro(descricao?.Codigo);
        if (descricao is null || codigo.Length == 0)
            return ["descricoes-de-erros.json: entrada com código ausente ou inválido."];
        return
        [
            .. ConferirRequisitos(descricao, codigo),
            .. ConferirSecao(codigo, "oQueAconteceu", descricao.OQueAconteceu),
            .. ConferirSecao(codigo, "comoResolver", descricao.ComoResolver),
            .. descricao.OndeAparece is null ? [] : ConferirSecao(codigo, "ondeAparece", descricao.OndeAparece),
        ];
    }

    private static IEnumerable<string> ConferirRequisitos(DescricaoDeErro descricao, string codigo)
    {
        if (descricao.Requisitos is null || descricao.Requisitos.Any(static requisito => requisito is null || !RegrasDoTextoDaDescricao.Requisito().IsMatch(requisito)))
            yield return $"{codigo}: requisitos devem usar UNI-REQ-NNNN.";
    }

    private static IEnumerable<string> ConferirSecao(string codigo, string secao, IReadOnlyList<string?>? blocos)
    {
        if (blocos is null or { Count: 0 })
            return [$"{codigo}: {secao} deve conter pelo menos um bloco."];
        return blocos.SelectMany(ConferirBloco).Select(regra => $"{codigo}: {secao}, {regra}.");
    }

    private static IEnumerable<string> ConferirBloco(string? bloco) =>
        string.IsNullOrWhiteSpace(bloco)
            ? ["bloco vazio"]
            : [
                .. RegrasDoTextoDaDescricao.Conferir(bloco),
                .. RegrasDoTextoDaDescricao.Conferir(MarkdownDaDescricao.TextoVisivel(bloco)),
                .. MarkdownDaDescricao.Conferir(bloco),
                .. MarkdownDaDescricao.TemTermoDeImplementacao(bloco) ? ["termo de implementação fora de código"] : Array.Empty<string>(),
            ];

    private static string CodigoSeguro(string? codigo) =>
        codigo is not null
        && RegrasDoTextoDaDescricao.CodigoPublico().IsMatch(codigo)
        && !RegrasDoTextoDaDescricao.Conferir(codigo).Any()
            ? codigo
            : string.Empty;
}
