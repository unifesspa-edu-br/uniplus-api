namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope os formulários de inscrição, com etapas e termos exigidos, e as regras de valor
/// (obrigatoriedade, impedimento e restrições) que o formulário e as exigências compartilham.
/// </summary>
internal static class SerializacaoDosFormularios
{
    internal static JsonArray SerializarFormularios(ProcessoSeletivo processo) =>
        new([.. processo.Formularios.OrderBy(static f => f.Finalidade).Select(formulario => (JsonNode)new JsonObject
        {
            ["finalidade"] = EstruturaFormulario.ParaToken(formulario.Finalidade),
            ["faseId"] = formulario.FaseId is { } fase ? JsonValue.Create(fase) : null,
            ["titulo"] = formulario.Titulo is { } titulo ? HashCanonicalComputer.NormalizeNfc(titulo) : null,
            ["modeloOrigem"] = formulario.ModeloOrigemId is { } modelo
                ? new JsonObject
                {
                    ["id"] = JsonValue.Create(modelo),
                    ["codigo"] = formulario.ModeloOrigemCodigo is { } codigo ? HashCanonicalComputer.NormalizeNfc(codigo) : null,
                }
                : null,
            ["etapas"] = new JsonArray([.. formulario.Etapas.OrderBy(static e => e.Ordem).Select(static e => (JsonNode)new JsonObject
            {
                ["codigo"] = HashCanonicalComputer.NormalizeNfc(e.Codigo),
                ["ordem"] = e.Ordem,
                ["tipo"] = EstruturaFormulario.ParaToken(e.Tipo),
                ["bloco"] = EstruturaFormulario.ParaToken(e.Bloco),
                ["titulo"] = HashCanonicalComputer.NormalizeNfc(e.Titulo),
                ["descricao"] = e.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                ["aviso"] = e.Aviso is { } aviso ? HashCanonicalComputer.NormalizeNfc(aviso) : null,
                ["exibicao"] = e.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null,
            })]),
            ["termos"] = new JsonArray([.. processo.TermosExigidos
                .Where(t => t.Finalidade == formulario.Finalidade)
                .OrderBy(static t => t.Ordem)
                .Select(SerializarTermoExigido)]),
        })]);

    private static JsonNode SerializarTermoExigido(TermoExigidoFormulario termo) => new JsonObject
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(termo.Codigo),
        ["ordem"] = termo.Ordem,
        ["termoId"] = JsonValue.Create(termo.TermoId),
        ["versaoId"] = JsonValue.Create(termo.VersaoId),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(termo.Nome),
        ["texto"] = HashCanonicalComputer.NormalizeNfc(termo.Texto),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(termo.BaseLegal),
        ["formaAceite"] = termo.FormaAceite,
        ["hashVersao"] = termo.HashVersao,
        ["exibicao"] = termo.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null,
        ["obrigatoriedade"] = SerializarObrigatoriedade(termo.Obrigatoriedade),
    };

    internal static JsonObject SerializarObrigatoriedade(Obrigatoriedade obrigatoriedade) => new()
    {
        ["tipo"] = PredicadoDnfJson.ParaToken(obrigatoriedade.Tipo),
        ["predicado"] = obrigatoriedade.Predicado is { } predicado ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(predicado)) : null,
    };

    internal static JsonObject SerializarImpedimento(Impedimento impedimento) => new()
    {
        ["quando"] = SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(impedimento.Quando)),
        ["mensagem"] = HashCanonicalComputer.NormalizeNfc(impedimento.Mensagem),
    };

    internal static JsonArray SerializarRestricoes(IReadOnlyList<RestricaoValor> restricoes) =>
        new([.. restricoes.OrderBy(static r => r.Tipo).Select(static r => (JsonNode)SerializarRestricao(r))]);

    private static JsonObject SerializarRestricao(RestricaoValor restricao)
    {
        JsonObject json = DetalheDaRestricao(restricao);
        json["tipo"] = RestricaoValorJson.ParaToken(restricao.Tipo);
        return json;
    }

    private static JsonObject DetalheDaRestricao(RestricaoValor restricao) => restricao switch
    {
        FaixaNumerica faixa => new JsonObject { ["minimo"] = LimiteDaFaixa(faixa.Minimo), ["maximo"] = LimiteDaFaixa(faixa.Maximo) },
        TamanhoTexto tamanho => new JsonObject { ["minimo"] = tamanho.Minimo, ["maximo"] = tamanho.Maximo },
        OpcoesPermitidas opcoes => new JsonObject { ["entradas"] = SerializarEntradasPermitidas(opcoes) },
        OpcoesDasRespostas respostas => new JsonObject { ["fatos"] = EscritaCanonica.CodigosEmOrdem(respostas.Fatos) },
        MunicipiosDaUf daUf => new JsonObject { ["fatos"] = EscritaCanonica.CodigosEmOrdem([daUf.FatoUf]) },
        FaixaDeData faixaDeData => new JsonObject
        {
            ["dataMinima"] = SerializarLimiteDeData(faixaDeData.Minima),
            ["dataMaxima"] = SerializarLimiteDeData(faixaDeData.Maxima),
        },
        _ => throw new InvalidOperationException($"Restrição de valor sem forma canônica: {restricao.Tipo}."),
    };

    private static JsonArray SerializarEntradasPermitidas(OpcoesPermitidas opcoes) =>
        EscritaCanonica.OrdenarPorConteudo(opcoes.Entradas.Select(static e => new JsonObject
        {
            ["quando"] = e.Quando is { } quando ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(quando)) : null,
            ["valores"] = EscritaCanonica.CodigosEmOrdem(e.Valores),
        }));

    private static JsonObject? SerializarLimiteDeData(LimiteDeData? limite) => limite switch
    {
        null => null,
        { Ancora: AncoraDoLimiteDeData.DataFixa } => new JsonObject
        {
            ["ancora"] = RestricaoValorJson.AncoraDataFixa,
            ["data"] = limite.Data!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        },
        _ => new JsonObject { ["ancora"] = RestricaoValorJson.AncoraEnvio, ["anos"] = limite.Anos, ["dias"] = limite.Dias },
    };

    private static string? LimiteDaFaixa(decimal? limite) =>
        limite is { } valor ? HashCanonicalComputer.SerializeDecimalCanonical(valor, FormaDoItem.CasasDecimaisDaFaixa) : null;
}
