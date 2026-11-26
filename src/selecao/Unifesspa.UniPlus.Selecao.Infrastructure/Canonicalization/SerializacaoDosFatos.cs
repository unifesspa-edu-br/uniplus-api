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
/// Projeta no envelope os fatos coletados e derivados, os grupos de coleta, as regras de derivação e o grafo de dependência.
/// </summary>
internal static class SerializacaoDosFatos
{
    internal static JsonArray SerializarFatosDerivados(IReadOnlyDictionary<string, FatoDerivadoCongelado>? fatos) =>
        new([.. (fatos ?? new Dictionary<string, FatoDerivadoCongelado>(StringComparer.Ordinal)).Values
            .OrderBy(static f => HashCanonicalComputer.NormalizeNfc(f.Codigo), StringComparer.Ordinal)
            .Select(static f => (JsonNode?)SerializarFatoDerivado(f))]);

    private static JsonObject SerializarFatoDerivado(FatoDerivadoCongelado f)
    {
        JsonObject objeto = DadosDoFatoDerivado(f);
        ContextoDosRotulos.Acrescentar(objeto, "tipo", (TipoDoDado?)f.Dominio);
        ContextoDosRotulos.Acrescentar(objeto, "quantidadeDeValores", (QuantidadeDeValores?)f.Cardinalidade);
        return objeto;
    }

    private static JsonObject DadosDoFatoDerivado(FatoDerivadoCongelado f) => new()
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(f.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(f.Nome),
        ["descricao"] = HashCanonicalComputer.NormalizeNfc(f.Descricao),
        ["componenteExibicao"] = f.ComponenteExibicao,
        ["tipo"] = f.Dominio.Codigo(),
        ["quantidadeDeValores"] = f.Cardinalidade.Codigo(),
        ["classificacaoProtecao"] = f.ClassificacaoProtecao,
        ["hipoteseLegal"] = f.HipoteseLegal,
        ["finalidadeTratamento"] = HashCanonicalComputer.NormalizeNfc(f.FinalidadeTratamento),
    };

    internal static JsonArray SerializarMetadadosFatos(IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados)
    {
        if (metadadosFatosCongelados is null || metadadosFatosCongelados.Count == 0)
        {
            return [];
        }

        return new JsonArray([.. metadadosFatosCongelados.Values
            .OrderBy(static m => m.Codigo, StringComparer.Ordinal)
            .Select(static metadado => (JsonNode)SerializarMetadadoDoFato(metadado))]);
    }

    private static JsonObject SerializarMetadadoDoFato(MetadadoFatoCongelado metadado)
    {
        JsonObject objeto = DadosDoMetadado(metadado);
        ContextoDosRotulos.Acrescentar(objeto, "tipo", (TipoDoDado?)metadado.Dominio);
        ContextoDosRotulos.Acrescentar(objeto, "origem", (OrigemFato?)metadado.Origem);
        ContextoDosRotulos.Acrescentar(objeto, "quantidadeDeValores", (QuantidadeDeValores?)metadado.Cardinalidade);
        return objeto;
    }

    private static JsonObject DadosDoMetadado(MetadadoFatoCongelado metadado) => new()
    {
        ["codigoDoDado"] = HashCanonicalComputer.NormalizeNfc(metadado.Codigo),
        ["tipo"] = metadado.Dominio.Codigo(),
        ["origem"] = metadado.Origem.Codigo(),
        ["quantidadeDeValores"] = metadado.Cardinalidade.Codigo(),
        ["faseDeDisponibilidade"] = HashCanonicalComputer.NormalizeNfc(metadado.PontoResolucao),
        ["fonteDoValor"] = HashCanonicalComputer.NormalizeNfc(metadado.Binding),
        ["opcoes"] = metadado.ValoresDominio is { } valores ? EscritaCanonica.TextosEmOrdemCanonica(valores) : null,
        ["opcoesDeclaradas"] = metadado.ValoresDominioDeclarados is { } declarados ? SerializarValoresDominioDeclarados(declarados) : null,
    };

    private static JsonArray SerializarValoresDominioDeclarados(IEnumerable<ValorDominioDeclaradoCongelado> declarados) =>
        new([.. declarados
            .OrderBy(static d => d.Ordem)
            .ThenBy(static d => d.Codigo, StringComparer.Ordinal)
            .Select(static d => (JsonNode)new JsonObject
            {
                ["valorCodigo"] = HashCanonicalComputer.NormalizeNfc(d.Codigo),
                ["descricao"] = d.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                ["ordem"] = d.Ordem,
            })]);

