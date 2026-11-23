namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Cenarios;

public sealed class NiveisDeAtendimentoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact]
    public async Task GravarENovaDefinicao_SubstituiNiveisEOrdenaNaLeitura()
    {
        Guid etapaId = await GravarEtapaAsync([new("Parcial", 0.5m), new("Completo", 1m)]);
        await using UniPlusDbContext troca = fixture.CreateDbContext();
        EtapaProcesso etapa = await troca.Set<EtapaProcesso>().Include(e => e.ItensDeAvaliacao).SingleAsync(e => e.Id == etapaId);
        etapa.EscalaDeAtendimento!.Select(n => n.Rotulo).Should().Equal("Parcial", "Completo");
        AtualizarNiveis(etapa);
        await troca.SaveChangesAsync();
        await using UniPlusDbContext leitura = fixture.CreateDbContext();
        EtapaProcesso relida = await leitura.Set<EtapaProcesso>().SingleAsync(e => e.Id == etapaId);
        relida.EscalaDeAtendimento!.Select(n => n.Rotulo).Should().Equal("Não atende", "Completo");
    }

    [Fact]
    public async Task ExcluirEtapa_ExcluiNiveisEmCascata()
    {
        Guid etapaId = await GravarEtapaAsync([new("Completo", 1m), new("Parcial", 0.5m)]);
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        await contexto.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM selecao.etapas_processo WHERE id = {etapaId}");
        int restantes = await contexto.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM selecao.niveis_de_atendimento_da_etapa WHERE etapa_processo_id = {etapaId}
            """).SingleAsync();
        restantes.Should().Be(0);
    }

    [Theory]
    [InlineData("Inválido", -0.1, "ck_niveis_atendimento_fracao_entre_0_e_1")]
    [InlineData("Inválido", 1.2, "ck_niveis_atendimento_fracao_entre_0_e_1")]
    [InlineData("   ", 0.5, "ck_niveis_atendimento_rotulo_preenchido")]
    [InlineData("Completo", 0.5, "uq_niveis_atendimento_etapa_rotulo")]
    public async Task InserirNivel_ComDadosInvalidos_BancoRecusa(string rotulo, decimal fracao, string restricao)
    {
        Guid etapaId = await GravarEtapaAsync([new("Completo", 1m)]);
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        Func<Task> inserir = () => contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO selecao.niveis_de_atendimento_da_etapa (etapa_processo_id, chave, rotulo, fracao, ordem)
            VALUES ({etapaId}, 'nivel_inserido', {rotulo}, {fracao}, 2)
            """);
        (await inserir.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(restricao);
    }

    [Theory]
    [InlineData(0, null, "ck_etapas_processo_peso_positivo")]
    [InlineData(1, -1, "ck_etapas_processo_nota_minima_nao_negativa")]
    public async Task AlterarNotas_ComValorInvalido_BancoRecusa(int peso, int? notaMinima, string restricao)
    {
        Guid etapaId = await GravarEtapaAsync([new("Completo", 1m)]);
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        Func<Task> alterar = () => contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE selecao.etapas_processo SET peso = {peso}, nota_minima = {notaMinima} WHERE id = {etapaId}
            """);
        (await alterar.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(restricao);
    }

    [Fact]
    public async Task AlterarPeriodo_ComInicioPosteriorAoFim_BancoRecusa()
    {
        Guid etapaId = await GravarEtapaAsync([new("Completo", 1m)]);
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        DateTimeOffset inicio = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset fim = inicio.AddDays(-1);
        Func<Task> alterar = () => contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE selecao.etapas_processo SET inicio = {inicio}, fim = {fim} WHERE id = {etapaId}
            """);
        (await alterar.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_etapas_processo_periodo_em_ordem");
    }

    [Fact]
    public async Task AlterarPeriodoDaFase_ComInicioPosteriorAoFim_BancoRecusa()
    {
        Guid faseId = await GravarFaseAsync();
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        DateTimeOffset inicio = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset fim = inicio.AddDays(-1);
        Func<Task> alterar = () => contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE selecao.fases_cronograma SET inicio = {inicio}, fim = {fim} WHERE id = {faseId}
            """);
        (await alterar.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_fases_cronograma_periodo_em_ordem");
    }

    private async Task<Guid> GravarFaseAsync()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo processo = EtapaDeAtendimentoBuilder.CriarProcesso([new("Completo", 1m)], [new(null, "Item", 10m)]);
        FaseCronograma fase = FaseCronogramaBuilder.Criar();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        contexto.Set<ProcessoSeletivo>().Add(processo);
        await contexto.SaveChangesAsync();
        return fase.Id;
    }

    private async Task<Guid> GravarEtapaAsync(IReadOnlyList<NivelDeAtendimento> niveis)
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo processo = EtapaDeAtendimentoBuilder.CriarProcesso(niveis, [new(null, "Item", 10m)]);
        contexto.Set<ProcessoSeletivo>().Add(processo);
        await contexto.SaveChangesAsync();
        return processo.Etapas.Single().Id;
    }

    private static void AtualizarNiveis(EtapaProcesso etapa)
    {
        IdentificacaoDaEtapa identificacao = new IdentificacaoDaEtapa(etapa.Nome, etapa.Carater, etapa.TipoEtapa, etapa.Ordem, etapa.FaseCodigo);
        DeclaracaoDaAvaliacaoDaEtapa avaliacao = new DeclaracaoDaAvaliacaoDaEtapa(etapa.Peso, etapa.NotaMinima, etapa.NotaMaxima,
            etapa.PassoDoLancamento, etapa.FormaDeAvaliacao, etapa.RegraConsolidacao, [new("Não atende", 0m), new("Completo", 1m)]);
        etapa.AtualizarDados(identificacao, avaliacao, [new(etapa.ItensDeAvaliacao.Single().Id, "Item", 10m)])
            .IsSuccess.Should().BeTrue();
    }
}
