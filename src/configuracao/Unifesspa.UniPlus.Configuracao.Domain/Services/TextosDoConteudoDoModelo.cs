namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Os textos de cada parte do conteúdo do modelo que vão para o banco, com o campo de cada um no caminho da
/// escrita: o conteúdo é um documento jsonb, e o que o Postgres recusaria é conferido por <see cref="TextoGravavel"/>.
/// Cada função recebe só os valores da parte, para servir à parte lida e à parte que a leitura recusou. Os textos
/// dentro de condição e de valor de regra entram junto com os fatos que citam.
/// </summary>
public static class TextosDoConteudoDoModelo
{
    /// <summary>Os textos de uma página: o título, a descrição, o aviso e os da condição de exibição.</summary>
    public static IEnumerable<(string Campo, string? Texto)> DaPagina(
        string caminho, string? titulo, string? descricao, string? aviso, PredicadoDnf? exibicao)
    {
        string apresentacao = $"{caminho}.{PaginaDoFormularioInput.CaminhoDaApresentacao}";
        return new (string, string?)[]
        {
            ($"{apresentacao}.titulo", titulo), ($"{apresentacao}.descricao", descricao), ($"{apresentacao}.aviso", aviso),
        }
        .Concat(DoPredicado($"{caminho}.exibicao", exibicao));
    }

    /// <summary>
    /// Os textos de um campo, item ou campo de grupo: os códigos, a apresentação, o formato e os das regras —
    /// exibição, obrigatoriedade, restrições e impedimento. As regras recusadas na leitura chegam nulas e não têm texto.
    /// </summary>
    public static IEnumerable<(string Campo, string? Texto)> DoCampo(
        string caminho,
        string? fatoCodigo,
        string? paginaCodigo,
        string? rotulo,
        string? ajuda,
        string? formato,
        RegrasLidasDoCampo regras)
    {
        ArgumentNullException.ThrowIfNull(regras);

        string apresentacao = $"{caminho}.{FatoColetadoInput.CaminhoDaApresentacao}";
        string caminhoDasRegras = $"{caminho}.{FatoColetadoInput.CaminhoDasRegras}";
        return new (string, string?)[]
        {
            ($"{caminho}.fatoCodigo", fatoCodigo), ($"{caminho}.paginaCodigo", paginaCodigo),
            ($"{apresentacao}.rotulo", rotulo), ($"{caminho}.formato", formato), ($"{apresentacao}.ajuda", ajuda),
        }
        .Concat(DasRegrasDoCampo(caminhoDasRegras, regras.Exibicao, regras.Obrigatoriedade?.Predicado, regras.Restricoes, regras.Impedimento));
    }

    /// <summary>Os textos de um grupo repetível, sem os campos dele: a página, o rótulo e os da exibição e da obrigatoriedade.</summary>
    public static IEnumerable<(string Campo, string? Texto)> DoGrupo(
        string caminho, string? paginaCodigo, string? rotulo, PredicadoDnf? exibicao, PredicadoDnf? obrigatoriedade) =>
        new (string, string?)[] { ($"{caminho}.paginaCodigo", paginaCodigo), ($"{caminho}.rotulo", rotulo) }
            .Concat(DasRegrasDeExibicaoEObrigatoriedade($"{caminho}.{GrupoColetadoInput.CaminhoDasRegras}", exibicao, obrigatoriedade));

    /// <summary>Os textos de um termo: os da exibição e os da obrigatoriedade.</summary>
    public static IEnumerable<(string Campo, string? Texto)> DoTermo(string caminho, PredicadoDnf? exibicao, PredicadoDnf? obrigatoriedade) =>
        DasRegrasDeExibicaoEObrigatoriedade($"{caminho}.{TermoExigidoInput.CaminhoDasRegras}", exibicao, obrigatoriedade);

    /// <summary>
    /// Os textos de um documento exigido: a fase, o tipo, a condição e os das partes que o documento lido e o recusado na
    /// leitura montam cada um a seu modo — as referências dos requisitos, as opções (<see cref="DaExigenciaPorOpcao"/>)
    /// e o indeferimento (<see cref="DoIndeferimento"/>).
    /// </summary>
    public static IEnumerable<(string Campo, string? Texto)> DoDocumento(
        string caminho,
        string? faseCodigo,
        string? tipoDocumentoCodigo,
        PredicadoDnf? quando,
        IEnumerable<(string Campo, string? Texto)> referencias,
        IEnumerable<(string Campo, string? Texto)> opcoes,
        IEnumerable<(string Campo, string? Texto)> indeferimento)
    {
        ArgumentNullException.ThrowIfNull(referencias);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(indeferimento);

        return new (string, string?)[] { ($"{caminho}.faseCodigo", faseCodigo), ($"{caminho}.tipoDocumentoCodigo", tipoDocumentoCodigo) }
            .Concat(DoPredicado($"{caminho}.quando", quando))
            .Concat(referencias)
            .Concat(opcoes)
            .Concat(indeferimento);
    }