    internal static JsonArray SerializarFatosColetados(
        IEnumerable<FatoColetado> fatos,
        IReadOnlyDictionary<Guid, string> codigosDosFormularios,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados)
    {
        JsonArray array = [];
        foreach (FatoColetado fato in OrdemCanonicaDaConfiguracao.Campos(fatos))
        {
            array.Add(SerializarFatoColetado(fato, codigosDosFormularios, ValoresSelecionaveisDoFato(fato, valoresSelecionaveisCongelados)));
        }

        return array;
    }

    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? ValoresSelecionaveisDoFato(
        FatoColetado fato,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados)
    {
        bool ehFatoDeSelecao = fato.TipoDeCampo.EhEscolha();
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato = null;
        bool temEntrada = valoresSelecionaveisCongelados?.TryGetValue(fato.FatoCodigo, out valoresDoFato) ?? false;

        if (DefeitoDoDicionarioDeValores(ehFatoDeSelecao, temEntrada, valoresDoFato) is { } defeito)
        {
            throw ErroNosValoresSelecionaveis(fato, ehFatoDeSelecao, defeito);
        }

        return valoresDoFato;
    }

    private static InvalidOperationException ErroNosValoresSelecionaveis(FatoColetado fato, bool ehFatoDeSelecao, string defeito) =>
        new($"O fato coletado '{fato.FatoCodigo}' {(ehFatoDeSelecao ? "é" : "não é")} de seleção " +
            $"({fato.TipoDeCampo}), mas o dicionário de valores selecionáveis {defeito}");

    private static string? DefeitoDoDicionarioDeValores(
        bool ehFatoDeSelecao,
        bool temEntrada,
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato) => (ehFatoDeSelecao, temEntrada, valoresDoFato) switch
        {
            (true, false, _) or (true, true, null) =>
                "recebido pelo canonicalizador não tem entrada para ele — a ausência é erro de programação do " +
                "handler, nunca lista vazia por omissão.",
            (true, _, { Count: 0 }) =>
                "traz uma lista VAZIA para ele — o canonicalizador nunca serializa um seletor sem opção nenhuma.",
            (false, true, not null) =>
                "traz um array para ele — a bicondicional exige null.",
            _ => null,
        };

    private static JsonObject SerializarFatoColetado(
        FatoColetado fato,
        IReadOnlyDictionary<Guid, string> codigosDosFormularios,
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato)
    {
        JsonObject objeto = IdentificacaoDoFato(fato, codigosDosFormularios);
        AdicionarRegrasDaResposta(objeto, fato);
        AdicionarTratamentoDoDado(objeto, fato);
        AdicionarApresentacaoEOpcoes(objeto, fato, valoresDoFato);
        ContextoDosRotulos.Acrescentar(objeto, "tipoDeCampo", (TipoDeCampo?)fato.TipoDeCampo);
        return objeto;
    }

    private static JsonObject IdentificacaoDoFato(FatoColetado fato, IReadOnlyDictionary<Guid, string> codigosDosFormularios) => new()
    {
        ["codigoDoDado"] = HashCanonicalComputer.NormalizeNfc(fato.FatoCodigo),
        ["formularioCodigo"] = SerializacaoDosFormularios.CodigoDoFormularioDoItem(fato.FormularioId, codigosDosFormularios),
        ["paginaCodigo"] = fato.PaginaCodigo is { } pagina ? HashCanonicalComputer.NormalizeNfc(pagina) : null,
        ["ordem"] = fato.Ordem,
        ["rotulo"] = HashCanonicalComputer.NormalizeNfc(fato.Rotulo),
        ["tipoDeCampo"] = fato.TipoDeCampo.Codigo(),
    };

    private static void AdicionarRegrasDaResposta(JsonObject objeto, FatoColetado fato)
    {
        objeto["obrigatoriedade"] = SerializacaoDosFormularios.SerializarObrigatoriedade(fato.Obrigatoriedade);
        objeto["ajuda"] = fato.Ajuda is { } ajuda ? HashCanonicalComputer.NormalizeNfc(ajuda) : null;
        objeto["pedirConfirmacao"] = fato.PedirConfirmacao;
        objeto["restricoes"] = SerializacaoDosFormularios.SerializarRestricoes(fato.Restricoes);
        objeto["impedimento"] = fato.Impedimento is { } impedimento ? SerializacaoDosFormularios.SerializarImpedimento(impedimento) : null;
    }

