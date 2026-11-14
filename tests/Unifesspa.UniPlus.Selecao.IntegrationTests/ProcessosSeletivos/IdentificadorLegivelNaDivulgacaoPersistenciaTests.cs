namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

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

        await using UniPlusDbContext ctx = _fixture.CreateDbContext();
        CertameDivulgadoRepository repositorio = new(ctx);

        (await repositorio.ObterParaLeituraPorIdentificadorAsync(identificador))!.Id.Should().Be(divulgado.Id);
        (await repositorio.ObterParaLeituraPorIdentificadorAsync(IdentificadoresDeTeste.Novo().Valor))
            .Should().BeNull();
    }

    [Fact(DisplayName = "Duas divulgações com o mesmo identificador: o índice recusa, e é o índice do identificador legível")]
    public async Task MesmoIdentificador_IndiceRecusa()
    {
        string identificador = IdentificadoresDeTeste.Novo().Valor;
        await PersistirAsync(Divulgado(identificador));

        await using UniPlusDbContext ctx = _fixture.CreateDbContext();
        await ctx.CertamesDivulgados.AddAsync(Divulgado(identificador));

        Func<Task> gravar = () => ctx.SaveChangesAsync();
        DbUpdateException falha = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;

        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(falha).Should().Be("ux_certames_divulgados_identificador_legivel");
    }

    [Fact(DisplayName = "Outra violação na gravação da divulgação não é tomada pelo índice do identificador legível")]
    public async Task OutraViolacao_NaoEhOIndiceDoIdentificador()
    {
        CertameDivulgado primeiro = Divulgado(IdentificadoresDeTeste.Novo().Valor);
        await PersistirAsync(primeiro);

        // Mesmo ato criador, identificador distinto: viola o índice do ato, não o do identificador.
        CertameDivulgado mesmoAto = CertameDivulgado.Criar(
            Guid.CreateVersion7(), 1, primeiro.AtoCriadorId, new string('a', 64), "2",
            Facetas(IdentificadoresDeTeste.Novo().Valor), """{"nome":"documento"}""", Agora);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext();
        await ctx.CertamesDivulgados.AddAsync(mesmoAto);

        Func<Task> gravar = () => ctx.SaveChangesAsync();
        DbUpdateException falha = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;

        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(falha).Should().NotBe("ux_certames_divulgados_identificador_legivel");
    }

    private static CertameDivulgado Divulgado(string identificador) => CertameDivulgado.Criar(
        Guid.CreateVersion7(), 1, Guid.CreateVersion7(), new string('a', 64), "2",
        Facetas(identificador), """{"nome":"documento"}""", Agora);

    private static FacetasDoCertameDivulgado Facetas(string identificador) =>
        new(identificador, "Certame", "001/2026", ["AC"], Agora.AddDays(-1), Agora.AddDays(10));

    private async Task PersistirAsync(CertameDivulgado divulgado)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext();
        await ctx.CertamesDivulgados.AddAsync(divulgado);
        await ctx.SaveChangesAsync();
    }
}
