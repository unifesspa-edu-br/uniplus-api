namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Registra na configuração publicada os formulários de inscrição, com etapas e termos exigidos, e as regras de valor
/// (obrigatoriedade, impedimento e restrições) que o formulário e as exigências compartilham.
/// </summary>
internal static class SerializacaoDosFormularios
{
    internal static JsonArray SerializarFormularios(ProcessoSeletivo processo) =>
        new([.. OrdemCanonicaDaConfiguracao.Formularios(processo.Formularios).Select(formulario => (JsonNode)new JsonObject
        {
            ["finalidade"] = formulario.Finalidade.Codigo(),
            ["faseId"] = formulario.FaseId is { } fase ? JsonValue.Create(fase) : null,
            ["titulo"] = formulario.Titulo is { } titulo ? HashCanonicalComputer.NormalizeNfc(titulo) : null,
            ["modeloOrigem"] = SerializarModeloDeOrigem(formulario),
            ["paginas"] = new JsonArray([.. formulario.Etapas.OrderBy(static e => e.Ordem).Select(SerializarEtapa)]),
            ["termos"] = new JsonArray([.. processo.TermosExigidos
                .Where(t => t.Finalidade == formulario.Finalidade)
                .OrderBy(static t => t.Ordem)
                .Select(SerializarTermoExigido)]),
        })]);

    private static JsonObject? SerializarModeloDeOrigem(FormularioProcesso formulario) =>
        formulario.ModeloOrigemId is { } modelo ? new JsonObject
        {
            ["id"] = JsonValue.Create(modelo),
            ["codigo"] = formulario.ModeloOrigemCodigo is { } codigo ? HashCanonicalComputer.NormalizeNfc(codigo) : null,
            ["nome"] = formulario.ModeloOrigemNome is { } nome ? HashCanonicalComputer.NormalizeNfc(nome) : null,
        } : null;

    private static JsonNode SerializarEtapa(EtapaFormulario etapa) => new JsonObject
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(etapa.Codigo),
        ["ordem"] = etapa.Ordem,
        ["tipo"] = etapa.Tipo.Codigo(),
        ["bloco"] = etapa.Bloco?.Codigo(),
        ["titulo"] = HashCanonicalComputer.NormalizeNfc(etapa.Titulo),
        ["descricao"] = etapa.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
        ["aviso"] = etapa.Aviso is { } aviso ? HashCanonicalComputer.NormalizeNfc(aviso) : null,
        ["exibicao"] = etapa.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null,
    };

    private static JsonNode SerializarTermoExigido(TermoExigidoFormulario termo) => new JsonObject
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(termo.Codigo),
        ["ordem"] = termo.Ordem,
        ["termoId"] = JsonValue.Create(termo.TermoId),
        ["versaoId"] = JsonValue.Create(termo.VersaoId),
        ["termoCodigo"] = HashCanonicalComputer.NormalizeNfc(termo.TermoCodigo),
        ["versaoNumero"] = termo.VersaoNumero,
        ["nome"] = HashCanonicalComputer.NormalizeNfc(termo.Nome),
        ["texto"] = HashCanonicalComputer.NormalizeNfc(termo.Texto),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(termo.BaseLegal),
        ["formaAceite"] = termo.FormaAceite.Codigo(),
        ["hashVersao"] = termo.HashVersao,
        ["exibicao"] = termo.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null,
        ["obrigatoriedade"] = SerializarObrigatoriedade(termo.Obrigatoriedade),
    };

    internal static JsonObject SerializarObrigatoriedade(Obrigatoriedade obrigatoriedade) => new()
    {
        ["tipo"] = obrigatoriedade.Tipo.Codigo(),
        ["quando"] = obrigatoriedade.Predicado is { } predicado ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(predicado)) : null,
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
        json["tipo"] = restricao.Tipo.Codigo();
        return json;
    }

    private static JsonObject DetalheDaRestricao(RestricaoValor restricao) => restricao switch
    {
        FaixaNumerica faixa => new JsonObject { ["minimo"] = LimiteDaFaixa(faixa.Minimo), ["maximo"] = LimiteDaFaixa(faixa.Maximo) },
        TamanhoTexto tamanho => new JsonObject { ["minimo"] = tamanho.Minimo, ["maximo"] = tamanho.Maximo },
        OpcoesPermitidas opcoes => new JsonObject { ["entradas"] = SerializarEntradasPermitidas(opcoes) },
        OpcoesDasRespostas respostas => new JsonObject { ["codigosDosDados"] = EscritaCanonica.CodigosEmOrdem(respostas.Fatos) },
        MunicipiosDaUf daUf => new JsonObject { ["codigosDosDados"] = EscritaCanonica.CodigosEmOrdem([daUf.FatoUf]) },
        FaixaDeData faixaDeData => new JsonObject
        {
            ["dataMinima"] = RestricaoValorJson.ParaJson(faixaDeData.Minima),
            ["dataMaxima"] = RestricaoValorJson.ParaJson(faixaDeData.Maxima),
        },
        _ => throw new InvalidOperationException($"Restrição de valor sem forma canônica: {restricao.Tipo}."),
    };

    private static JsonArray SerializarEntradasPermitidas(OpcoesPermitidas opcoes) =>
        EscritaCanonica.OrdenarPorConteudo(opcoes.Entradas.Select(static e => new JsonObject
        {
            ["quando"] = e.Quando is { } quando ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(quando)) : null,
            ["valores"] = EscritaCanonica.CodigosEmOrdem(e.Valores),
        }));

    private static string? LimiteDaFaixa(decimal? limite) =>
        limite is { } valor ? HashCanonicalComputer.SerializeDecimalCanonical(valor, FormaDoItem.CasasDecimaisDaFaixa) : null;
}
