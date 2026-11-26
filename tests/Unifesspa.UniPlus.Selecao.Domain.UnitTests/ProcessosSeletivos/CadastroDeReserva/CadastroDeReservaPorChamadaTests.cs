namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.CadastroDeReserva;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// A declaração do cadastro de reserva por chamada (UNI-REQ-0354, UNI-REQ-0355): cada chamada declara um
/// número a partir de 1, uma só vez, e quantas pessoas da reserva convoca por vaga, a partir de zero. A chamada
/// sem declaração, ou declarada com zero, não tem reserva, e a declaração guarda as chamadas em ordem crescente do número. A chamada
/// com reserva escolhe a participação da reserva (UNI-REQ-0505), sem valor padrão, e a chamada sem reserva não a tem.
/// </summary>
public sealed class CadastroDeReservaPorChamadaTests
{
    private const string AntecipaAHabilitacao = "ANTECIPA_A_HABILITACAO";
    private const string AssumeNaMesmaChamada = "ASSUME_NA_MESMA_CHAMADA";

    // A chamada com reserva leva a participação que a regra exige; a chamada sem reserva, nenhuma.
    private static CadastroDeReservaInformado Chamada(int? numero, int? pessoasPorVaga) =>
        new(numero, pessoasPorVaga, pessoasPorVaga is > 0 ? AntecipaAHabilitacao : null);

    private static CadastroDeReservaInformado ComParticipacao(int? numero, int? pessoasPorVaga, string? participacao) =>
        new(numero, pessoasPorVaga, participacao);

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

    [Fact(DisplayName = "A primeira chamada também pode ter cadastro de reserva")]
    public void PrimeiraChamada_Aceita()
    {
        CadastroDeReservaPorChamada declaracao = Declarada(Chamada(1, 2));

        declaracao.Chamadas.Should().ContainSingle().Which.Chamada.Valor.Should().Be(1);
    }

    [Fact(DisplayName = "Zero pessoas da reserva por vaga é aceito e declara a chamada sem reserva")]
    public void Zero_Aceito_ComoChamadaSemReserva()
    {
        Declarada(Chamada(1, 0)).Chamadas.Should().ContainSingle().Which.PessoasDaReservaPorVaga.Should().Be(0);
    }

