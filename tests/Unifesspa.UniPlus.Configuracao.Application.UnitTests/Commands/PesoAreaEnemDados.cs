namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Linha de Pesos por Área válida, nas duas formas: payload e agregado.</summary>
internal static class PesoAreaEnemDados
{
    public const string Resolucao = "Res. 805/2024";
    public const string BaseLegal = "Res. 805/2024 Anexo I";

    public static List<PesoAreaEnemAreaCommand> AreasDoPayload() =>
    [
        new(PesoAreaEnem.CodigoRedacao, 2.00m, 400m),
        new(PesoAreaEnem.CodigoCienciasDaNatureza, 1.50m),
        new(PesoAreaEnem.CodigoCienciasHumanas, 2.50m),
        new(PesoAreaEnem.CodigoLinguagens, 2.50m),
        new(PesoAreaEnem.CodigoMatematica, 1.50m),
    ];

    public static List<PesoAreaEnemAreaCommand> AreasDoPayloadCom(int indice, PesoAreaEnemAreaCommand area)
    {
        List<PesoAreaEnemAreaCommand> areas = AreasDoPayload();
        areas[indice] = area;
        return areas;
    }

    public static PesoAreaEnem Existente() =>
        PesoAreaEnem.Criar(
            Resolucao,
            GrupoCurso.Tecnologica,
            [.. AreasDoPayload().Select(static a => new AreaInformada(a.Codigo, a.Peso, a.Corte))],
            BaseLegal).Value!;
}
