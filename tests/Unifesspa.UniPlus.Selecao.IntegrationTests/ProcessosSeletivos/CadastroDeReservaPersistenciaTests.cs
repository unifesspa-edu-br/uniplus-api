namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CadastroDeReservaPersistenciaTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact(DisplayName = "O cadastro de reserva é gravado e lido em ordem crescente do número, com a chamada sem reserva incluída")]
    public async Task Cadastro_GravaELeEmOrdemCrescente()
    {
        Guid id = await GravarAsync([new(3, 1), new(1, 0), new(2, 2)]);

        await ConferirAsync(id, (1, 0), (2, 2), (3, 1));
    }

    [Fact(DisplayName = "A nova definição substitui a lista, inclusive com o mesmo número de chamada, sem violar o índice único")]
    public async Task Cadastro_SubstituiMantendoONumeroDaChamada()
    {
        Guid id = await GravarAsync([new(1, 2), new(2, 3)]);

        await DefinirAsync(id, [new(2, 5), new(4, 1)]);

        await ConferirAsync(id, (2, 5), (4, 1));
    }

    [Fact(DisplayName = "A lista vazia remove todas as linhas e o processo volta a não ter reserva")]
    public async Task Cadastro_ListaVazia_RemoveAsLinhas()
    {
        Guid id = await GravarAsync([new(1, 2)]);

        await DefinirAsync(id, []);

        await ConferirAsync(id);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        (await db.ProcessosSeletivos.SingleAsync(p => p.Id == id)).CadastroDeReserva.Should().Be(CadastroDeReservaPorChamada.SemReserva);
    }

    private async Task<Guid> GravarAsync(IReadOnlyList<CadastroDeReservaInformado?> chamadas)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCadastroDeReserva(chamadas).IsSuccess.Should().BeTrue();
        db.ProcessosSeletivos.Add(processo);
        await db.SaveChangesAsync();
        return processo.Id;
    }

    private async Task DefinirAsync(Guid id, IReadOnlyList<CadastroDeReservaInformado?> chamadas)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.DefinirCadastroDeReserva(chamadas).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task ConferirAsync(Guid id, params (int Chamada, int Pessoas)[] esperadas)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.CadastroDeReserva.Chamadas.Select(static c => (c.Chamada.Valor, c.PessoasDaReservaPorVaga)).Should().Equal(esperadas);
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "Processo com cadastro de reserva", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
