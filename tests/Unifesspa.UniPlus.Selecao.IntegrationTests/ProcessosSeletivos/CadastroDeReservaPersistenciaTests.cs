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
    private const string Assume = "ASSUME_NA_MESMA_CHAMADA";

    [Fact(DisplayName = "O cadastro de reserva é gravado e lido em ordem crescente do número, com a chamada sem reserva incluída")]
    public async Task Cadastro_GravaELeEmOrdemCrescente()
    {
        Guid id = await GravarAsync([new(3, 1, Assume), new(1, 0, null), new(2, 2, Assume)]);

        await ConferirAsync(id, (1, 0, null), (2, 2, ParticipacaoDaReserva.AssumeNaMesmaChamada), (3, 1, ParticipacaoDaReserva.AssumeNaMesmaChamada));
    }

    [Fact(DisplayName = "Cada chamada guarda a própria participação da reserva, e a chamada sem reserva fica sem ela")]
    public async Task Cadastro_GuardaAParticipacaoDeCadaChamada()
    {
        Guid id = await GravarAsync([new(1, 0, null), new(2, 2, "ANTECIPA_A_HABILITACAO"), new(3, 1, Assume)]);

        await ConferirAsync(
            id,
            (1, 0, null),
            (2, 2, ParticipacaoDaReserva.AntecipaAHabilitacao),
            (3, 1, ParticipacaoDaReserva.AssumeNaMesmaChamada));
    }

    [Fact(DisplayName = "A nova definição substitui a lista, inclusive com o mesmo número de chamada, sem violar o índice único")]
    public async Task Cadastro_SubstituiMantendoONumeroDaChamada()
    {
        Guid id = await GravarAsync([new(1, 2, Assume), new(2, 3, Assume)]);

        await DefinirAsync(id, [new(2, 5, "ANTECIPA_A_HABILITACAO"), new(4, 1, Assume)]);

        await ConferirAsync(id, (2, 5, ParticipacaoDaReserva.AntecipaAHabilitacao), (4, 1, ParticipacaoDaReserva.AssumeNaMesmaChamada));
    }

    [Fact(DisplayName = "A lista vazia remove todas as linhas e o processo volta a não ter reserva")]
    public async Task Cadastro_ListaVazia_RemoveAsLinhas()
    {
        Guid id = await GravarAsync([new(1, 2, Assume)]);

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

    private async Task ConferirAsync(Guid id, params (int Chamada, int Pessoas, ParticipacaoDaReserva? Participacao)[] esperadas)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.CadastroDeReserva.Chamadas.Select(static c => (c.Chamada.Valor, c.PessoasDaReservaPorVaga, c.Participacao)).Should().Equal(esperadas);
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "Processo com cadastro de reserva", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