    private static void AdicionarTratamentoDoDado(JsonObject objeto, FatoColetado fato)
    {
        objeto["origemValores"] = fato.OrigemValores.Codigo();
        ContextoDosRotulos.Acrescentar(objeto, "origemValores", (OrigemValoresColeta?)fato.OrigemValores);
        objeto["formato"] = fato.Formato;
        objeto["classificacaoProtecao"] = fato.ClassificacaoProtecao;
        objeto["hipoteseLegal"] = fato.HipoteseLegal;
        objeto["finalidadeTratamento"] = HashCanonicalComputer.NormalizeNfc(fato.FinalidadeTratamento);
    }

    private static void AdicionarApresentacaoEOpcoes(
        JsonObject objeto, FatoColetado fato, IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato)
    {
        objeto["descricao"] = HashCanonicalComputer.NormalizeNfc(fato.Descricao);
        objeto["componenteExibicao"] = fato.ComponenteExibicao;
        objeto["exibicao"] = SerializacaoDosPredicados.SerializarDnf(
            fato.Precondicoes.Select(static c => (c.Clausula, c.Fato, c.Operador, c.Valor)));
        objeto["valoresSelecionaveis"] = valoresDoFato is null ? null : SerializarValoresSelecionaveis(valoresDoFato);
    }

    private static JsonArray SerializarValoresSelecionaveis(IReadOnlyList<ValorDominioDeclaradoCongelado> valores) =>
        new([.. valores
            .OrderBy(static v => v.Ordem)
            .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
            .Select(static v => (JsonNode)new JsonObject
            {
                ["valorCodigo"] = HashCanonicalComputer.NormalizeNfc(v.Codigo),
                ["descricao"] = v.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                ["ordem"] = v.Ordem,
                ["orientacao"] = v.Orientacao is { } orientacao ? HashCanonicalComputer.NormalizeNfc(orientacao) : null,
            })]);

    internal static JsonArray SerializarGruposColetados(
        IEnumerable<GrupoColetado> grupos,
        IReadOnlyDictionary<Guid, string> codigosDosFormularios,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados,
        IReadOnlyList<DefinicaoAgregado>? agregados) =>
        [.. OrdemCanonicaDaConfiguracao.Grupos(grupos).Select(g => SerializarGrupo(g, codigosDosFormularios, valoresSelecionaveisCongelados, agregados))];

    private static JsonObject SerializarGrupo(
        GrupoColetado g,
        IReadOnlyDictionary<Guid, string> codigosDosFormularios,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados,
        IReadOnlyList<DefinicaoAgregado>? agregados)
    {
        JsonObject objeto = IdentificacaoDoGrupo(g, codigosDosFormularios);
        AdicionarRegrasEComposicaoDoGrupo(objeto, g, codigosDosFormularios, valoresSelecionaveisCongelados, agregados);
        return objeto;
    }

