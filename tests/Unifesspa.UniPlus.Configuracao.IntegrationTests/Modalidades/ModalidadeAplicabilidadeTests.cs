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

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades.ApoioDaPersistenciaDeModalidades;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class ModalidadeAplicabilidadeTests(ConfiguracaoDbFixture fixture)
{
    private readonly ConfiguracaoDbFixture _fixture = fixture;

    [Fact(DisplayName = "Seed: a regra de cada modalidade legal passa pela conferência contra o catálogo semeado")]
    public async Task Seed_RegrasDeAplicabilidade_PassamPelaConferenciaDoCatalogo()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Include(static f => f.ValoresDominioDeclarados).ToListAsync();
        List<PrecedenciaFase> precedencias = await ctx.PrecedenciasFase.AsNoTracking().ToListAsync();
        CatalogoDeFatos catalogo = new(fatos, precedencias);

        foreach (ModalidadeSeedItem item in ModalidadeSeed.Itens)
        {
            IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(item.RegraDeAplicabilidade, vigente: null, catalogo);

            erros.Should().BeEmpty(
                $"a regra semeada de '{item.Codigo}' tem de valer no catálogo — "
                + string.Join("; ", erros.Select(static e => e.Error.Message)));
        }
    }

    [Fact(DisplayName = "Regra que cita a modalidade da convocação é recusada: ela só existe depois do resultado final")]
    public async Task Conferir_ModalidadeDaConvocacao_RecusaPelasPrecedenciasSemeadas()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Include(static f => f.ValoresDominioDeclarados).ToListAsync();
        List<PrecedenciaFase> precedencias = await ctx.PrecedenciasFase.AsNoTracking().ToListAsync();
        PredicadoDnf regra = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("MODALIDADE_CONVOCACAO", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!)]).Value!;

        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(
            regra, vigente: null, new CatalogoDeFatos(fatos, precedencias));

        erros.Select(static e => e.Error.Code).Should().Contain(ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao);
    }

    [Fact(DisplayName = "Seed: a regra gravada de cada modalidade legal volta igual pela leitura cross-módulo")]
    public async Task Seed_RegraDeAplicabilidade_VoltaIgualPeloReader()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        IReadOnlyList<ModalidadeView> vivas = await new ModalidadeReader(ctx).ListarVivosAsync(CancellationToken.None);

        foreach (ModalidadeSeedItem item in ModalidadeSeed.Itens)
        {
            PredicadoDnf lida = vivas.Single(v => v.Codigo == item.Codigo).RegraDeAplicabilidade;

            PredicadoDnfJson.ParaJson(lida).ToJsonString().Should().Be(
                PredicadoDnfJson.ParaJson(item.RegraDeAplicabilidade).ToJsonString(), item.Codigo);
        }
    }

}
