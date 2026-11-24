namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem.PesoAreaEnemTestHelper;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class PesoAreaEnemGravacaoTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public PesoAreaEnemGravacaoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os pesos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao);

        await GravarPesoAsync(_fixture, peso);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await contextoLeitura.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);

        ConferirPesoCriado(persistida, resolucao);
        (await ContarAreasAsync(contextoLeitura, peso.Id)).Should().Be(5);
        (await GrupoPersistidoAsync(contextoLeitura, peso.Id)).Should().Be(("TECNOLOGICA", "Tecnológica"),
            "a linha grava o código do grupo e, ao lado, o rótulo posto pelo sistema");

        await ConferirLeituraPorIdAsync(contextoLeitura, peso, persistida, resolucao);
    }

    [Fact(DisplayName = "Atualizar muda os valores no lugar: continuam cinco linhas filhas, com os valores novos")]
    public async Task Atualizar_MudaNoLugar()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await GravarPesoAsync(_fixture, peso);

        await AtualizarPesosEBaseAsync(peso);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await contextoLeitura.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[0].Peso.Should().Be(3.00m);
        persistida.AreasDaLinha[0].Corte.Should().BeNull();
        persistida.AreasDaLinha[4].Peso.Should().Be(4.25m);
        persistida.BaseLegal.Should().Be("Nova base");
        persistida.UpdatedBy.Should().Be(AdminB);
        (await ContarAreasAsync(contextoLeitura, peso.Id)).Should().Be(5);
    }

    [Fact(DisplayName = "Atualizar só o peso de uma área, com a mesma base legal, carimba UpdatedAt/UpdatedBy da linha de pesos")]
    public async Task Atualizar_SoAsAreas_CarimbaAuditoriaDaLinha()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await GravarPesoAsync(_fixture, peso);

        await AtualizarPesoDeMatematicaAsync(peso);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await contextoLeitura.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[4].Peso.Should().Be(4.25m);
        persistida.BaseLegal.Should().Be(BaseLegal);
        persistida.UpdatedBy.Should().Be(AdminB, "mudar só o peso de uma área também é edição da linha de pesos");
        persistida.UpdatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Atualizar regrava o rótulo da área a partir do domínio")]
    public async Task Atualizar_RegravaORotulo()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await GravarPesoComRotuloDeAreaAntigoAsync(peso);

        await AtualizarPesoComRotuloAntigoAsync(peso);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await contextoLeitura.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[3].Rotulo.Should().Be("Linguagens e suas Tecnologias");
    }

    [Fact(DisplayName = "Atualizar regrava o rótulo do grupo a partir do domínio")]
    public async Task Atualizar_RegravaORotuloDoGrupo()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await GravarPesoComRotuloDeGrupoAntigoAsync(peso);

        await AtualizarSemAlterarOsDadosAsync(peso);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        (await GrupoPersistidoAsync(contextoLeitura, peso.Id)).Should().Be(("TECNOLOGICA", "Tecnológica"),
            "o rótulo do grupo é regravado a partir do código na edição, como o das áreas");
    }

    [Fact(DisplayName = "Edição só dos pesos concorrente com remoção lógica não desfaz a remoção")]
    public async Task AtualizarConcorrenteComRemocao_NaoRessuscitaALinha()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await GravarPesoAsync(_fixture, peso);

        // A edição carrega a linha viva; a remoção lógica é confirmada antes de a edição salvar.
        await using UniPlusDbContext contextoEdicao = _fixture.CreateDbContext(AdminA);
        PesoAreaEnem emEdicao = await contextoEdicao.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);

        await RemoverPesoAsync(_fixture, peso.Id);

        await SalvarEdicaoConcorrenteAsync(contextoEdicao, emEdicao);

        await ConferirRemocaoEAuditoriaAsync(peso);
    }

    [Fact(DisplayName = "Edição idêntica ao estado gravado não carimba a auditoria")]
    public async Task AtualizarSemMudanca_NaoCarimbaAuditoria()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await GravarPesoAsync(_fixture, peso);

        await AtualizarSemAlterarOsDadosAsync(peso);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await contextoLeitura.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.UpdatedBy.Should().BeNull("nada mudou, então não houve edição a registrar");
        persistida.UpdatedAt.Should().BeNull();
    }

    private static void ConferirPesoCriado(PesoAreaEnem persistida, string resolucao)
    {
        persistida.Resolucao.Should().Be(resolucao);
        persistida.GrupoCurso.Codigo.Should().Be(GrupoCurso.Tecnologica);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
        persistida.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().Equal(
            ("REDACAO", "Redação", 2.00m, (decimal?)400.000m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, (decimal?)null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, (decimal?)null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", 2.50m, (decimal?)null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null));
    }

    private static async Task ConferirLeituraPorIdAsync(UniPlusDbContext contextoLeitura, PesoAreaEnem peso, PesoAreaEnem persistida, string resolucao)
    {
        var reader = new PesoAreaEnemReader(contextoLeitura);
        PesoAreaEnemView? view = await reader.ObterPorIdAsync(peso.Id);
        view.Should().NotBeNull();
        view!.Resolucao.Should().Be(resolucao);
        view.GrupoCurso.Should().Be(new GrupoAreaEnemView(GrupoCurso.Tecnologica, "Tecnológica"));
        view.Areas.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().Equal(
            persistida.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)));
    }

    private async Task AtualizarPesosEBaseAsync(PesoAreaEnem peso)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await contexto.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            List<AreaInformada> novas = Areas();
            novas[0] = new(PesoAreaEnem.CodigoRedacao, 3.00m, null);
            novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);
            tracked.Atualizar(novas, "Nova base").IsSuccess.Should().BeTrue();
            await contexto.SaveChangesAsync();
        }
    }

    private async Task AtualizarPesoDeMatematicaAsync(PesoAreaEnem peso)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminB))
        {
            var repository = new PesoAreaEnemRepository(contexto);
            PesoAreaEnem tracked = (await repository.ObterPorIdAsync(peso.Id, CancellationToken.None))!;
            List<AreaInformada> novas = Areas();
            novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);
            tracked.Atualizar(novas, BaseLegal).IsSuccess.Should().BeTrue();
            await contexto.SaveChangesAsync();
        }
    }

    private async Task AtualizarPesoComRotuloAntigoAsync(PesoAreaEnem peso)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await contexto.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            tracked.AreasDaLinha[3].Rotulo.Should().Be("Rótulo antigo");
            tracked.Atualizar(Areas(), BaseLegal).IsSuccess.Should().BeTrue();
            await contexto.SaveChangesAsync();
        }
    }

    private async Task GravarPesoComRotuloDeAreaAntigoAsync(PesoAreaEnem peso)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA))
        {
            contexto.PesosAreaEnem.Add(peso);
            await contexto.SaveChangesAsync();
            await contexto.Database.ExecuteSqlAsync(
                $"UPDATE configuracao.areas_dos_pesos_do_enem SET rotulo = {"Rótulo antigo"} WHERE peso_area_enem_id = {peso.Id} AND codigo = {PesoAreaEnem.CodigoLinguagens}");
        }
    }

    private async Task GravarPesoComRotuloDeGrupoAntigoAsync(PesoAreaEnem peso)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA))
        {
            contexto.PesosAreaEnem.Add(peso);
            await contexto.SaveChangesAsync();
            await contexto.Database.ExecuteSqlAsync(
                $"UPDATE configuracao.pesos_por_area_do_enem SET grupo_curso_rotulo = {"Rótulo antigo"} WHERE id = {peso.Id}");
        }
    }

    private async Task AtualizarSemAlterarOsDadosAsync(PesoAreaEnem peso)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await contexto.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            tracked.Atualizar(Areas(), BaseLegal).IsSuccess.Should().BeTrue();
            await contexto.SaveChangesAsync();
        }
    }

    private async Task ConferirRemocaoEAuditoriaAsync(PesoAreaEnem peso)
    {
        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem final = await contextoLeitura.PesosAreaEnem.IgnoreQueryFilters().SingleAsync(p => p.Id == peso.Id);
        final.IsDeleted.Should().BeTrue("a edição regrava só as colunas de auditoria, não as de remoção lógica");
        final.DeletedBy.Should().Be(AdminB);
        final.UpdatedBy.Should().Be(AdminA);
    }

    private static async Task SalvarEdicaoConcorrenteAsync(UniPlusDbContext contextoEdicao, PesoAreaEnem emEdicao)
    {
        List<AreaInformada> novas = Areas();
        novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);
        emEdicao.Atualizar(novas, BaseLegal).IsSuccess.Should().BeTrue();
        await contextoEdicao.SaveChangesAsync();
    }
}
