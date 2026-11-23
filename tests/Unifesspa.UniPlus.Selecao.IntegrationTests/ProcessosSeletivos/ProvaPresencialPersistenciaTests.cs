namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ProvaPresencialPersistenciaTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly MunicipioDeProvaInformado[] Municipios = [new("1504208", "PA"), new("1508159", "PA"), new("3550308", "SP")];

    [Fact]
    public async Task Declaracao_Substituicoes_GravaELeNaOrdem()
    {
        Guid id = await GravarAsync();
        await ConferirAsync(id, "1504208", "1508159", "3550308");
        await TrocarAsync(id);
        await ConferirAsync(id, "3550308", "1508159", "1504208");
        await CopiarNomesAsync(id);
        await using UniPlusDbContext leitura = fixture.CreateDbContext();
        ProcessoSeletivo processo = await leitura.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.ProvaPresencial!.Municipios.Select(m => m.Nome).Should().Equal("São Paulo", "Xinguara", "Marabá");
    }

    [Fact]
    public async Task Declaracao_TrueFalseENula_PersisteOsTresEstados()
    {
        Guid id = await GravarAsync();
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.DefinirProvaPresencial(false, [], 100).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.ProvaPresencial!.TemProvaPresencial.Should().BeFalse();
        processo.ProvaPresencial.Municipios.Should().BeEmpty();
        await ConferirColunaSemProvaAsync(db, id);
        db.Entry(processo).Reference(p => p.ProvaPresencial).CurrentValue = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        (await db.ProcessosSeletivos.SingleAsync(p => p.Id == id)).ProvaPresencial.Should().BeNull();
    }

    private static async Task ConferirColunaSemProvaAsync(UniPlusDbContext db, Guid id)
    {
        await db.Database.OpenConnectionAsync();
        await using System.Data.Common.DbCommand comando = db.Database.GetDbConnection().CreateCommand();
        comando.CommandText = "select tem_prova_presencial, (select count(*) from selecao.municipios_de_prova m where m.processo_seletivo_id = p.id) from selecao.processos_seletivos p where p.id = @id";
        System.Data.Common.DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = "id";
        parametro.Value = id;
        comando.Parameters.Add(parametro);
        await using System.Data.Common.DbDataReader leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();
        leitor.GetBoolean(0).Should().BeFalse();
        leitor.GetInt64(1).Should().Be(0);
    }

    private async Task<Guid> GravarAsync()
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirProvaPresencial(true, Municipios, 100).IsSuccess.Should().BeTrue();
        db.ProcessosSeletivos.Add(processo);
        await db.SaveChangesAsync();
        return processo.Id;
    }

    private async Task TrocarAsync(Guid id)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.DefinirProvaPresencial(true, [.. Municipios.Reverse()], 100).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task CopiarNomesAsync(Guid id)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        MunicipiosConferidosNoGeo geo = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            ["1504208"] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
            ["1508159"] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara"),
            ["3550308"] = ResultadoDoMunicipioNoGeo.Conferido("São Paulo"),
        });
        db.Entry(processo).Reference(p => p.ProvaPresencial).CurrentValue = processo.ProvaPresencial!.ComNomesDoGeo(geo);
        await db.SaveChangesAsync();
    }

    private async Task ConferirAsync(Guid id, params string[] codigos)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.ProvaPresencial!.Municipios.Select(m => m.CodigoIbge).Should().Equal(codigos);
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "Processo com prova presencial", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