    [Theory(DisplayName = "A quantidade ausente ou negativa é recusada na posição da chamada, sem repetir o valor")]
    [InlineData(null)]
    [InlineData(-3)]
    public void Quantidade_AusenteOuNegativa_Recusa(int? pessoasPorVaga)
    {
        Result<CadastroDeReservaPorChamada> resultado = CadastroDeReservaPorChamada.Declarar([Chamada(1, 2), Chamada(2, pessoasPorVaga)]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "cadastroDeReserva[1].pessoasDaReservaPorVaga",
            Error = new
            {
                Code = CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida,
                Message = "Informe quantas pessoas da reserva a chamada convoca por vaga, um inteiro a partir de 0; zero é a chamada sem reserva.",
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
        Recusas(Chamada(1, 2), Chamada(1, -2), Chamada(null, 1), Chamada(2, null), Chamada(-1, -1)).Should().Equal(
            ("cadastroDeReserva[1].numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("cadastroDeReserva[1].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("cadastroDeReserva[2].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("cadastroDeReserva[3].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("cadastroDeReserva[4].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("cadastroDeReserva[4].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida));
    }

    [Theory(DisplayName = "A participação escolhida na chamada com reserva é guardada com a chamada")]
    [InlineData(AntecipaAHabilitacao, ParticipacaoDaReserva.AntecipaAHabilitacao)]
    [InlineData(AssumeNaMesmaChamada, ParticipacaoDaReserva.AssumeNaMesmaChamada)]
    public void Participacao_NaChamadaComReserva_EGuardada(string codigo, ParticipacaoDaReserva esperada)
    {
        Declarada(ComParticipacao(2, 2, codigo)).Chamadas.Should().ContainSingle().Which.Participacao.Should().Be(esperada);
    }

    [Fact(DisplayName = "Chamadas diferentes do mesmo processo guardam cada uma a própria participação")]
    public void ChamadasDiferentes_ComParticipacoesDiferentes()
    {
        CadastroDeReservaPorChamada declaracao = Declarada(
            ComParticipacao(2, 2, AntecipaAHabilitacao), ComParticipacao(3, 1, AssumeNaMesmaChamada), ComParticipacao(1, 2, AssumeNaMesmaChamada));

        declaracao.Chamadas.Select(static c => (c.Chamada.Valor, c.Participacao)).Should().Equal(
            (1, ParticipacaoDaReserva.AssumeNaMesmaChamada),
            (2, ParticipacaoDaReserva.AntecipaAHabilitacao),
            (3, ParticipacaoDaReserva.AssumeNaMesmaChamada));
    }

    [Fact(DisplayName = "A primeira chamada com reserva também escolhe a participação")]
    public void PrimeiraChamada_ComReserva_EscolheParticipacao()
    {
        Declarada(ComParticipacao(1, 2, AntecipaAHabilitacao)).Chamadas.Single().Participacao
            .Should().Be(ParticipacaoDaReserva.AntecipaAHabilitacao);
    }

    [Fact(DisplayName = "A chamada com zero pessoas da reserva por vaga e sem participação é aceita e fica sem participação")]
    public void QuantidadeZero_SemParticipacao_Aceita()
    {
        Declarada(ComParticipacao(1, 0, null)).Chamadas.Single().Participacao.Should().BeNull();
    }

    [Fact(DisplayName = "A chamada com reserva e sem participação é recusada no campo da participação, sem presumir uma opção")]
    public void QuantidadeMaiorQueZero_SemParticipacao_Recusa()
    {
        Result<CadastroDeReservaPorChamada> resultado = CadastroDeReservaPorChamada.Declarar([Chamada(1, 2), ComParticipacao(2, 3, null)]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "cadastroDeReserva[1].participacaoDaReserva",
            Error = new
            {
                Code = CadastroDeReservaErrorCodes.ParticipacaoAusente,
                Message = "A chamada com pessoas da reserva por vaga precisa da participação da reserva; escolha \"A reserva só antecipa a habilitação\" ou \"A reserva assume na mesma chamada\".",
            },
        });
    }

    [Theory(DisplayName = "A participação em chamada com zero pessoas da reserva por vaga é recusada, sem dado de pessoa")]
    [InlineData(AntecipaAHabilitacao)]
    [InlineData(AssumeNaMesmaChamada)]
    public void QuantidadeZero_ComParticipacao_Recusa(string codigo)
    {
        Result<CadastroDeReservaPorChamada> resultado = CadastroDeReservaPorChamada.Declarar([Chamada(1, 2), ComParticipacao(3, 0, codigo)]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "cadastroDeReserva[1].participacaoDaReserva",
            Error = new
            {
                Code = CadastroDeReservaErrorCodes.ParticipacaoSemReserva,
                Message = "A chamada sem pessoas da reserva por vaga não tem opção de participação a escolher; retire a opção ou informe a quantidade.",
            },
        });
    }

    [Theory(DisplayName = "O código de participação que não é uma das duas opções é recusado, com quantidade zero ou maior que zero")]
    [InlineData("SO_ANTECIPA_A_HABILITACAO", 2)]
    [InlineData("antecipa_a_habilitacao", 2)]
    [InlineData("", 0)]
    [InlineData(" ", 1)]
    public void Participacao_Desconhecida_Recusa(string codigo, int pessoasPorVaga)
    {
        Recusas(ComParticipacao(1, pessoasPorVaga, codigo)).Should().Equal(
            ("cadastroDeReserva[0].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoDesconhecida));
    }

    [Theory(DisplayName = "A quantidade ausente ou negativa não decide a participação: só a quantidade é recusada")]
    [InlineData(null)]
    [InlineData(-1)]
    public void QuantidadeInvalida_NaoRecusaAParticipacaoConhecida(int? pessoasPorVaga)
    {
        Recusas(ComParticipacao(1, pessoasPorVaga, AssumeNaMesmaChamada), ComParticipacao(2, pessoasPorVaga, null)).Should().Equal(
            ("cadastroDeReserva[0].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("cadastroDeReserva[1].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida));
    }

    [Fact(DisplayName = "Em cada item as recusas vêm na ordem do número, da quantidade e da participação")]
    public void RecusasDoItem_NaOrdemDosCampos()
    {
        Recusas(ComParticipacao(1, 1, null), ComParticipacao(1, -1, "X"), ComParticipacao(0, 0, AssumeNaMesmaChamada)).Should().Equal(
            ("cadastroDeReserva[0].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoAusente),
            ("cadastroDeReserva[1].numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("cadastroDeReserva[1].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("cadastroDeReserva[1].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoDesconhecida),
            ("cadastroDeReserva[2].numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("cadastroDeReserva[2].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoSemReserva));
    }

    [Fact(DisplayName = "As declarações das mesmas chamadas em ordens diferentes são iguais e têm o mesmo hash")]
    public void OrdensDiferentes_ProduzemDeclaracoesIguais()
    {
        CadastroDeReservaPorChamada emOrdem = Declarada(Chamada(1, 2), Chamada(2, 1));
        CadastroDeReservaPorChamada foraDeOrdem = Declarada(Chamada(2, 1), Chamada(1, 2));

        foraDeOrdem.Should().Be(emOrdem);
        foraDeOrdem.GetHashCode().Should().Be(emOrdem.GetHashCode());
    }

    [Fact(DisplayName = "As declarações com quantidades, chamadas ou participações diferentes não são iguais")]
    public void DeclaracoesDiferentes_NaoSaoIguais()
    {
        CadastroDeReservaPorChamada declaracao = Declarada(Chamada(1, 2));

        declaracao.Should().NotBe(Declarada(Chamada(1, 3)));
        declaracao.Should().NotBe(Declarada(Chamada(2, 2)));
        declaracao.Should().NotBe(Declarada(ComParticipacao(1, 2, AssumeNaMesmaChamada)));
        declaracao.Should().NotBe(CadastroDeReservaPorChamada.SemReserva);
    }
}
