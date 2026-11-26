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
        processo.DefinirCadastroDeReserva([new(2, 1, "ASSUME_NA_MESMA_CHAMADA"), new(1, 0, null)]).IsSuccess.Should().BeTrue();

        ProjecaoDoProcessoSeletivo.Projetar(processo).CadastroDeReserva.Select(static c => (c.NumeroDaChamada, c.PessoasDaReservaPorVaga))
            .Should().Equal((1, 0), (2, 1));
    }

    [Fact(DisplayName = "A leitura traz a participação da reserva com o rótulo na chamada com reserva e nada na chamada sem reserva")]
    public void Projetar_ComParticipacao_TrazCodigoERotulo()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCadastroDeReserva([new(1, 0, null), new(2, 2, "ANTECIPA_A_HABILITACAO"), new(3, 1, "ASSUME_NA_MESMA_CHAMADA")]).IsSuccess.Should().BeTrue();

        IReadOnlyList<CadastroDeReservaDaChamadaDto> leitura = ProjecaoDoProcessoSeletivo.Projetar(processo).CadastroDeReserva;

        leitura.Select(static c => c.ParticipacaoDaReserva is { } p ? (p.Codigo, p.Rotulo) : default).Should().Equal(
            default,
            ("ANTECIPA_A_HABILITACAO", "A reserva só antecipa a habilitação"),
            ("ASSUME_NA_MESMA_CHAMADA", "A reserva assume na mesma chamada"));
    }

    private static ProcessoSeletivo Processo() => ProcessosDeTeste.SemConfiguracao("Processo de reserva", OrigemCandidatos.InscricaoPropria);
}
