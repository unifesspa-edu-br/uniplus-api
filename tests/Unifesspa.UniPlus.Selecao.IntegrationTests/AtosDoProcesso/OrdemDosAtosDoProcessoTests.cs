namespace Unifesspa.UniPlus.Selecao.IntegrationTests.AtosDoProcesso;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// Dois atos do mesmo processo gravados no mesmo microssegundo: a lista do processo põe primeiro o registrado por último,
/// mesmo quando ele tem o identificador menor.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OrdemDosAtosDoProcessoTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly DateTimeOffset MesmoInstante = new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    private readonly InscricaoDbFixture _fixture;

    public OrdemDosAtosDoProcessoTests(InscricaoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Com o mesmo instante de gravação, a lista do processo traz primeiro o ato registrado por último")]
    public async Task MesmoInstante_ListaTrazPrimeiroOUltimoRegistrado()
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"Ordem dos atos {Guid.CreateVersion7()}");
        (Guid idMenor, Guid idMaior) = PorIdentificador(Guid.CreateVersion7(), Guid.CreateVersion7());
        await RegistrarAsync(db, Aviso(idMaior, processo.Id, documento.Id));
        await RegistrarAsync(db, Aviso(idMenor, processo.Id, documento.Id));
        await db.AtosDoProcesso.Where(a => a.ProcessoSeletivoId == processo.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, MesmoInstante));

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        IReadOnlyList<AtoDoProcesso> atos = await new AtoDoProcessoRepository(leitura).ListarDoProcessoAsync(processo.Id);

        atos.Select(a => a.Id).Should().Equal(idMenor, idMaior);
    }

    [Fact(DisplayName = "A ordem de registro do ato não sai no contrato")]
    public void OrdemDeRegistro_ForaDoContrato() =>
        typeof(AtoDoProcessoDto).GetProperties().Select(p => p.Name).Should().NotContain(nameof(AtoDoProcesso.OrdemDeRegistro));

    // O Postgres ordena o uuid pela forma textual, que é a ordem do desempate antigo pelo identificador.
    private static (Guid Menor, Guid Maior) PorIdentificador(Guid um, Guid outro) =>
        string.CompareOrdinal(um.ToString(), outro.ToString()) < 0 ? (um, outro) : (outro, um);

    private static NovoAtoDoProcesso Aviso(Guid atoId, Guid processoId, Guid documentoId) => new(
        atoId, processoId, DadosDoAtoDeTeste.TipoAviso, "Aviso", "1/2027", new DateOnly(2027, 3, 10),
        "Diretor do CEPS", "ceps", "psm-medicina-2027", documentoId, []);

    private static async Task RegistrarAsync(UniPlusDbContext db, NovoAtoDoProcesso novo)
    {
        db.AtosDoProcesso.Add(AtoDoProcesso.Registrar(novo, TimeProvider.System.GetUtcNow()).Value!);
        await db.SaveChangesAsync();
    }
}
