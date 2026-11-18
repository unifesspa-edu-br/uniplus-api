namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.CadastroDeReserva;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// A declaração do cadastro de reserva por chamada (UNI-REQ-0354, UNI-REQ-0355): cada chamada declara um
/// número a partir de 1, uma só vez, e ao menos uma pessoa da reserva por vaga. A chamada sem declaração não
/// tem reserva, e a declaração guarda as chamadas em ordem crescente do número.
/// </summary>
public sealed class CadastroDeReservaPorChamadaTests
{
    private static CadastroDeReservaInformado Chamada(int? numero, int? pessoasPorVaga) => new(numero, pessoasPorVaga);

    private static CadastroDeReservaPorChamada Declarada(params CadastroDeReservaInformado?[] chamadas) =>
        CadastroDeReservaPorChamada.Declarar(chamadas).Value!;

    private static (string Campo, string Codigo)[] Recusas(params CadastroDeReservaInformado?[] chamadas) =>
        [.. CadastroDeReservaPorChamada.Declarar(chamadas).Errors.Select(static erro => (erro.Field!, erro.Error.Code))];

    [Fact(DisplayName = "A declaração sem chamada é válida e é o processo sem cadastro de reserva")]
    public void SemChamada_E_SemReserva()
    {
        Result<CadastroDeReservaPorChamada> resultado = CadastroDeReservaPorChamada.Declarar([]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Chamadas.Should().BeEmpty();
        resultado.Value.Should().Be(CadastroDeReservaPorChamada.SemReserva);
    }

    [Fact(DisplayName = "As chamadas declaradas ficam em ordem crescente do número, ainda que informadas fora de ordem")]
    public void Chamadas_ForaDeOrdem_FicamEmOrdemNumerica()
    {
        CadastroDeReservaPorChamada declaracao = Declarada(Chamada(2, 1), Chamada(1, 2));

        declaracao.Chamadas.Select(static cadastro => (cadastro.Chamada.Valor, cadastro.PessoasDaReservaPorVaga))
            .Should().Equal((1, 2), (2, 1));
    }

    [Fact(DisplayName = "Só a segunda chamada pode ter cadastro de reserva: a primeira fica sem declaração")]
    public void SoASegundaChamada_Aceita()
    {
        CadastroDeReservaPorChamada declaracao = Declarada(Chamada(2, 1));

        declaracao.Chamadas.Should().ContainSingle().Which.Chamada.Valor.Should().Be(2);
    }

    [Fact(DisplayName = "Uma pessoa da reserva por vaga é a menor quantidade aceita")]
    public void UmaPessoaPorVaga_Aceita()
    {
        Declarada(Chamada(1, 1)).Chamadas.Should().ContainSingle().Which.PessoasDaReservaPorVaga.Should().Be(1);
    }

    [Theory(DisplayName = "A quantidade ausente, zero ou negativa é recusada na posição da chamada, sem repetir o valor")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public void Quantidade_AusenteOuMenorQueUm_Recusa(int? pessoasPorVaga)
    {
        Result<CadastroDeReservaPorChamada> resultado = CadastroDeReservaPorChamada.Declarar([Chamada(1, 2), Chamada(2, pessoasPorVaga)]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "cadastroDeReserva[1].pessoasDaReservaPorVaga",
            Error = new
            {
                Code = CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida,
                Message = "A chamada com cadastro de reserva convoca pelo menos uma pessoa da reserva por vaga; para não ter reserva, deixe a chamada sem declaração.",
            },
        });
    }

    [Theory(DisplayName = "O número ausente, zero ou negativo é recusado na posição da chamada")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Numero_AusenteOuMenorQueUm_Recusa(int? numero)
    {
        Recusas(Chamada(numero, 1)).Should().Equal(("cadastroDeReserva[0].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido));
    }

    [Fact(DisplayName = "A chamada repetida recusa só a segunda ocorrência, ainda que a quantidade seja outra")]
    public void ChamadaRepetida_RecusaSoASegunda()
    {
        Result<CadastroDeReservaPorChamada> resultado = CadastroDeReservaPorChamada.Declarar([Chamada(1, 2), Chamada(1, 3)]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "cadastroDeReserva[1].numeroDaChamada",
            Error = new
            {
                Code = CadastroDeReservaErrorCodes.ChamadaRepetida,
                Message = "Esta chamada já tem cadastro de reserva declarado; declare cada chamada uma só vez.",
            },
        });
    }

    [Fact(DisplayName = "O item nulo é recusado no número e na quantidade")]
    public void ItemNulo_RecusaNumeroEQuantidade()
    {
        Recusas([null]).Should().Equal(
            ("cadastroDeReserva[0].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("cadastroDeReserva[0].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida));
    }

    [Fact(DisplayName = "O número inválido não entra na conferência de repetição: dois zeros dão duas recusas de número e nenhuma de repetição")]
    public void DoisNumerosInvalidos_NaoSaoRepeticao()
    {
        Recusas(Chamada(0, 1), Chamada(0, 1)).Should().Equal(
            ("cadastroDeReserva[0].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("cadastroDeReserva[1].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido));
    }

    [Fact(DisplayName = "As recusas de vários itens se acumulam na ordem do índice, e em cada item o número vem antes da quantidade")]
    public void Recusas_DeVariosItens_AcumulamNaOrdemDoIndice()
    {
        Recusas(Chamada(1, 2), Chamada(1, 0), Chamada(null, 1), Chamada(2, null), Chamada(-1, -1)).Should().Equal(
            ("cadastroDeReserva[1].numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("cadastroDeReserva[1].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("cadastroDeReserva[2].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("cadastroDeReserva[3].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("cadastroDeReserva[4].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("cadastroDeReserva[4].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida));
    }

    [Fact(DisplayName = "As declarações das mesmas chamadas em ordens diferentes são iguais e têm o mesmo hash")]
    public void OrdensDiferentes_ProduzemDeclaracoesIguais()
    {
        CadastroDeReservaPorChamada emOrdem = Declarada(Chamada(1, 2), Chamada(2, 1));
        CadastroDeReservaPorChamada foraDeOrdem = Declarada(Chamada(2, 1), Chamada(1, 2));

        foraDeOrdem.Should().Be(emOrdem);
        foraDeOrdem.GetHashCode().Should().Be(emOrdem.GetHashCode());
    }

    [Fact(DisplayName = "As declarações com quantidades ou chamadas diferentes não são iguais")]
    public void DeclaracoesDiferentes_NaoSaoIguais()
    {
        CadastroDeReservaPorChamada declaracao = Declarada(Chamada(1, 2));

        declaracao.Should().NotBe(Declarada(Chamada(1, 3)));
        declaracao.Should().NotBe(Declarada(Chamada(2, 2)));
        declaracao.Should().NotBe(CadastroDeReservaPorChamada.SemReserva);
    }
}
