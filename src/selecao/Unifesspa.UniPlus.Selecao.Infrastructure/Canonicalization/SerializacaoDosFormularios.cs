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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Ordenacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Registra na configuração publicada os formulários do processo, cada um pelo seu código, com páginas e termos exigidos, e as
/// regras de valor (obrigatoriedade, impedimento e restrições) que o formulário e as exigências compartilham.
/// </summary>
internal static class SerializacaoDosFormularios
{
    internal static JsonArray SerializarFormularios(ProcessoSeletivo processo) =>
        new([.. OrdemDaConfiguracao.Formularios(processo.Formularios).Select(formulario => (JsonNode)SerializarFormulario(processo, formulario))]);

    private static JsonObject SerializarFormulario(ProcessoSeletivo processo, FormularioProcesso formulario)
    {
        JsonObject objeto = DadosDoFormulario(processo, formulario);
        ContextoDosRotulos.Acrescentar(objeto, "consequenciaDaFaltaDeEnvio", formulario.ConsequenciaDaFaltaDeEnvio);
        return objeto;
    }

    private static JsonObject DadosDoFormulario(ProcessoSeletivo processo, FormularioProcesso formulario) => new()
    {
        ["codigo"] = formulario.Codigo.Valor,
        ["faseId"] = formulario.FaseId is { } fase ? JsonValue.Create(fase) : null,
        ["obrigatorio"] = formulario.Obrigatorio,
        ["titulo"] = formulario.Titulo is { } titulo ? HashCanonicalComputer.NormalizeNfc(titulo) : null,
        ["consequenciaDaFaltaDeEnvio"] = formulario.ConsequenciaDaFaltaDeEnvio?.Codigo(),
        ["modeloOrigem"] = SerializarModeloDeOrigem(formulario),
        ["paginas"] = new JsonArray([.. formulario.Paginas.OrderBy(static e => e.Ordem).Select(SerializarPagina)]),
        ["termos"] = new JsonArray([.. processo.TermosExigidos
            .Where(t => t.FormularioId == formulario.Id)
            .OrderBy(static t => t.Ordem)
            .Select(SerializarTermoExigido)]),
    };

    /// <summary>
    /// Os códigos dos formulários do processo, pelo identificador: é por ele que cada campo, grupo e termo se liga ao
    /// formulário, e o documento o cita pelo código.
    /// </summary>
    internal static IReadOnlyDictionary<Guid, string> CodigosPorIdentificador(IEnumerable<FormularioProcesso> formularios) =>
        formularios.ToDictionary(static formulario => formulario.Id, static formulario => formulario.Codigo.Valor);

    /// <exception cref="InvalidOperationException">O item não pertence a nenhum dos formulários do processo.</exception>
    internal static string CodigoDoFormularioDoItem(Guid? formularioId, IReadOnlyDictionary<Guid, string> codigos) =>
        formularioId is { } id && codigos.TryGetValue(id, out string? codigo)
            ? codigo
            : throw new InvalidOperationException(
                "Canonicalização de item que não pertence a nenhum formulário do processo — a verificação de publicação "
                + "deveria tê-lo recusado antes deste ponto.");

    private static JsonObject? SerializarModeloDeOrigem(FormularioProcesso formulario) =>
        formulario.ModeloDeOrigem is { } modelo ? new JsonObject
        {
            ["id"] = JsonValue.Create(modelo.Id),
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(modelo.Codigo),
            ["nome"] = HashCanonicalComputer.NormalizeNfc(modelo.Nome),
        } : null;

    private static JsonNode SerializarPagina(PaginaDoFormulario pagina)
    {
        JsonObject objeto = SerializarPaginaSemContexto(pagina);
        ContextoDosRotulos.Acrescentar(objeto, "tipo", pagina.Tipo);
        ContextoDosRotulos.Acrescentar(objeto, "bloco", pagina.Bloco);
        return objeto;
    }

    private static JsonObject SerializarPaginaSemContexto(PaginaDoFormulario pagina) => new()
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(pagina.Codigo),
        ["ordem"] = pagina.Ordem,
        ["tipo"] = pagina.Tipo.Codigo(),
        ["bloco"] = pagina.Bloco?.Codigo(),
        ["titulo"] = HashCanonicalComputer.NormalizeNfc(pagina.Titulo),
        ["descricao"] = pagina.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
        ["aviso"] = pagina.Aviso is { } aviso ? HashCanonicalComputer.NormalizeNfc(aviso) : null,
        ["exibicao"] = pagina.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null,
    };

    private static JsonNode SerializarTermoExigido(TermoExigidoFormulario termo)
    {
        JsonObject objeto = DadosDoTermo(termo);
        ContextoDosRotulos.Acrescentar(objeto, "formaAceite", (FormaAceite?)termo.FormaAceite);
        return objeto;
    }

    private static JsonObject DadosDoTermo(TermoExigidoFormulario termo)
    {
        JsonObject objeto = IdentificacaoDoTermo(termo);
        objeto["hashVersao"] = termo.HashVersao;
        objeto["exibicao"] = termo.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null;
        objeto["obrigatoriedade"] = SerializarObrigatoriedade(termo.Obrigatoriedade);
        return objeto;
    }

    private static JsonObject IdentificacaoDoTermo(TermoExigidoFormulario termo) => new()
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
    };

    internal static JsonObject SerializarObrigatoriedade(Obrigatoriedade obrigatoriedade)
    {
        JsonObject objeto = new()
        {
            ["tipo"] = obrigatoriedade.Tipo.Codigo(),
            ["quando"] = obrigatoriedade.Predicado is { } predicado ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(predicado)) : null,
        };
        ContextoDosRotulos.Acrescentar(objeto, "tipo", obrigatoriedade.Tipo);
        return objeto;
    }

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
        ContextoDosRotulos.Acrescentar(json, "tipo", restricao.Tipo);
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
