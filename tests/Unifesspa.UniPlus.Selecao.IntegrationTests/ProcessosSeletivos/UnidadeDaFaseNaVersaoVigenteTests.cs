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

    [Fact(DisplayName = "O ato do item que a fase declara faz a fase contar como iniciada antes da janela")]
    public async Task Iniciadas_ContaPeloAtoDoItemDaFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS ato da fase");
        await SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de uma etapa faz a fase que a contém contar como iniciada")]
    public async Task Iniciadas_ContaPeloAtoDaEtapaDaFase()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS ato da etapa");
        await SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaEtapa(etapa, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de um item de outra fase com o mesmo tipo não inicia a fase")]
    public async Task Iniciadas_AtoDeOutraFaseNaoIniciaAFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS ato de outra fase");
        (_, FaseCronograma faseDeOutroProcesso, _) = await PublicadoAsync("PS outra fase");
        IdentidadeDoItemDoEdital deOutraFase = IdentidadeDoItemDoEdital.DaFase(faseDeOutroProcesso, fase.Produtos.Single());
        await SemearAtoAsync(processoId, deOutraFase);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "A avaliação já lançada numa etapa da fase faz a fase contar como iniciada")]
    public async Task Iniciadas_ContaPelaAvaliacaoLancadaNaEtapa()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS avaliacao da etapa");
        (Guid outroProcessoId, FaseCronograma outraFase, _) = await PublicadoAsync("PS avaliacao de outra etapa");
        await SemearAvaliacaoAsync(etapa.Id);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));
        FasesIniciadas deOutroProcesso = await IniciadasAsync(outroProcessoId, outraFase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        deOutroProcesso.Contem(outraFase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    // As chaves estrangeiras do lote e da inscrição ficam fora da semeadura: o que se verifica é só a leitura.
    private async Task SemearAvaliacaoAsync(Guid etapaId)
    {
        await using SelecaoDbContext semeadura = _fixture.CreateDbContext();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transacao = await semeadura.Database.BeginTransactionAsync();
        await semeadura.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica");
        await semeadura.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO selecao.avaliacoes_da_etapa (id, lote_id, etapa_processo_id, inscricao_id, oferta_curso_id, grupo_area_enem_codigo, situacao, nota)
            VALUES ({Guid.CreateVersion7()}, {Guid.CreateVersion7()}, {etapaId}, {Guid.CreateVersion7()}, {Guid.CreateVersion7()}, 'TECNOLOGICA', 1, 600.5)
            """);
        await transacao.CommitAsync();
    }

    private async Task<FasesIniciadas> IniciadasAsync(Guid processoId, DateTimeOffset agora)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        return await new FasesIniciadasDoProcesso(context).ObterAsync(processoId, agora);
    }

    private async Task SemearAtoAsync(Guid processoId, IdentidadeDoItemDoEdital item)
    {
        await using SelecaoDbContext semeadura = _fixture.CreateDbContext();
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, "resultado.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        semeadura.DocumentosAPublicar.Add(documento);
        semeadura.AtosDoProcesso.Add(AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, item.AtoCodigo, "Resultado", Numero: null,
                new DateOnly(2025, 12, 1), "Diretoria", "ceps", IdentificadoresDeTeste.NovoValor(), documento.Id, [], item),
            TimeProvider.System).Value!);
        await semeadura.SaveChangesAsync();
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
