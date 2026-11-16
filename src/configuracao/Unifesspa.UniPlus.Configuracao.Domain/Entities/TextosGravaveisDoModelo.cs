namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Os textos do conteúdo do modelo que o banco não grava: o conteúdo é um documento jsonb, e o Postgres só os recusaria na gravação.</summary>
internal static class TextosGravaveisDoModelo
{
    /// <summary>
    /// Os textos do conteúdo que o banco não grava — com caractere nulo, ou que não são Unicode
    /// válido —, inclusive os valores e os fatos citados pelas regras: o conteúdo é um documento
    /// jsonb, e o Postgres o recusaria só na gravação.
    /// </summary>
    internal static IEnumerable<FieldError> NaoGravaveis(ConteudoDoModelo conteudo)
    {
        IEnumerable<(string Campo, string? Texto)> textos = new (string, string?)[] { ("titulo", conteudo.Titulo) }
            .Concat(conteudo.Etapas.SelectMany(static (e, i) => new (string, string?)[]
                {
                    ($"etapas[{i}].codigo", e.Codigo), ($"etapas[{i}].titulo", e.Titulo),
                    ($"etapas[{i}].descricao", e.Descricao), ($"etapas[{i}].aviso", e.Aviso),
                }.Concat(TextosDoPredicado($"etapas[{i}].exibicao", e.Exibicao))))
            .Concat(conteudo.Itens.SelectMany(static (item, i) => TextosDoCampo($"itens[{i}]", item)))
            .Concat(conteudo.Grupos.SelectMany(static (g, i) => new (string, string?)[]
                {
                    ($"grupos[{i}].codigo", g.Codigo), ($"grupos[{i}].etapaCodigo", g.EtapaCodigo), ($"grupos[{i}].rotulo", g.Rotulo),
                }
                .Concat(TextosDoPredicado($"grupos[{i}].exibicao", g.Exibicao))
                .Concat(TextosDoPredicado($"grupos[{i}].obrigatoriedade", g.Obrigatoriedade.Predicado))
                .Concat(g.Subitens.SelectMany((s, j) => TextosDoCampo($"grupos[{i}].subitens[{j}]", s)))))
            .Concat(conteudo.Termos.SelectMany(static (t, i) => new (string, string?)[] { ($"termos[{i}].codigo", t.Codigo) }
                .Concat(TextosDoPredicado($"termos[{i}].exibicao", t.Exibicao))
                .Concat(TextosDoPredicado($"termos[{i}].obrigatoriedade", t.Obrigatoriedade.Predicado))))
            .Concat(conteudo.Documentos.SelectMany(static (d, i) => new (string, string?)[]
                {
                    ($"documentosExigidos[{i}].faseCodigo", d.FaseCodigo), ($"documentosExigidos[{i}].tipoDocumentoCodigo", d.TipoDocumentoCodigo),
                    ($"documentosExigidos[{i}].consequenciaIndeferimento", d.ConsequenciaIndeferimento),
                }
                .Concat(TextosDoPredicado($"documentosExigidos[{i}].quando", d.Quando))
                .Concat(d.PorOpcao is { } porOpcao
                    ? porOpcao.Exceto.Select((v, j) => ($"documentosExigidos[{i}].porOpcao.exceto[{j}]", (string?)v))
                        .Prepend(($"documentosExigidos[{i}].porOpcao.fatoCodigo", porOpcao.FatoCodigo))
                    : [])
                .Concat(d.BasesLegais.SelectMany((b, j) => new (string, string?)[]
                {
                    ($"documentosExigidos[{i}].basesLegais[{j}].referencia", b.Referencia), ($"documentosExigidos[{i}].basesLegais[{j}].abrangencia", b.Abrangencia),
                    ($"documentosExigidos[{i}].basesLegais[{j}].status", b.Status), ($"documentosExigidos[{i}].basesLegais[{j}].observacao", b.Observacao),
                }))))
            .Concat(conteudo.Pressupostos.Select(static (p, i) => ($"pressupostos[{i}]", (string?)p)));
        return textos.SelectMany(static t => NaoGravavel(t.Campo, t.Texto)).DistinctBy(static e => e.Field);
    }

    private static IEnumerable<(string Campo, string? Texto)> TextosDoCampo(string caminho, ItemDoModelo campo) =>
        new (string, string?)[]
        {
            ($"{caminho}.fatoCodigo", campo.FatoCodigo), ($"{caminho}.etapaCodigo", campo.EtapaCodigo),
            ($"{caminho}.rotulo", campo.Rotulo), ($"{caminho}.formato", campo.Formato), ($"{caminho}.ajuda", campo.Ajuda),
        }
        .Concat(TextosDoPredicado($"{caminho}.exibicao", campo.Exibicao))
        .Concat(TextosDoPredicado($"{caminho}.obrigatoriedade", campo.Obrigatoriedade.Predicado))
        .Concat(campo.Restricoes.SelectMany((r, j) => TextosDaRestricao($"{caminho}.restricoes[{j}]", r)))
        .Concat(campo.Impedimento is { } impedimento
            ? TextosDoPredicado($"{caminho}.impedimento.quando", impedimento.Quando).Append(($"{caminho}.impedimento.mensagem", impedimento.Mensagem))
            : []);

    private static IEnumerable<(string Campo, string? Texto)> TextosDaRestricao(string campo, RestricaoValor restricao) => restricao switch
    {
        OpcoesPermitidas opcoes => opcoes.Entradas.SelectMany((e, k) => e.Valores.Select(v => ($"{campo}.entradas[{k}].valores", (string?)v))
            .Concat(TextosDoPredicado($"{campo}.entradas[{k}].quando", e.Quando))),
        OpcoesDasRespostas respostas => respostas.Fatos.Select(f => ($"{campo}.fatos", (string?)f)),
        MunicipiosDaUf daUf => [($"{campo}.fatos", daUf.FatoUf)],
        _ => [],
    };

    private static IEnumerable<(string Campo, string? Texto)> TextosDoPredicado(string campo, PredicadoDnf? predicado) =>
        (predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes)
            .SelectMany(c => new[] { (campo, (string?)c.Fato) }.Concat(TextosDoValor(c.Valor).Select(v => (campo, (string?)v))));

    private static IEnumerable<string> TextosDoValor(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => [valor.GetString()!],
        JsonValueKind.Array => valor.EnumerateArray().SelectMany(TextosDoValor),
        JsonValueKind.Object => valor.EnumerateObject().SelectMany(static p => TextosDoValor(p.Value).Prepend(p.Name)),
        _ => [],
    };

    internal static bool EhGravavel(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return !texto.Contains('\0', StringComparison.Ordinal) && TextoNormalizavel.TentarNormalizar(texto, out _);
    }

    internal static IEnumerable<FieldError> NaoGravavel(string campo, string? texto)
    {
        if (texto is not null && !EhGravavel(texto))
        {
            yield return new(campo, new DomainError(
                ModeloFormularioErrorCodes.TextoNaoGravavel, "O texto contém o caractere nulo (U+0000) ou não é Unicode válido."));
        }
    }
}
