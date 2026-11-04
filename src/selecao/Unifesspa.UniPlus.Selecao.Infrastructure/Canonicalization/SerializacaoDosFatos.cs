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
/// Projeta no envelope os fatos coletados e derivados, os grupos de coleta, as regras de derivação e o grafo de dependência.
/// </summary>
internal static class SerializacaoDosFatos
{
    internal static JsonArray SerializarFatosDerivados(IReadOnlyDictionary<string, FatoDerivadoCongelado>? fatos) =>
        new([.. (fatos ?? new Dictionary<string, FatoDerivadoCongelado>(StringComparer.Ordinal)).Values
            .OrderBy(static f => HashCanonicalComputer.NormalizeNfc(f.Codigo), StringComparer.Ordinal)
            .Select(static f => (JsonNode?)new JsonObject
            {
                ["codigo"] = HashCanonicalComputer.NormalizeNfc(f.Codigo),
                ["nome"] = HashCanonicalComputer.NormalizeNfc(f.Nome),
                ["descricao"] = HashCanonicalComputer.NormalizeNfc(f.Descricao),
                ["componenteExibicao"] = f.ComponenteExibicao,
                ["dominio"] = f.Dominio,
                ["cardinalidade"] = f.Cardinalidade,
                ["classificacaoProtecao"] = f.ClassificacaoProtecao,
                ["hipoteseLegal"] = f.HipoteseLegal,
                ["finalidadeTratamento"] = HashCanonicalComputer.NormalizeNfc(f.FinalidadeTratamento),
            })]);

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

