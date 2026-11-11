namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A unidade da fase e a das fases iniciadas, lidas do envelope congelado da versão vigente num Postgres real.
/// </summary>
public sealed class UnidadeDaFaseNaVersaoVigenteTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public UnidadeDaFaseNaVersaoVigenteTests(ProcessoSeletivoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A fase publicada resolve a unidade congelada na versão vigente")]
    public async Task DaFase_ResolveAUnidadeDaVersaoVigente()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS unidade da fase");
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        UnidadeDaFaseNaVersaoVigente leitor = new(context);

        UnidadeDaFase? daFase = await leitor.DaFaseAsync(processoId, fase.Id);
        UnidadeDaFase? daEtapa = await leitor.DaEtapaAsync(processoId, etapa.Id);

        daFase.Should().Be(new UnidadeDaFase(fase.Id, UnidadesDeTeste.Ceps.OrigemId));
        daEtapa.Should().Be(daFase, "a etapa segue a unidade da fase que a contém");
    }

    [Fact(DisplayName = "Fase ou etapa inexistente, de outro processo ou de processo sem versão não resolvem unidade")]
    public async Task NaoResolve_FaseOuEtapaQueNaoSeEncontra()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS fase inexistente");
        (Guid outroProcessoId, _, _) = await PublicadoAsync("PS outro processo");
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        UnidadeDaFaseNaVersaoVigente leitor = new(context);

        (await leitor.DaFaseAsync(processoId, Guid.CreateVersion7())).Should().BeNull();
        (await leitor.DaEtapaAsync(processoId, Guid.CreateVersion7())).Should().BeNull();
        (await leitor.DaFaseAsync(outroProcessoId, fase.Id)).Should().BeNull("a fase pertence a outro processo");
        (await leitor.DaEtapaAsync(outroProcessoId, etapa.Id)).Should().BeNull("a etapa pertence a outro processo");
        (await leitor.DaFaseAsync(Guid.CreateVersion7(), fase.Id)).Should().BeNull("o processo não existe");
    }

    [Fact(DisplayName = "A fase só começa a contar como iniciada quando a janela publicada começou")]
    public async Task Iniciadas_ContaPelaJanelaPublicada()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS janela da fase");
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        FasesIniciadasDoProcesso leitor = new(context);

        FasesIniciadas antes = await leitor.ObterAsync(processoId, fase.Inicio!.Value.AddDays(-1));
        FasesIniciadas depois = await leitor.ObterAsync(processoId, fase.Inicio!.Value.AddDays(1));
        FasesIniciadas semProcesso = await leitor.ObterAsync(Guid.CreateVersion7(), fase.Inicio!.Value.AddDays(1));

        antes.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
        depois.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        semProcesso.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "O ato do tipo que a fase declara faz a fase contar como iniciada antes da janela")]
    public async Task Iniciadas_ContaPeloAtoDoTipoDaFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS ato da fase");
        DateTimeOffset antesDaJanela = fase.Inicio!.Value.AddDays(-30);
        await using (SelecaoDbContext semeadura = _fixture.CreateDbContext())
        {
            DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
                processoId, FinalidadeDoDocumento.DocumentoDeAto, "resultado.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
            documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
            semeadura.DocumentosAPublicar.Add(documento);
            semeadura.AtosDoProcesso.Add(AtoDoProcesso.Registrar(
                new NovoAtoDoProcesso(
                    Guid.CreateVersion7(), processoId, fase.Produtos.Single().AtoCodigo, "Resultado", Numero: null,
                    new DateOnly(2025, 12, 1), "Diretoria", "ceps", IdentificadoresDeTeste.NovoValor(), documento.Id, []),
                TimeProvider.System).Value!);
            await semeadura.SaveChangesAsync();
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        FasesIniciadas iniciadas = await new FasesIniciadasDoProcesso(context).ObterAsync(processoId, antesDaJanela);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    private async Task<(Guid ProcessoId, FaseCronograma Fase, EtapaProcesso Etapa)> PublicadoAsync(string nome)
    {
        ProcessoSeletivoPublicacaoSeeder.Resultado resultado = await ProcessoSeletivoPublicacaoSeeder.PublicarAsync(_fixture, nome);
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await context.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.Etapas)
            .SingleAsync(p => p.Id == resultado.ProcessoId);
        return (processo.Id, processo.CronogramaFases.Single(), processo.Etapas.Single());
    }
}
