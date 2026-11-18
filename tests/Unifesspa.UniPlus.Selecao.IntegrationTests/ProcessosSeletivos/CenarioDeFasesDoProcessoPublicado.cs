namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem compartilhada dos testes das fases iniciadas (Postgres real): o processo publicado com uma fase e uma
/// etapa, o ato semeado no processo e a leitura das fases iniciadas num instante.
/// </summary>
internal sealed class CenarioDeFasesDoProcessoPublicado
{
    public static readonly DateTimeOffset Abertura = new(2040, 3, 10, 0, 0, 0, TimeSpan.Zero);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public CenarioDeFasesDoProcessoPublicado(ProcessoSeletivoDbFixture fixture) => _fixture = fixture;

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public async Task<(Guid ProcessoId, FaseCronograma Fase, EtapaProcesso Etapa)> PublicadoAsync(string nome, DateTimeOffset? inicioDaFase = null)
    {
        ProcessoSeletivoPublicacaoSeeder.Resultado resultado = await ProcessoSeletivoPublicacaoSeeder.PublicarAsync(_fixture, nome, inicioDaFase);
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await context.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.Etapas)
            .SingleAsync(p => p.Id == resultado.ProcessoId);
        return (processo.Id, processo.CronogramaFases.Single(), processo.Etapas.Single());
    }

    public async Task SemearAtoAsync(Guid processoId, IdentidadeDoItemDoEdital item)
    {
        await using UniPlusDbContext semeadura = _fixture.CreateDbContext();
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, "resultado.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        semeadura.DocumentosAPublicar.Add(documento);
        semeadura.AtosDoProcesso.Add(AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, item.AtoCodigo, "Resultado", Numero: null,
                new DateOnly(2025, 12, 1), "Diretoria", "ceps", IdentificadoresDeTeste.NovoValor(), documento.Id, [], item),
            TimeProvider.System.GetUtcNow()).Value!);
        await semeadura.SaveChangesAsync();
    }

    public async Task<FasesIniciadas> IniciadasAsync(Guid processoId, DateTimeOffset agora, params IFonteDoInicioNoLugar[] fontes)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        return await new FasesIniciadasDoProcesso(context, TimeProvider.System, fontes).ObterAsync(processoId, agora);
    }
}
