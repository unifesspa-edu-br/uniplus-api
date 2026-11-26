namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.CadastroDeReserva;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O cadastro de reserva por chamada no processo seletivo (UNI-REQ-0354, UNI-REQ-0355): a definição substitui a
/// lista inteira, a lista vazia limpa, a recusa não altera o estado e o processo publicado não aceita edição.
/// </summary>
public sealed class CadastroDeReservaDoProcessoTests
{
    private static CadastroDeReservaInformado Chamada(int numero, int pessoasPorVaga) =>
        new(numero, pessoasPorVaga, pessoasPorVaga > 0 ? "ASSUME_NA_MESMA_CHAMADA" : null);

    private static (int Chamada, int Pessoas)[] Declarado(ProcessoSeletivo processo) =>
        [.. processo.CadastroDeReserva.Chamadas.Select(static item => (item.Chamada.Valor, item.PessoasDaReservaPorVaga))];

    [Fact(DisplayName = "O processo novo não tem cadastro de reserva")]
    public void ProcessoNovo_SemReserva()
    {
        NovoProcesso().CadastroDeReserva.Should().Be(CadastroDeReservaPorChamada.SemReserva);
    }

    [Fact(DisplayName = "A definição guarda as chamadas em ordem crescente do número")]
    public void Definir_GuardaEmOrdemCrescente()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.DefinirCadastroDeReserva([Chamada(2, 1), Chamada(1, 0)]).IsSuccess.Should().BeTrue();

        Declarado(processo).Should().Equal((1, 0), (2, 1));
    }

    [Fact(DisplayName = "A nova definição substitui as chamadas anteriores por inteiro")]
    public void Definir_SubstituiAListaInteira()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCadastroDeReserva([Chamada(1, 2), Chamada(2, 1)]);

        processo.DefinirCadastroDeReserva([Chamada(3, 4)]).IsSuccess.Should().BeTrue();

        Declarado(processo).Should().Equal((3, 4));
    }

    [Fact(DisplayName = "A lista vazia limpa o cadastro de reserva")]
    public void ListaVazia_Limpa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCadastroDeReserva([Chamada(1, 2)]);

        processo.DefinirCadastroDeReserva([]).IsSuccess.Should().BeTrue();

        processo.CadastroDeReserva.Should().Be(CadastroDeReservaPorChamada.SemReserva);
    }

    [Fact(DisplayName = "A recusa acumula os erros dos itens e mantém o cadastro anterior")]
    public void Recusa_AcumulaErros_SemAlterarOEstado()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCadastroDeReserva([Chamada(1, 2)]);

        Result resultado = processo.DefinirCadastroDeReserva([Chamada(1, 1), Chamada(1, -1), new(null, 1, "ASSUME_NA_MESMA_CHAMADA")]);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("cadastroDeReserva[1].numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("cadastroDeReserva[1].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("cadastroDeReserva[2].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido));
        Declarado(processo).Should().Equal((1, 2));
    }

    [Fact(DisplayName = "O processo publicado recusa a edição do cadastro de reserva")]
    public void ProcessoPublicado_RecusaEdicao()
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        CenarioDeCongelamento cenario = ApoioDaPublicacaoDoProcessoSeletivo.CenarioDoEdital() with
        {
            Relogio = new RelogioFixo(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
        };
        processo.Publicar(cenario).IsSuccess.Should().BeTrue();

        processo.DefinirCadastroDeReserva([Chamada(1, 2)]).IsFailure.Should().BeTrue();
        processo.CadastroDeReserva.Should().Be(CadastroDeReservaPorChamada.SemReserva);
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "Processo de seleção", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
