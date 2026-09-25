namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O identificador legível na divulgação, contra o Postgres real: a busca por ele e a defesa do
/// índice único quando dois certames divulgados disputariam o mesmo endereço público.
/// </summary>
public sealed class IdentificadorLegivelNaDivulgacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public IdentificadorLegivelNaDivulgacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A leitura pelo identificador encontra a divulgação que o traz, e só ela")]
    public async Task ObterParaLeituraPorIdentificador_EncontraADivulgacao()
    {
        string identificador = IdentificadoresDeTeste.Novo().Valor;
        CertameDivulgado divulgado = Divulgado(identificador);
        await PersistirAsync(divulgado);

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        CertameDivulgadoRepository repositorio = new(ctx);

        (await repositorio.ObterParaLeituraPorIdentificadorAsync(identificador))!.Id.Should().Be(divulgado.Id);
        (await repositorio.ObterParaLeituraPorIdentificadorAsync(IdentificadoresDeTeste.Novo().Valor))
            .Should().BeNull();
    }

    [Fact(DisplayName = "Duas divulgações com o mesmo identificador: o índice recusa, e a recusa vira o conflito nomeado")]
    public async Task MesmoIdentificador_IndiceRecusa_EViraConflitoNomeado()
    {
        string identificador = IdentificadoresDeTeste.Novo().Valor;
        await PersistirAsync(Divulgado(identificador));

        CertameDivulgado disputante = Divulgado(identificador);
        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        await ctx.CertamesDivulgados.AddAsync(disputante);

        Func<Task> gravar = () => ctx.SaveChangesAsync();
        DbUpdateException falha = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;

        IdentificadorLegivelJaDivulgadoException.EhViolacaoDoIndice(falha).Should().BeTrue();

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        CertameDivulgadoRepository repositorio = new(leitura);
        CertameDivulgado? dono = await repositorio.ObterParaLeituraPorIdentificadorAsync(identificador);

        IdentificadorLegivelJaDivulgadoException? conflito = IdentificadorLegivelJaDivulgadoException.Classificar(
            falha, disputante.Id, dono?.Id, identificador);

        conflito.Should().NotBeNull("o identificador já é o endereço de OUTRO processo, e nenhuma reentrega corrige isso");
        conflito!.Message.Should().Contain(identificador).And.Contain(disputante.Id.ToString());
        conflito.InnerException.Should().BeSameAs(falha);
    }

    [Fact(DisplayName = "Outra violação na gravação da divulgação não é tomada pelo conflito de identificador")]
    public async Task OutraViolacao_NaoEhConflitoDeIdentificador()
    {
        CertameDivulgado primeiro = Divulgado(IdentificadoresDeTeste.Novo().Valor);
        await PersistirAsync(primeiro);

        // Mesmo ato criador, identificador distinto: viola o índice do ato, não o do identificador.
        CertameDivulgado mesmoAto = CertameDivulgado.Criar(
            Guid.CreateVersion7(), 1, primeiro.AtoCriadorId, new string('a', 64), "2",
            Facetas(IdentificadoresDeTeste.Novo().Valor), """{"nome":"documento"}""", Agora);

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        await ctx.CertamesDivulgados.AddAsync(mesmoAto);

        Func<Task> gravar = () => ctx.SaveChangesAsync();
        DbUpdateException falha = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;

        IdentificadorLegivelJaDivulgadoException.EhViolacaoDoIndice(falha).Should().BeFalse();
    }

    [Fact(DisplayName = "Identificador já na linha do MESMO processo é corrida entre entregas, não conflito terminal")]
    public async Task MesmoProcesso_NaoEhConflitoTerminal()
    {
        string identificador = IdentificadoresDeTeste.Novo().Valor;
        CertameDivulgado existente = Divulgado(identificador);
        await PersistirAsync(existente);

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        CertameDivulgadoRepository repositorio = new(leitura);
        CertameDivulgado? dono = await repositorio.ObterParaLeituraPorIdentificadorAsync(identificador);

        // Duas versões do mesmo processo divulgadas em paralelo: a segunda pode esbarrar no índice
        // antes da chave primária. A linha é dela mesma — a reentrega avança a divulgação.
        IdentificadorLegivelJaDivulgadoException.Classificar(
                new InvalidOperationException("violação simulada"), existente.Id, dono?.Id, identificador)
            .Should().BeNull();

        IdentificadorLegivelJaDivulgadoException.Classificar(
                new InvalidOperationException("violação simulada"), existente.Id, processoDonoDoIdentificador: null, identificador)
            .Should().BeNull("a linha que causou a violação já não existe: reentregar resolve");
    }

    private static CertameDivulgado Divulgado(string identificador) => CertameDivulgado.Criar(
        Guid.CreateVersion7(), 1, Guid.CreateVersion7(), new string('a', 64), "2",
        Facetas(identificador), """{"nome":"documento"}""", Agora);

    private static FacetasDoCertameDivulgado Facetas(string identificador) =>
        new(identificador, "Certame", "001/2026", ["AC"], Agora.AddDays(-1), Agora.AddDays(10));

    private async Task PersistirAsync(CertameDivulgado divulgado)
    {
        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        await ctx.CertamesDivulgados.AddAsync(divulgado);
        await ctx.SaveChangesAsync();
    }
}
