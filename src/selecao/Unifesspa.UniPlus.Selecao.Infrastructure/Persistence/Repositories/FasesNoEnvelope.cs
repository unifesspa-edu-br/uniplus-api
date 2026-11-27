namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>Lê as fases e etapas de uma versão congelada.</summary>
internal static class FasesNoEnvelope
{
    internal static IReadOnlyList<FaseDaVersaoPublicada> Das(string? congelado) => Ler(congelado).Fases;

    internal static UnidadeDaFase? DaFase(string? congelado, Guid faseId) =>
        Unidade(Ler(congelado).Fases.FirstOrDefault(f => f.Id == faseId));

    internal static UnidadeDaFase? DaEtapa(string? congelado, Guid etapaId)
    {
        (List<FaseDaVersaoPublicada> Fases, List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> Etapas) leitura = Ler(congelado);
        string? codigo = leitura.Etapas.FirstOrDefault(e => e.Etapa.Id == etapaId).FaseCodigo;
        return codigo is null ? null : Unidade(leitura.Fases.FirstOrDefault(f => string.Equals(f.Codigo, codigo, StringComparison.Ordinal)));
    }

    private static UnidadeDaFase? Unidade(FaseDaVersaoPublicada? fase) =>
        fase?.UnidadeResponsavelOrigemId is { } unidade ? new UnidadeDaFase(fase.Id, unidade) : null;

    private static (List<FaseDaVersaoPublicada> Fases, List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> Etapas) Ler(string? texto)
    {
        if (texto is null)
        {
            return ([], []);
        }

        return LerPresente(texto);
    }

    private static (List<FaseDaVersaoPublicada> Fases, List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> Etapas) LerPresente(string texto)
    {
        LeitorEnvelope leitor = new();
        JsonObject envelope = LerObjetoRaiz(leitor, texto);
        JsonArray etapasArray = leitor.Array(envelope, "etapas", "$");
        List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> etapas = LerEtapas(leitor, etapasArray);
        JsonObject cronograma = leitor.Objeto(envelope, "cronogramaFases", "$");
        JsonArray fasesArray = leitor.Array(cronograma, "fases", "cronogramaFases");
        List<FaseDaVersaoPublicada> fases = LerFases(leitor, fasesArray, etapas);
        if (leitor.Falhou)
        {
            throw new PayloadForaDoPerfilCanonicoException(leitor.Erro.Message);
        }

        return (fases, etapas);
    }

    private static JsonObject LerObjetoRaiz(LeitorEnvelope leitor, string texto)
    {
        try
        {
            return JsonNode.Parse(texto) as JsonObject ?? leitor.Malformado<JsonObject>("$", "esperado um objeto.");
        }
        catch (JsonException)
        {
            return leitor.Malformado<JsonObject>("$", "JSON inválido.");
        }
    }

    private static List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> LerEtapas(LeitorEnvelope leitor, JsonArray array)
    {
        List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> etapas = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            JsonObject item = leitor.ItemObjeto(array, i, "etapas");
            AdicionarEtapa(leitor, item, $"etapas[{i}]", etapas);
        }

        return etapas;
    }

    private static void AdicionarEtapa(
        LeitorEnvelope leitor, JsonObject item, string caminho, List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> etapas)
    {
        Guid id = leitor.Identificador(item, "id", caminho);
        string nome = leitor.TextoNaoVazio(item, "nome", caminho, LimitesDoEnvelope.EtapaNome);
        string? codigo = leitor.TextoOpcional(item, "faseCodigo", caminho, LimitesDoEnvelope.FaseCodigo);
        JanelaDeTempo periodo = LerPeriodo(leitor, item, caminho);
        if (!leitor.Falhou)
        {
            etapas.Add((new EtapaDaVersaoPublicada(id, nome, periodo), codigo));
        }
    }

    private static List<FaseDaVersaoPublicada> LerFases(
        LeitorEnvelope leitor, JsonArray array, List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> etapas)
    {
        List<FaseDaVersaoPublicada> fases = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            JsonObject item = leitor.ItemObjeto(array, i, "cronogramaFases.fases");
            FaseDaVersaoPublicada? fase = LerFase(leitor, item, $"cronogramaFases.fases[{i}]", etapas);
            if (fase is not null)
            {
                fases.Add(fase);
            }
        }

        return fases;
    }

    private static FaseDaVersaoPublicada? LerFase(
        LeitorEnvelope leitor, JsonObject item, string caminho, List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> etapas)
    {
        Guid id = leitor.Identificador(item, "id", caminho);
        Guid tipoId = leitor.Identificador(item, "tipoFaseOrigemId", caminho);
        string codigo = leitor.TextoNaoVazio(item, "codigo", caminho, LimitesDoEnvelope.FaseCodigo);
        string nome = leitor.TextoNaoVazio(item, "tipoFaseNome", caminho, LimitesDoEnvelope.TipoFaseNome);
        UnidadeResponsavelSnapshot? unidade = LeituraDoCronograma.LerUnidadeResponsavel(leitor, item, caminho);
        JanelaDeTempo periodo = LerPeriodo(leitor, item, caminho);
        bool coleta = leitor.Booleano(item, "coletaInscricao", caminho);
        return leitor.Falhou ? null : new FaseDaVersaoPublicada(
            id, tipoId, codigo, nome, unidade?.OrigemId, periodo, coleta,
            [.. etapas.Where(e => string.Equals(e.FaseCodigo, codigo, StringComparison.Ordinal)).Select(static e => e.Etapa)]);
    }

    private static JanelaDeTempo LerPeriodo(LeitorEnvelope leitor, JsonObject item, string caminho) =>
        new(leitor.InstanteOpcional(item, "inicio", caminho), leitor.InstanteOpcional(item, "fim", caminho));
}