    /// <summary>Os textos de um documento exigido lido, com o das referências, o das opções e o do indeferimento dele.</summary>
    public static IEnumerable<(string Campo, string? Texto)> DoDocumentoLido(string caminho, DocumentoExigidoDoModelo documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return DoDocumento(
            caminho,
            documento.FaseCodigo,
            documento.TipoDocumentoCodigo,
            documento.Quando,
            documento.Requisitos.TextosDasReferencias(caminho),
            documento.PorOpcao is { } porOpcao ? DaExigenciaPorOpcao(caminho, porOpcao.FatoCodigo, porOpcao.Exceto) : [],
            DoIndeferimento(caminho, documento.Indeferimento.ConsequenciaDoDescumprimento, documento.Indeferimento.BasesLegais));
    }

    /// <summary>
    /// Os textos da exigência por opção de um documento, na posição de cada opção excetuada: a posição que veio, também
    /// quando a opção ao lado é nula.
    /// </summary>
    /// <param name="caminho">O caminho do documento.</param>
    public static IEnumerable<(string Campo, string? Texto)> DaExigenciaPorOpcao(string caminho, string? fatoCodigo, IReadOnlyList<string?> exceto)
    {
        ArgumentNullException.ThrowIfNull(exceto);

        return exceto.Select((valor, j) => ($"{caminho}.porOpcao.exceto[{j}]", valor)).Prepend(($"{caminho}.porOpcao.fatoCodigo", fatoCodigo));
    }

    /// <summary>
    /// Os textos do indeferimento de um documento ou de um grupo de documentos: a consequência e as bases legais, na posição
    /// de cada uma; a base nula não tem texto e mantém a posição.
    /// </summary>
    /// <param name="caminho">O caminho do documento ou do grupo.</param>
    public static IEnumerable<(string Campo, string? Texto)> DoIndeferimento(
        string caminho, string? consequenciaDoDescumprimento, IReadOnlyList<BaseLegalDoModelo?> basesLegais)
    {
        ArgumentNullException.ThrowIfNull(basesLegais);

        return DasBasesLegais($"{caminho}.indeferimento.basesLegais", basesLegais)
            .Prepend(($"{caminho}.indeferimento.consequenciaDoDescumprimento", consequenciaDoDescumprimento));
    }

    private static IEnumerable<(string Campo, string? Texto)> DasBasesLegais(string caminho, IReadOnlyList<BaseLegalDoModelo?> basesLegais) =>
        basesLegais.SelectMany((baseLegal, j) => baseLegal is null
            ? []
            : new (string, string?)[]
            {
                ($"{caminho}[{j}].referencia", baseLegal.Referencia), ($"{caminho}[{j}].abrangencia", baseLegal.Abrangencia),
                ($"{caminho}[{j}].situacao", baseLegal.Situacao), ($"{caminho}[{j}].observacao", baseLegal.Observacao),
            });

    private static IEnumerable<(string Campo, string? Texto)> DasRegrasDeExibicaoEObrigatoriedade(
        string caminho, PredicadoDnf? exibicao, PredicadoDnf? obrigatoriedade) =>
        DoPredicado($"{caminho}.exibicao", exibicao).Concat(DoPredicado($"{caminho}.obrigatoriedade.predicado", obrigatoriedade));

    private static IEnumerable<(string Campo, string? Texto)> DasRegrasDoCampo(
        string caminho,
        PredicadoDnf? exibicao,
        PredicadoDnf? obrigatoriedade,
        IReadOnlyList<RestricaoValor?> restricoes,
        Impedimento? impedimento) =>
        DasRegrasDeExibicaoEObrigatoriedade(caminho, exibicao, obrigatoriedade)
            .Concat(restricoes.SelectMany((restricao, j) => DaRestricao($"{caminho}.restricoes[{j}]", restricao)))
            .Concat(DoImpedimento(caminho, impedimento));

    private static IEnumerable<(string Campo, string? Texto)> DoImpedimento(string caminho, Impedimento? impedimento) =>
        impedimento is null
            ? []
            : DoPredicado($"{caminho}.impedimento.quando", impedimento.Quando).Append(($"{caminho}.impedimento.mensagem", impedimento.Mensagem));

    private static IEnumerable<(string Campo, string? Texto)> DaRestricao(string campo, RestricaoValor? restricao) => restricao switch
    {
        OpcoesPermitidas opcoes => opcoes.Entradas.SelectMany((e, k) => e.Valores.Select(v => ($"{campo}.entradas[{k}].valores", (string?)v))
            .Concat(DoPredicado($"{campo}.entradas[{k}].quando", e.Quando))),
        OpcoesDasRespostas respostas => respostas.Fatos.Select(f => ($"{campo}.fatos", (string?)f)),
        MunicipiosDaUf daUf => [($"{campo}.fatos", daUf.FatoUf)],
        _ => [],
    };

    private static IEnumerable<(string Campo, string? Texto)> DoPredicado(string campo, PredicadoDnf? predicado) =>
        (predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes)
            .SelectMany(c => new[] { (campo, (string?)c.Fato) }.Concat(DoValor(c.Valor).Select(v => (campo, (string?)v))));

    private static IEnumerable<string> DoValor(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => [valor.GetString()!],
        JsonValueKind.Array => valor.EnumerateArray().SelectMany(DoValor),
        JsonValueKind.Object => valor.EnumerateObject().SelectMany(static p => DoValor(p.Value).Prepend(p.Name)),
        _ => [],
    };
}
