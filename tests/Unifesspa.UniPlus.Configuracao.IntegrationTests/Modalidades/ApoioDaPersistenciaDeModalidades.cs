namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal static class ApoioDaPersistenciaDeModalidades
{
    internal const string AdminA = "admin-a";
    internal const string AdminB = "admin-b";

    internal static async Task GravarAsync(ConfiguracaoDbFixture fixture, params Modalidade[] modalidades)
    {
        await using UniPlusDbContext context = fixture.CreateDbContext(AdminA);
        context.Modalidades.AddRange(modalidades);
        await context.SaveChangesAsync();
    }

    internal static async Task RemoverAsync(ConfiguracaoDbFixture fixture, Guid id)
    {
        await using UniPlusDbContext context = fixture.CreateDbContext(AdminB);
        Modalidade modalidade = await context.Modalidades.SingleAsync(m => m.Id == id);
        context.Modalidades.Remove(modalidade);
        await context.SaveChangesAsync();
    }

    internal static Guid ExcluirId(UniPlusDbContext ctx, string codigo)
    {
        CodigoModalidade vo = CodigoModalidade.Criar(codigo).Value!;
        return ctx.Modalidades.Single(m => m.Codigo == vo).Id;
    }

    internal static Modalidade Ampla(string codigo) =>
        Modalidade.Criar(codigo, new DeclaracaoDaModalidade(null, "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS", null, null, null, null, null,
            SementeDeAplicabilidadeLegal.AmplaConcorrencia, null, null)).Value!;

    internal static Modalidade Cota(string codigo) =>
        Modalidade.Criar(codigo, new DeclaracaoDaModalidade("Cota", "COTA_RESERVADA", "DENTRO_DA_RESERVA_LEGAL", null, "SEGUE_CASCATA", null, null, null,
            SementeDeAplicabilidadeLegal.LiEp, null, null)).Value!;

    internal static Modalidade RetiradaDeOutraModalidade(string codigo, string origem) =>
        Modalidade.Criar(codigo, new DeclaracaoDaModalidade(null, "AMPLA", "RETIRADA_DE_OUTRA_MODALIDADE", origem, null, null, null, null,
            SementeDeAplicabilidadeLegal.AmplaConcorrencia, null, null)).Value!;

    internal static Modalidade DestinoUnico(string codigo, string destino) =>
        Modalidade.Criar(codigo, new DeclaracaoDaModalidade(null, "ACAO_AFIRMATIVA", "SUPLEMENTAR_AO_TOTAL", null, "DESTINO_UNICO", destino, null, null,
            SementeDeAplicabilidadeLegal.AcPcd, null, null)).Value!;

    internal static string CodigoUnico() => $"MOD_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
