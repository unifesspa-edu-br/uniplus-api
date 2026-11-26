namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class ProjecaoDoCadastroDeReservaTests
{
    [Fact(DisplayName = "Sem declaração, a leitura devolve a lista vazia")]
    public void Projetar_SemDeclaracao_DevolveListaVazia() =>
        ProjecaoDoProcessoSeletivo.Projetar(Processo()).CadastroDeReserva.Should().BeEmpty();

    [Fact(DisplayName = "Com declaração, a leitura devolve as chamadas em ordem crescente, com a chamada sem reserva incluída")]
    public void Projetar_ComDeclaracao_DevolveEmOrdemCrescente()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCadastroDeReserva([new(2, 1), new(1, 0)]).IsSuccess.Should().BeTrue();

        ProjecaoDoProcessoSeletivo.Projetar(processo).CadastroDeReserva.Should().Equal(
            new CadastroDeReservaDaChamadaDto(1, 0), new CadastroDeReservaDaChamadaDto(2, 1));
    }

    private static ProcessoSeletivo Processo() => ProcessosDeTeste.SemConfiguracao("Processo de reserva", OrigemCandidatos.InscricaoPropria);
}
