namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

        DeclaracaoDaClassificacao? declaracao = LerDeclaracao(leitor, bloco);
        if (declaracao is null)
        {
            return null;
        }
        Result<ConfiguracaoClassificacao> classificacao = ConfiguracaoClassificacao.Criar(declaracao);
        return classificacao.IsFailure
            ? leitor.Propagar<ConfiguracaoClassificacao>([.. classificacao.Errors.Select(static causa => causa.Field == "regraArredondamentoCodigo"
                || causa.Field?.EndsWith(".areaCodigo", StringComparison.Ordinal) == true ? LeitorEnvelope.NoProprioItem([causa])[0] : causa)], "classificacao")
            : classificacao.Value;
    }

    private static DeclaracaoDaClassificacao? LerDeclaracao(LeitorEnvelope leitor, JsonObject bloco)
    {
        DeclaracaoDaClassificacao calculoEOrdem = LerCalculoEOrdemDeAlocacao(leitor, bloco);
        bool baseadoEmEnem = leitor.Booleano(bloco, "baseadoEmEnem", "classificacao");
        List<RegraEliminacao>? eliminacoes = LerRegrasDeEliminacao(leitor, bloco);
        (string? resolucaoPesoAreaEnem, IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro) = LerPesosDaAreaDoEnem(leitor, bloco, eliminacoes is not null);

        return leitor.Falhou
            ? null
            : calculoEOrdem with
            {
                RegrasEliminacao = eliminacoes!,
                BaseadoEmEnem = baseadoEmEnem,
                ResolucaoPesoAreaEnem = resolucaoPesoAreaEnem,
                QuadroPesoAreaEnem = quadro!,
            };
    }

    private static (string? Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado>? Quadro) LerPesosDaAreaDoEnem(
        LeitorEnvelope leitor, JsonObject bloco, bool eliminacoesLidas)
    {
        string? resolucao = leitor.TextoOpcional(bloco, "resolucaoPesoAreaEnem", "classificacao", LimitesDoEnvelope.ResolucaoPesoAreaEnem);
        return (resolucao, eliminacoesLidas ? LerQuadroPesoAreaEnem(leitor, bloco) : null);
    }

    /// <summary>A leitura segue a ordem das propriedades: o leitor guarda a primeira recusa, e a ordem decide qual delas.</summary>
    private static DeclaracaoDaClassificacao LerCalculoEOrdemDeAlocacao(LeitorEnvelope leitor, JsonObject bloco) => new()
    {
        RegraCalculo = leitor.Regra<RegraDeCalculoDaNota>(bloco, "regraCalculo", "classificacao"),
        RegraArredondamento = leitor.RegraOpcional<RegraDeArredondamentoDaNota>(bloco, "regraArredondamento", "classificacao"),
        CasasArredondamento = leitor.InteiroOpcional(bloco, "casasArredondamento", "classificacao"),
        EscalaNotaFinal = leitor.DecimalOpcional(
            bloco, "escalaNotaFinal", ConfiguracaoClassificacao.EscalaNotaFinalEscala, "classificacao", LimitesDoEnvelope.PrecisaoEscalaNotaFinal),
        RegraOrdemAlocacao = leitor.Regra<RegraDeOrdemDeAlocacao>(bloco, "regraOrdemAlocacao", "classificacao"),
    };

    private static List<RegraEliminacao>? LerRegrasDeEliminacao(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "regrasEliminacao", "classificacao");
        if (leitor.Falhou)
        {
            return null;
        }

        return LeituraDeItens.Ler(leitor, array, "classificacao.regrasEliminacao", LerRegraDeEliminacao);
    }

    private static RegraEliminacao? LerRegraDeEliminacao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "regra", "parametros");

        ReferenciaRegra regra = leitor.Regra<RegraDeEliminacaoPorCalculo>(item, "regra", path);
        JsonObject parametros = leitor.Objeto(item, "parametros", path);
        if (leitor.Falhou)
        {
            return null;
        }

        return ConstruirRegraDeEliminacao(leitor, regra, parametros, path);
    }

    private static RegraEliminacao? ConstruirRegraDeEliminacao(
        LeitorEnvelope leitor, ReferenciaRegra regra, JsonObject parametros, string path)
    {
        ParametrosRegraEliminacao? parametrosTipados = LerParametrosEliminacao(leitor, CatalogoDe<RegraDeEliminacaoPorCalculo>.Obter(regra.Codigo), parametros, $"{path}.parametros");
        if (leitor.Falhou)
        {
            return null;
        }

        Result<RegraEliminacao> eliminacao = RegraEliminacao.Criar(regra, parametrosTipados!);
        return eliminacao.IsFailure ? leitor.Propagar<RegraEliminacao?>(eliminacao.Errors, path) : eliminacao.Value;
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

        return LeituraDeItens.Ler(leitor, array, "classificacao.quadroPesoAreaEnem", LerGrupoDePesos);
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
        return congelado.IsFailure ? leitor.Propagar<GrupoPesoAreaEnemCongelado?>(congelado.Errors, path) : congelado.Value;
    }

    private static List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)>? LerAreas(
        LeitorEnvelope leitor, JsonArray arrayAreas, string path)
    {
        List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)> areas = [];
        for (int j = 0; j < arrayAreas.Count; j++)
        {
            (string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)? area =
                LerArea(leitor, leitor.ItemObjeto(arrayAreas, j, $"{path}.areas"), $"{path}.areas[{j}]");
            if (area is null)
            {
                return null;
            }

            areas.Add(area.Value);
        }

        return areas;
    }

    private static (string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)? LerArea(
        LeitorEnvelope leitor, JsonObject area, string path)
    {
        leitor.ExigirChaves(area, path, "codigo", "rotulo", "peso", "corte");
        string codigo = leitor.TextoNaoVazio(area, "codigo", path, LimitesDoEnvelope.AreaPesoAreaEnemCodigo);
        string rotulo = leitor.TextoNaoVazio(area, "rotulo", path, LimitesDoEnvelope.AreaPesoAreaEnemRotulo);
        decimal peso = leitor.Decimal(area, "peso", GrupoPesoAreaEnemCongelado.EscalaDoPesoEDoCorte, path, LimitesDoEnvelope.PrecisaoPesoAreaEnem);
        decimal? corte = leitor.DecimalOpcional(area, "corte", GrupoPesoAreaEnemCongelado.EscalaDoPesoEDoCorte, path, LimitesDoEnvelope.PrecisaoCorteAreaEnem);
        return leitor.Falhou ? null : (codigo, rotulo, peso, corte);
    }

    private static ParametrosRegraEliminacao? LerParametrosEliminacao(
        LeitorEnvelope leitor,
        RegraDeEliminacaoPorCalculo codigo,
        JsonObject parametros,
        string path) => codigo switch
        {
            RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa => LerNotaMinimaDaEtapa(leitor, parametros, path),
            RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem => LerCorteEmArea(leitor, parametros, path),
            RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem => LerSemParametros(leitor, parametros, path, static () => new ParametrosEliminacaoPorZeroEmAreaDoEnem()),
            RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem => LerSemParametros(leitor, parametros, path, static () => new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem()),
            RegraDeEliminacaoPorCalculo.AusenciaNaEtapa => LerAusenciaNaEtapa(leitor, parametros, path),
            _ => throw new ArgumentOutOfRangeException(nameof(codigo)),
        };

    private static ParametrosEliminacaoPorNotaMinimaNaEtapa? LerNotaMinimaDaEtapa(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "etapaId", "notaMinima");
        Guid etapaRef = leitor.Identificador(parametros, "etapaId", path);
        decimal notaMinima = leitor.Decimal(parametros, "notaMinima", EscritaCanonica.EscalaPadrao, path);
        return leitor.Falhou ? null : new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapaRef, notaMinima);
    }

    private static ParametrosEliminacaoPorCorteEmAreaDoEnem? LerCorteEmArea(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "areaCodigo", "minimo");
        string areaCodigo = leitor.TextoNaoVazio(parametros, "areaCodigo", path, LimitesDoEnvelope.AreaPesoAreaEnemCodigo);
        decimal minimo = leitor.Decimal(parametros, "minimo", EscritaCanonica.EscalaPadrao, path);
        return leitor.Falhou ? null : new ParametrosEliminacaoPorCorteEmAreaDoEnem(areaCodigo, minimo);
    }

    private static ParametrosEliminacaoPorAusenciaNaEtapa? LerAusenciaNaEtapa(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "etapaId");
        Guid etapaRef = leitor.Identificador(parametros, "etapaId", path);
        return leitor.Falhou ? null : new ParametrosEliminacaoPorAusenciaNaEtapa(etapaRef);
    }

    private static ParametrosRegraEliminacao? LerSemParametros(LeitorEnvelope leitor, JsonObject parametros, string path, Func<ParametrosRegraEliminacao> criar)
    {
        leitor.ExigirChaves(parametros, path);
        return leitor.Falhou ? null : criar();
    }
}
