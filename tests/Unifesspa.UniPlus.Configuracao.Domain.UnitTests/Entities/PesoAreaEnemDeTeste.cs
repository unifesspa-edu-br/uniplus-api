namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Dados e atalhos de criação compartilhados pelos testes do agregado de Pesos por Área.</summary>
internal static class PesoAreaEnemDeTeste
{
    internal const string Resolucao = "Res. 805/2024";
    internal const string Grupo = GrupoCurso.Tecnologica;
    internal const string BaseLegal = "Res. 805/2024 Anexo I";

    internal static List<AreaInformada> AreasValidas() =>
    [
        new(PesoAreaEnem.CodigoRedacao, 2.00m, 400m),
        new(PesoAreaEnem.CodigoCienciasDaNatureza, 1.50m, null),
        new(PesoAreaEnem.CodigoCienciasHumanas, 2.50m, null),
        new(PesoAreaEnem.CodigoLinguagens, 2.50m, null),
        new(PesoAreaEnem.CodigoMatematica, 1.50m, null),
    ];

    internal static Result<PesoAreaEnem> Criar(
        IReadOnlyList<AreaInformada>? areas = null,
        string resolucao = Resolucao,
        string grupo = Grupo,
        string? baseLegal = BaseLegal) =>
        PesoAreaEnem.Criar(resolucao, grupo, areas ?? AreasValidas(), baseLegal);

    internal static List<AreaInformada> ComArea(
        int indice, AreaInformada area)
    {
        List<AreaInformada> areas = AreasValidas();
        areas[indice] = area;
        return areas;
    }
}