    private static JsonObject SerializarMetadadoDoFato(MetadadoFatoCongelado metadado) => new()
    {
        ["fatoCodigo"] = HashCanonicalComputer.NormalizeNfc(metadado.Codigo),
        ["dominio"] = HashCanonicalComputer.NormalizeNfc(metadado.Dominio),
        ["origem"] = HashCanonicalComputer.NormalizeNfc(metadado.Origem),
        ["cardinalidade"] = HashCanonicalComputer.NormalizeNfc(metadado.Cardinalidade),
        ["pontoResolucao"] = HashCanonicalComputer.NormalizeNfc(metadado.PontoResolucao),
        ["binding"] = HashCanonicalComputer.NormalizeNfc(metadado.Binding),
        ["valoresDominio"] = metadado.ValoresDominio is { } valores
            ? EscritaCanonica.TextosEmOrdemCanonica(valores)
            : null,
        ["valoresDominioDeclarados"] = metadado.ValoresDominioDeclarados is { } declarados
            ? SerializarValoresDominioDeclarados(declarados)
            : null,
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
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados)
    {
        JsonArray array = [];
        foreach (FatoColetado fato in fatos.OrderBy(static f => f.Finalidade).ThenBy(static f => f.Ordem))
        {
            array.Add(SerializarFatoColetado(fato, ValoresSelecionaveisDoFato(fato, valoresSelecionaveisCongelados)));
        }

        return array;
    }

    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? ValoresSelecionaveisDoFato(
        FatoColetado fato,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados)
    {
        bool ehFatoDeSelecao = fato.TipoRenderizacao.EhSelecao();
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato = null;
        bool temEntrada = valoresSelecionaveisCongelados?.TryGetValue(fato.FatoCodigo, out valoresDoFato) ?? false;

        if (DefeitoDoDicionarioDeValores(ehFatoDeSelecao, temEntrada, valoresDoFato) is { } defeito)
        {
            throw new InvalidOperationException(
                $"O fato coletado '{fato.FatoCodigo}' {(ehFatoDeSelecao ? "é" : "não é")} de seleção " +
                $"({fato.TipoRenderizacao}), mas o dicionário de valores selecionáveis {defeito}");
        }

        return valoresDoFato;
    }

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
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato) => new()
    {
        ["fatoCodigo"] = HashCanonicalComputer.NormalizeNfc(fato.FatoCodigo),
        ["finalidade"] = EstruturaFormulario.ParaToken(fato.Finalidade),
        ["etapaCodigo"] = fato.EtapaCodigo is { } etapa ? HashCanonicalComputer.NormalizeNfc(etapa) : null,
        ["ordem"] = fato.Ordem,
        ["rotulo"] = HashCanonicalComputer.NormalizeNfc(fato.Rotulo),
        ["tipoRenderizacao"] = fato.TipoRenderizacao.ToCodigo(),
        ["obrigatoriedade"] = SerializacaoDosFormularios.SerializarObrigatoriedade(fato.Obrigatoriedade),
        ["ajuda"] = fato.Ajuda is { } ajuda ? HashCanonicalComputer.NormalizeNfc(ajuda) : null,
        ["pedirConfirmacao"] = fato.PedirConfirmacao,
        ["restricoes"] = SerializacaoDosFormularios.SerializarRestricoes(fato.Restricoes),
        ["impedimento"] = fato.Impedimento is { } impedimento ? SerializacaoDosFormularios.SerializarImpedimento(impedimento) : null,
        ["origemValores"] = fato.OrigemValores.ToString(),
        ["formato"] = fato.Formato,
        ["classificacaoProtecao"] = fato.ClassificacaoProtecao,
        ["hipoteseLegal"] = fato.HipoteseLegal,
        ["finalidadeTratamento"] = HashCanonicalComputer.NormalizeNfc(fato.FinalidadeTratamento),
        ["descricao"] = HashCanonicalComputer.NormalizeNfc(fato.Descricao),
        ["componenteExibicao"] = fato.ComponenteExibicao,
        ["precondicao"] = SerializacaoDosPredicados.SerializarDnf(fato.Precondicoes.Select(
            static c => (c.Clausula, c.Fato, c.Operador, c.Valor))),
        ["valoresSelecionaveis"] = valoresDoFato is null ? null : SerializarValoresSelecionaveis(valoresDoFato),
    };

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
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados,
        IReadOnlyList<DefinicaoAgregado>? agregados) =>
        [.. grupos.OrderBy(static g => g.Finalidade).ThenBy(static g => g.Ordem).Select(g => (JsonNode)new JsonObject
        {
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(g.Codigo),
            ["finalidade"] = EstruturaFormulario.ParaToken(g.Finalidade),
            ["etapaCodigo"] = g.EtapaCodigo is { } etapa ? HashCanonicalComputer.NormalizeNfc(etapa) : null,
            ["ordem"] = g.Ordem,
            ["rotulo"] = HashCanonicalComputer.NormalizeNfc(g.Rotulo),
            ["minimo"] = g.Minimo,
            ["maximo"] = g.Maximo,
            ["incluiCandidato"] = g.IncluiCandidato,
            ["exibicao"] = g.Exibicao is { } exibicao ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(exibicao)) : null,
            ["obrigatoriedade"] = SerializacaoDosFormularios.SerializarObrigatoriedade(g.Obrigatoriedade),
            ["subitens"] = SerializarFatosColetados(g.Subitens, valoresSelecionaveisCongelados),
            ["agregados"] = new JsonArray([.. (agregados ?? [])
                .Where(a => string.Equals(a.GrupoCodigo, g.Codigo, StringComparison.Ordinal))
                .OrderBy(static a => a.Codigo, StringComparer.Ordinal)
                .Select(static a => (JsonNode)new JsonObject
                {
                    ["codigo"] = HashCanonicalComputer.NormalizeNfc(a.Codigo),
                    ["fatoDeMembro"] = HashCanonicalComputer.NormalizeNfc(a.FatoDeMembro),
                    ["operacao"] = AgregadoDeGrupo.ParaToken(a.Operacao),
                })]),
        })];

    internal static JsonArray SerializarRegrasDerivacao(IEnumerable<ConfiguracaoDerivacaoFato> regrasDerivacao)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            JsonArray regras = [];
            foreach (RegraDerivacaoConfigurada regra in config.Regras.OrderBy(static r => r.Ordem))
            {
                regras.Add(new JsonObject
                {
                    ["ordem"] = regra.Ordem,
                    ["contribui"] = regra.Contribui is { } contribui ? HashCanonicalComputer.NormalizeNfc(contribui) : null,
                    ["quando"] = SerializacaoDosPredicados.SerializarDnf(regra.Condicoes.Select(
                        static c => (c.Clausula, c.Fato, c.Operador, c.Valor))),
                });
            }

            array.Add(new JsonObject
            {
                ["codigoFato"] = HashCanonicalComputer.NormalizeNfc(config.CodigoFato),
                ["regras"] = regras,
            });
        }

        return array;
    }

    internal static JsonObject SerializarGrafoDependencia(ProcessoSeletivo processo)
    {
        Result<GrafoDependenciaConjunta> grafo = Domain.ProcessosSeletivos.Formularios.CitacoesDosFormularios.ConstruirGrafoDependencia(Domain.ProcessosSeletivos.Edicao.EstadoDaConfiguracao.De(processo));
        if (grafo.IsFailure)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo cujo grafo de dependência conjunto forma um ciclo — o gate de "
                + $"publicação deveria tê-lo recusado antes deste ponto: {grafo.Error!.Message}");
        }

        return SerializarGrafoDependencia(grafo.Value!);
    }

    internal static JsonObject SerializarGrafoDependencia(GrafoDependenciaConjunta grafo) => new()
    {
        ["nos"] = new JsonArray([.. grafo.Nos.Select(no => (JsonNode)new JsonObject
        {
            ["idCanonico"] = RotuloCanonico(no),
        })]),
        ["arestas"] = new JsonArray([.. grafo.Arestas.Select(aresta => (JsonNode)new JsonObject
        {
            ["tipo"] = aresta.Tipo.ToCodigo(),
            ["origem"] = RotuloCanonico(aresta.Origem),
            ["destino"] = RotuloCanonico(aresta.Destino),
        })]),
        ["ordemTopologica"] = new JsonArray(
            [.. grafo.OrdemTopologica.Select(no => (JsonNode?)JsonValue.Create(RotuloCanonico(no)))]),
    };

    private static string RotuloCanonico(NoGrafoDependencia no) => $"{no.Classe.ToCodigo()}/{no.Codigo}";

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
