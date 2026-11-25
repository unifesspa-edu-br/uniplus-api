namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.FaseDoCronogramaPublicavelDeTeste;

/// <summary>
/// A coluna da marca "no mesmo momento da anterior" em Postgres real: o que foi gravado é lido de volta na posição de cada fase, e o
/// banco a guarda como booleano obrigatório com valor padrão falso.
/// </summary>
public sealed class ColunaDaMarcaNoMesmoMomentoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact(DisplayName = "A marca gravada em cada fase é lida de volta na ordem da fase, marcada ou não")]
    public async Task MarcaGravada_ELidaNaOrdemDaFase()
    {
        Guid processoId = await SemearProcessoAsync(Criar("INSCRICAO", 1, 1), Criar("HOMOLOGACAO_INSCRICOES", 2, 2, noMesmoMomentoDaAnterior: true), Criar("RESULTADO_FINAL", 3, 3));

        await using UniPlusDbContext leitura = fixture.CreateDbContext();
        List<FaseCronograma> fases = await leitura.Set<FaseCronograma>().AsNoTracking()
            .Where(f => f.ProcessoSeletivoId == processoId).OrderBy(f => f.Ordem).ToListAsync();

        fases.Select(static f => (f.Ordem, f.NoMesmoMomentoDaAnterior)).Should().Equal((1, false), (2, true), (3, false));
    }

    [Fact(DisplayName = "No banco a coluna é booleana, obrigatória e tem o valor padrão falso")]
    public async Task Coluna_EBooleanaObrigatoriaComPadraoFalso()
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();

        List<string> definicao = await db.Database.SqlQuery<string>($"""
            SELECT data_type || ' ' || is_nullable || ' ' || column_default AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'selecao' AND table_name = 'fases_cronograma' AND column_name = 'no_mesmo_momento_da_anterior'
            """).ToListAsync();

        definicao.Should().Equal("boolean NO false");
    }

    private async Task<Guid> SemearProcessoAsync(params FaseCronograma[] fases)
    {
        await using UniPlusDbContext escrita = fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Certame da marca {Guid.CreateVersion7()}",
            TiposDeProcessoDeTeste.Sisu,
            OrigemCandidatos.InscricaoPropria,
            Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(fases, []).IsSuccess.Should().BeTrue();
        escrita.ProcessosSeletivos.Add(processo);
        await escrita.SaveChangesAsync();

        return processo.Id;
    }
}
