namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

internal static class PesoAreaEnemTestHelper
{
    internal const string AdminA = "admin-a";
    internal const string AdminB = "admin-b";
    internal const string BaseLegal = "Res. 805/2024 Anexo I";

    internal static List<AreaInformada> Areas() =>
    [
        new(PesoAreaEnem.CodigoRedacao, 2.00m, 400m),
        new(PesoAreaEnem.CodigoCienciasDaNatureza, 1.50m, null),
        new(PesoAreaEnem.CodigoCienciasHumanas, 2.50m, null),
        new(PesoAreaEnem.CodigoLinguagens, 2.50m, null),
        new(PesoAreaEnem.CodigoMatematica, 1.50m, null),
    ];

    internal static PesoAreaEnem Nova(string resolucao, string grupo = GrupoCurso.Tecnologica) =>
        PesoAreaEnem.Criar(resolucao, grupo, Areas(), BaseLegal).Value!;

    internal static Task<int> ContarAreasAsync(UniPlusDbContext contexto, Guid pesoId) =>
        contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM configuracao.areas_dos_pesos_do_enem WHERE peso_area_enem_id = {pesoId}")
            .SingleAsync();

    internal static async Task<(string Codigo, string Rotulo)> GrupoPersistidoAsync(UniPlusDbContext contexto, Guid pesoId)
    {
        GrupoGravado grupo = await contexto.Database
            .SqlQuery<GrupoGravado>($"SELECT grupo_curso AS codigo, grupo_curso_rotulo AS rotulo FROM configuracao.pesos_por_area_do_enem WHERE id = {pesoId}")
            .SingleAsync();
        return (grupo.Codigo, grupo.Rotulo);
    }

    internal static async Task GravarPesoAsync(ConfiguracaoDbFixture fixture, PesoAreaEnem peso)
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext(AdminA);
        contexto.PesosAreaEnem.Add(peso);
        await contexto.SaveChangesAsync();
    }

    internal static async Task RemoverPesoAsync(ConfiguracaoDbFixture fixture, Guid pesoId)
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext(AdminB);
        PesoAreaEnem peso = await contexto.PesosAreaEnem.SingleAsync(p => p.Id == pesoId);
        contexto.PesosAreaEnem.Remove(peso);
        await contexto.SaveChangesAsync();
    }

    private sealed record GrupoGravado(string Codigo, string Rotulo);

    internal static string ResolucaoUnica() => $"Res. {Guid.NewGuid().ToString("N")[..12]}";
}