    private static JsonObject IdentificacaoDoGrupo(GrupoColetado g, IReadOnlyDictionary<Guid, string> codigosDosFormularios) => new()
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(g.Codigo),
        ["formularioCodigo"] = SerializacaoDosFormularios.CodigoDoFormularioDoItem(g.FormularioId, codigosDosFormularios),
        ["paginaCodigo"] = g.PaginaCodigo is { } pagina ? HashCanonicalComputer.NormalizeNfc(pagina) : null,
        ["ordem"] = g.Ordem,
        ["rotulo"] = HashCanonicalComputer.NormalizeNfc(g.Rotulo),
        ["minimo"] = g.Minimo,
        ["maximo"] = g.Maximo,
        ["incluiCandidato"] = g.IncluiCandidato,
    };

    private static void AdicionarRegrasEComposicaoDoGrupo(
        JsonObject objeto,
        GrupoColetado g,
        IReadOnlyDictionary<Guid, string> codigosDosFormularios,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados,
        IReadOnlyList<DefinicaoAgregado>? agregados)
    {
        objeto["exibicao"] = g.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null;
        objeto["obrigatoriedade"] = SerializacaoDosFormularios.SerializarObrigatoriedade(g.Obrigatoriedade);
        objeto["subitens"] = SerializarFatosColetados(g.Subitens, codigosDosFormularios, valoresSelecionaveisCongelados);
        objeto["agregados"] = SerializarAgregadosDoGrupo(g, agregados);
    }

    private static JsonArray SerializarAgregadosDoGrupo(GrupoColetado g, IReadOnlyList<DefinicaoAgregado>? agregados) =>
        new([.. (agregados ?? [])
            .Where(a => string.Equals(a.GrupoCodigo, g.Codigo, StringComparison.Ordinal))
            .OrderBy(static a => a.Codigo, StringComparer.Ordinal)
            .Select(static a => SerializarAgregado(a))]);

    private static JsonObject SerializarAgregado(DefinicaoAgregado agregado)
    {
        JsonObject objeto = new()
        {
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(agregado.Codigo),
            ["codigoDoDadoDoMembro"] = HashCanonicalComputer.NormalizeNfc(agregado.FatoDeMembro),
            ["operacao"] = agregado.Operacao.Codigo(),
        };
        ContextoDosRotulos.Acrescentar(objeto, "operacao", agregado.Operacao);
        return objeto;
    }

    internal static JsonArray SerializarRegrasDerivacao(IEnumerable<ConfiguracaoDerivacaoFato> regrasDerivacao) =>
        [.. regrasDerivacao
            .OrderBy(static c => c.CodigoFato, StringComparer.Ordinal)
            .Select(static c => (JsonNode)new JsonObject
            {
                ["codigoDoDado"] = HashCanonicalComputer.NormalizeNfc(c.CodigoFato),
                ["regras"] = new JsonArray([.. c.Regras.OrderBy(static r => r.Ordem).Select(SerializarRegraDeDerivacao)]),
            })];

    private static JsonNode SerializarRegraDeDerivacao(RegraDerivacaoConfigurada regra) => new JsonObject
    {
        ["ordem"] = regra.Ordem,
        ["contribui"] = regra.Contribui is { } contribui ? HashCanonicalComputer.NormalizeNfc(contribui) : null,
        ["quando"] = SerializacaoDosPredicados.SerializarDnf(regra.Condicoes.Select(static c => (c.Clausula, c.Fato, c.Operador, c.Valor))),
    };

    internal static JsonObject SerializarGrafoDependencia(ProcessoSeletivo processo)
    {
        Result<GrafoDependenciaConjunta> grafo = Domain.ProcessosSeletivos.Formularios.CitacoesDosFormularios.ConstruirGrafoDependencia(processo.Declaracao());
        if (grafo.IsFailure)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo cujo grafo de dependência conjunto forma um ciclo — a verificação de "
                + $"publicação deveria tê-lo recusado antes deste ponto: {grafo.Error!.Message}");
        }

        return SerializarGrafoDependencia(grafo.Value!);
    }

    internal static JsonObject SerializarGrafoDependencia(GrafoDependenciaConjunta grafo) => new()
    {
        ["itens"] = new JsonArray([.. grafo.Partes.Select(no => (JsonNode)new JsonObject
        {
            ["identificador"] = no.Identidade,
        })]),
        ["ligacoes"] = new JsonArray([.. grafo.Dependencias.Select(aresta => (JsonNode)SerializarDependencia(aresta))]),
        ["ordemDeDependencia"] = new JsonArray(
            [.. grafo.OrdemDeColeta.Select(no => (JsonNode?)JsonValue.Create(no.Identidade))]),
    };

    private static JsonObject SerializarDependencia(DependenciaEntrePartes aresta)
    {
        JsonObject objeto = new() { ["tipo"] = aresta.Motivo.Codigo(), ["origem"] = aresta.Origem.Identidade, ["destino"] = aresta.Destino.Identidade };
        ContextoDosRotulos.Acrescentar(objeto, "tipo", (MotivoDaDependencia?)aresta.Motivo);
        return objeto;
    }

    internal static JsonArray SerializarModalidadesOfertadas(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        IEnumerable<string> codigos = distribuicao
            .SelectMany(static d => d.Modalidades)
            .Select(static m => HashCanonicalComputer.NormalizeNfc(m.Codigo))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static c => c, StringComparer.Ordinal);

        return new JsonArray([.. codigos.Select(static c => (JsonNode?)JsonValue.Create(c))]);
    }
}
