namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using static RascunhosSemeadosParaTeste;

/// <summary>
/// O bloqueio da linha do rascunho e o da linha do processo, ambos com espera limitada: o exclusivo espera qualquer outro, o
/// compartilhado convive com outro compartilhado, e o limite de espera da transação é devolvido depois do bloqueio.
/// </summary>
public sealed class BloqueioDoRascunhoDeProcessoSeletivoTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public BloqueioDoRascunhoDeProcessoSeletivoTests(ProcessoSeletivoDbFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact(DisplayName = "O bloqueio exclusivo do rascunho retido além do limite lança a espera esgotada")]
    public async Task ObterParaMutacao_BloqueioExclusivoRetido_LancaEsperaEsgotada()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        await using TransacaoQueRetemBloqueios retida = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await retida.Repositorio.ObterParaMutacaoAsync(id);
        await using TransacaoQueRetemBloqueios espera = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);

        Func<Task> bloquear = () => espera.Repositorio.ObterParaMutacaoAsync(id);

        (await bloquear.Should().ThrowAsync<EsperaPelaTravaEsgotadaException>()).Which.InnerException
            .Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(PostgresErrorCodes.LockNotAvailable);
    }

    [Fact(DisplayName = "Dois bloqueios compartilhados do mesmo rascunho convivem e as duas encontram o rascunho")]
    public async Task BloquearCompartilhado_DuasTransacoes_Convivem()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        await using TransacaoQueRetemBloqueios primeira = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await using TransacaoQueRetemBloqueios segunda = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);

        bool daPrimeira = await primeira.Repositorio.BloquearCompartilhadoAsync(id);
        bool daSegunda = await segunda.Repositorio.BloquearCompartilhadoAsync(id);

        (daPrimeira, daSegunda).Should().Be((true, true));
    }

    [Fact(DisplayName = "O bloqueio compartilhado espera o exclusivo e é recusado ao fim do limite")]
    public async Task BloquearCompartilhado_BloqueioExclusivoRetido_LancaEsperaEsgotada()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        await using TransacaoQueRetemBloqueios retida = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await retida.Repositorio.ObterParaMutacaoAsync(id);
        await using TransacaoQueRetemBloqueios espera = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);

        Func<Task> bloquear = () => espera.Repositorio.BloquearCompartilhadoAsync(id);

        await bloquear.Should().ThrowAsync<EsperaPelaTravaEsgotadaException>();
    }

    [Fact(DisplayName = "O bloqueio exclusivo espera o compartilhado e é recusado ao fim do limite")]
    public async Task ObterParaMutacao_BloqueioCompartilhadoRetido_LancaEsperaEsgotada()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        await using TransacaoQueRetemBloqueios retida = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await retida.Repositorio.BloquearCompartilhadoAsync(id);
        await using TransacaoQueRetemBloqueios espera = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);

        Func<Task> bloquear = () => espera.Repositorio.ObterParaMutacaoAsync(id);

        await bloquear.Should().ThrowAsync<EsperaPelaTravaEsgotadaException>();
    }

    [Fact(DisplayName = "Liberado o bloqueio, a operação que esperava obtém o rascunho")]
    public async Task ObterParaMutacao_DepoisDaLiberacao_ObtemORascunho()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        await using TransacaoQueRetemBloqueios retida = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await retida.Repositorio.ObterParaMutacaoAsync(id);
        await using TransacaoQueRetemBloqueios espera = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await retida.ConfirmarAsync();

        RascunhoDeProcessoSeletivo? obtido = await espera.Repositorio.ObterParaMutacaoAsync(id);

        obtido.Should().NotBeNull();
    }

    [Fact(DisplayName = "Rascunho que não existe: o bloqueio compartilhado devolve falso e o de mutação devolve nulo, sem erro")]
    public async Task RascunhoInexistente_NaoEncontra()
    {
        await using TransacaoQueRetemBloqueios transacao = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);

        (await transacao.Repositorio.BloquearCompartilhadoAsync(Guid.CreateVersion7())).Should().BeFalse();
        (await transacao.Repositorio.ObterParaMutacaoAsync(Guid.CreateVersion7())).Should().BeNull();
    }

    [Fact(DisplayName = "Depois do bloqueio do rascunho, o limite de espera que a transação já tinha é devolvido")]
    public async Task BloqueioDoRascunho_DevolveOLimiteQueATransacaoJaTinha()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        await using TransacaoQueRetemBloqueios transacao = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await transacao.DefinirLimiteDaEsperaAsync("5s");

        await transacao.Repositorio.ObterParaMutacaoAsync(id);

        (await transacao.LimiteDaEsperaAsync()).Should().Be("5s");
    }

    [Fact(DisplayName = "A trava do processo segue com o mesmo comportamento: retida além do limite, lança a espera esgotada")]
    public async Task TravarAsync_DoProcesso_TravaRetida_LancaEsperaEsgotada()
    {
        ProcessoSeletivo processo = await SemearProcessoAsync(_fixture, Guid.CreateVersion7());
        await using TransacaoQueRetemBloqueios retida = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        await retida.Contexto.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM selecao.processos_seletivos WHERE id = {processo.Id} FOR UPDATE");
        await using TransacaoQueRetemBloqueios espera = await TransacaoQueRetemBloqueios.IniciarAsync(_fixture);
        ProcessoSeletivoRepository repositorioDoProcesso = new(espera.Contexto, TimeProvider.System, Options.Create(new TravaDoProcessoOptions { EsperaMaxima = EsperaCurta }));

        Func<Task> travar = () => repositorioDoProcesso.TravarAsync(processo.Id);

        await travar.Should().ThrowAsync<EsperaPelaTravaEsgotadaException>();
    }
}
