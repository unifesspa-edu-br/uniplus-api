namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da classificação: quadro de pesos por área do Enem e regras de eliminação.
/// </summary>
internal static class LeituraDaClassificacao
{
    private static readonly string[] ChavesDaClassificacao =
    [
        "regraCalculo", "regraArredondamento", "casasArredondamento", "escalaNotaFinal", "regraOrdemAlocacao",
        "regrasEliminacao", "baseadoEmEnem", "resolucaoPesoAreaEnem", "quadroPesoAreaEnem",
    ];

    internal static ConfiguracaoClassificacao? LerClassificacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "classificacao", "$");
        leitor.ExigirChaves(bloco, "classificacao", ChavesDaClassificacao);

        ReferenciaRegra regraCalculo = leitor.Regra(
            bloco, "regraCalculo", "classificacao", RegraCalculoCodigo.FormulaMediaPonderada, RegraCalculoCodigo.ClassificacaoImportada);
        ReferenciaRegra? regraArredondamento = leitor.RegraOpcional(
            bloco, "regraArredondamento", "classificacao", RegraArredondamentoCodigo.PrecisaoTruncar, RegraArredondamentoCodigo.PrecisaoArredondarCima);
        int? casas = leitor.InteiroOpcional(bloco, "casasArredondamento", "classificacao");
        decimal? escalaNotaFinal = leitor.DecimalOpcional(
            bloco, "escalaNotaFinal", ConfiguracaoClassificacao.EscalaNotaFinalEscala, "classificacao", LimitesDoEnvelope.PrecisaoEscalaNotaFinal);
        ReferenciaRegra regraOrdem = leitor.Regra(
            bloco, "regraOrdemAlocacao", "classificacao", RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria);
        bool baseadoEmEnem = leitor.Booleano(bloco, "baseadoEmEnem", "classificacao");
        List<RegraEliminacao>? eliminacoes = LerRegrasDeEliminacao(leitor, bloco);
        string? resolucaoPesoAreaEnem = leitor.TextoOpcional(
            bloco, "resolucaoPesoAreaEnem", "classificacao", LimitesDoEnvelope.ResolucaoPesoAreaEnem);
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = eliminacoes is null ? null : LerQuadroPesoAreaEnem(leitor, bloco);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<ConfiguracaoClassificacao> classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = regraCalculo,
            RegraArredondamento = regraArredondamento,
            CasasArredondamento = casas,
            RegraOrdemAlocacao = regraOrdem,
            RegrasEliminacao = eliminacoes!,
            BaseadoEmEnem = baseadoEmEnem,
            ResolucaoPesoAreaEnem = resolucaoPesoAreaEnem,
            QuadroPesoAreaEnem = quadro!,
            EscalaNotaFinal = escalaNotaFinal,
        });

        return classificacao.IsFailure
            ? leitor.Propagar<ConfiguracaoClassificacao>(classificacao.Error!)
            : classificacao.Value;
    }

    private static List<RegraEliminacao>? LerRegrasDeEliminacao(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "regrasEliminacao", "classificacao");
        if (leitor.Falhou)
        {
            return null;
        }

        List<RegraEliminacao> eliminacoes = [];
        for (int i = 0; i < array.Count; i++)
        {
            RegraEliminacao? eliminacao = LerRegraDeEliminacao(
                leitor, leitor.ItemObjeto(array, i, "classificacao.regrasEliminacao"), $"classificacao.regrasEliminacao[{i}]");
            if (eliminacao is null)
            {
                return null;
            }

            eliminacoes.Add(eliminacao);
        }

        return eliminacoes;
    }

    private static RegraEliminacao? LerRegraDeEliminacao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "regra", "parametros");

        ReferenciaRegra regra = leitor.Regra(
            item,
            "regra",
            path,
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa,
            RegraEliminacaoCodigo.ElimCorteEmArea,
            RegraEliminacaoCodigo.ElimZeroEmArea,
            RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem,
            RegraEliminacaoCodigo.ElimAusenciaNaEtapa);
        JsonObject parametros = leitor.Objeto(item, "parametros", path);
        if (leitor.Falhou)
        {
            return null;
        }

        ParametrosRegraEliminacao? parametrosTipados = LerParametrosEliminacao(leitor, regra.Codigo, parametros, $"{path}.parametros");
        if (leitor.Falhou)
        {
            return null;
        }

        Result<RegraEliminacao> eliminacao = RegraEliminacao.Criar(regra, parametrosTipados!);
        return eliminacao.IsFailure ? leitor.Propagar<RegraEliminacao?>(eliminacao.Error!) : eliminacao.Value;
    }

    /// <summary>
    /// O quadro de pesos por área congelado. Coerência entre o quadro e a resolução
    /// declarada, e a presença dos dois só na classificação baseada em ENEM com cálculo
    /// local, ficam com <see cref="ConfiguracaoClassificacao.Criar"/>; os limites de cada
    /// valor, com <see cref="GrupoPesoAreaEnemCongelado.Criar"/> e com as colunas que o
    /// recebem (<see cref="LimitesDoEnvelope"/>).
    /// </summary>
    internal static IReadOnlyList<GrupoPesoAreaEnemCongelado>? LerQuadroPesoAreaEnem(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "quadroPesoAreaEnem", "classificacao");
        if (leitor.Falhou)
        {
            return null;
        }

        List<GrupoPesoAreaEnemCongelado> quadro = [];
        for (int i = 0; i < array.Count; i++)
        {
            GrupoPesoAreaEnemCongelado? grupo = LerGrupoDePesos(
                leitor, leitor.ItemObjeto(array, i, "classificacao.quadroPesoAreaEnem"), $"classificacao.quadroPesoAreaEnem[{i}]");
            if (grupo is null)
            {
                return null;
            }

            quadro.Add(grupo);
        }

        return quadro;
    }

    private static GrupoPesoAreaEnemCongelado? LerGrupoDePesos(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "grupoAreaEnem", "baseLegal", "areas");

        (string Codigo, string Rotulo)? grupo = LeituraDoGrupoDeAreaEnem.LerObrigatorio(leitor, item, path);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegalPesoAreaEnem);
        JsonArray arrayAreas = leitor.Array(item, "areas", path);
        return leitor.Falhou || grupo is not { } grupoLido ? null : CongelarGrupoDePesos(leitor, grupoLido, baseLegal, arrayAreas, path);
    }

    private static GrupoPesoAreaEnemCongelado? CongelarGrupoDePesos(
        LeitorEnvelope leitor, (string Codigo, string Rotulo) grupo, string baseLegal, JsonArray arrayAreas, string path)
    {
        List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)>? areas = LerAreas(leitor, arrayAreas, path);
        if (areas is null)
        {
            return null;
        }

        Result<GrupoPesoAreaEnemCongelado> congelado = GrupoPesoAreaEnemCongelado.Criar(grupo.Codigo, grupo.Rotulo, baseLegal, areas);
        return congelado.IsFailure ? leitor.Propagar<GrupoPesoAreaEnemCongelado?>(congelado.Error!) : congelado.Value;
    }

    private static List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)>? LerAreas(
        LeitorEnvelope leitor, JsonArray arrayAreas, string path)
    {
        List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)> areas = [];
        for (int j = 0; j < arrayAreas.Count; j++)
        {
            string pathArea = $"{path}.areas[{j}]";
            JsonObject area = leitor.ItemObjeto(arrayAreas, j, $"{path}.areas");
            leitor.ExigirChaves(area, pathArea, "codigo", "rotulo", "peso", "corte");

            string codigo = leitor.TextoNaoVazio(area, "codigo", pathArea, LimitesDoEnvelope.AreaPesoAreaEnemCodigo);
            string rotulo = leitor.TextoNaoVazio(area, "rotulo", pathArea, LimitesDoEnvelope.AreaPesoAreaEnemRotulo);
            decimal peso = leitor.Decimal(area, "peso", EscritaCanonica.EscalaPadrao, pathArea, LimitesDoEnvelope.PrecisaoPesoAreaEnem);
            decimal? corte = leitor.DecimalOpcional(area, "corte", EscritaCanonica.EscalaPadrao, pathArea, LimitesDoEnvelope.PrecisaoCorteAreaEnem);
            if (leitor.Falhou)
            {
                return null;
            }

            areas.Add((codigo, rotulo, peso, corte));
        }

        return areas;
    }

    internal static ParametrosRegraEliminacao? LerParametrosEliminacao(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject parametros,
        string path) => codigo switch
        {
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa => LerNotaMinimaDaEtapa(leitor, parametros, path),
            RegraEliminacaoCodigo.ElimCorteEmArea => LerCorteEmArea(leitor, parametros, path),
            RegraEliminacaoCodigo.ElimZeroEmArea => LerSemParametros(leitor, parametros, path, static () => new ParametrosElimZeroEmArea()),
            RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem => LerSemParametros(leitor, parametros, path, static () => new ParametrosElimFaltaEmDiaDeProvaEnem()),
            RegraEliminacaoCodigo.ElimAusenciaNaEtapa => LerAusenciaNaEtapa(leitor, parametros, path),
            _ => leitor.Propagar<ParametrosRegraEliminacao>(new DomainError(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"Não há variante de parâmetros conhecida para a regra de eliminação '{codigo}' em '{path}'.")),
        };

    private static ParametrosElimNotaMinimaEtapa? LerNotaMinimaDaEtapa(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "etapaRef", "notaMinima");
        Guid etapaRef = leitor.Identificador(parametros, "etapaRef", path);
        decimal notaMinima = leitor.Decimal(parametros, "notaMinima", EscritaCanonica.EscalaPadrao, path);
        return leitor.Falhou ? null : new ParametrosElimNotaMinimaEtapa(etapaRef, notaMinima);
    }

    private static ParametrosElimCorteEmArea? LerCorteEmArea(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "areaCodigo", "minimo");
        string areaCodigo = leitor.TextoNaoVazio(parametros, "areaCodigo", path, LimitesDoEnvelope.AreaPesoAreaEnemCodigo);
        decimal minimo = leitor.Decimal(parametros, "minimo", EscritaCanonica.EscalaPadrao, path);
        return leitor.Falhou ? null : new ParametrosElimCorteEmArea(areaCodigo, minimo);
    }

    private static ParametrosElimAusenciaNaEtapa? LerAusenciaNaEtapa(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "etapaRef");
        Guid etapaRef = leitor.Identificador(parametros, "etapaRef", path);
        return leitor.Falhou ? null : new ParametrosElimAusenciaNaEtapa(etapaRef);
    }

    private static ParametrosRegraEliminacao? LerSemParametros(LeitorEnvelope leitor, JsonObject parametros, string path, Func<ParametrosRegraEliminacao> criar)
    {
        leitor.ExigirChaves(parametros, path);
        return leitor.Falhou ? null : criar();
    }
}
