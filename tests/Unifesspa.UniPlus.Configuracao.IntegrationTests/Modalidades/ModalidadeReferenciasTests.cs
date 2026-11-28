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
public sealed class ModalidadeReferenciasTests(ConfiguracaoDbFixture fixture)
{
    private readonly ConfiguracaoDbFixture _fixture = fixture;

    [Fact(DisplayName = "Remoção é bloqueada por referência via composicao_origem (repo)")]
    public async Task EhReferenciada_PorComposicaoOrigem_True()
    {
        string origem = CodigoUnico();
        string dependente = CodigoUnico();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Modalidades.Add(Ampla(origem));
        ctx.Modalidades.Add(RetiradaDeOutraModalidade(dependente, origem));
        await ctx.SaveChangesAsync();

        var repo = new ModalidadeRepository(ctx);
        bool referenciada = await repo.EhReferenciadaPorOutraModalidadeVivaAsync(origem, ExcluirId(ctx, origem), default);

        referenciada.Should().BeTrue("outra modalidade viva aponta este código como composicao_origem");
    }

    [Fact(DisplayName = "Remoção é bloqueada por referência pelo destino de remanejamento (repo)")]
    public async Task EhReferenciada_PorRemanejamentoParametros_True()
    {
        string destino = CodigoUnico();
        string dependente = CodigoUnico();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Modalidades.Add(Ampla(destino));
        ctx.Modalidades.Add(DestinoUnico(dependente, destino));
        await ctx.SaveChangesAsync();

        var repo = new ModalidadeRepository(ctx);
        bool referenciada = await repo.EhReferenciadaPorOutraModalidadeVivaAsync(destino, ExcluirId(ctx, destino), default);

        referenciada.Should().BeTrue("outra modalidade viva aponta para o destino");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EhReferenciada_PorParOuDestinoFinal_True(bool destinoFinal)
    {
        string codigo = CodigoUnico();
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA);
        contexto.Modalidades.Add(Ampla(codigo));
        contexto.Modalidades.Add(Cruzada(CodigoUnico(), codigo, destinoFinal));
        await contexto.SaveChangesAsync();
        ModalidadeRepository repositorio = new(contexto);
        bool referenciada = await repositorio.EhReferenciadaPorOutraModalidadeVivaAsync(codigo, null, default);
        referenciada.Should().BeTrue();
    }

    private static Modalidade Cruzada(string codigo, string referencia, bool destinoFinal)
    {
        DeclaracaoDaModalidade declaracao = new(NomeDe(codigo), null, "ACAO_AFIRMATIVA", "SUPLEMENTAR_AO_TOTAL", null, "CRUZADO",
            null, destinoFinal ? "OUTRA" : referencia, destinoFinal ? referencia : null, SementeDeAplicabilidadeLegal.AcI, null, null);
        Result<Modalidade> resultado = Modalidade.Criar(codigo, declaracao);
        resultado.IsSuccess.Should().BeTrue();
        return resultado.Value!;
    }

    [Fact(DisplayName = "Código não referenciado por ninguém não bloqueia a remoção (repo)")]
    public async Task EhReferenciada_SemReferencia_False()
    {
        string livre = CodigoUnico();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Modalidades.Add(Ampla(livre));
        await ctx.SaveChangesAsync();

        var repo = new ModalidadeRepository(ctx);
        bool referenciada = await repo.EhReferenciadaPorOutraModalidadeVivaAsync(livre, ExcluirId(ctx, livre), default);

        referenciada.Should().BeFalse();
    }

}
