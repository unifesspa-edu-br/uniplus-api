namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CadastroDeReservaNoEnvelopeTests
{
    private static CadastroDeReservaPorChamada Cadastro() =>
        CadastroDeReservaPorChamada.Declarar([new(2, 3), new(1, 0)]).Value!;

    [Fact(DisplayName = "Serializar escreve as chamadas em ordem crescente, com a chamada sem reserva incluída")]
    public void Serializar_EscreveEmOrdemCrescente()
    {
        JsonArray bloco = SerializacaoDoCadastroDeReserva.Serializar(Cadastro());

        bloco.Select(static item => (item!["numeroDaChamada"]!.GetValue<int>(), item["pessoasDaReservaPorVaga"]!.GetValue<int>()))
            .Should().Equal((1, 0), (2, 3));
    }

    [Fact(DisplayName = "Sem declaração, o bloco é a lista vazia")]
    public void Serializar_SemReserva_EscreveListaVazia() =>
        SerializacaoDoCadastroDeReserva.Serializar(CadastroDeReservaPorChamada.SemReserva).Should().BeEmpty();

    [Fact(DisplayName = "Ler reconstrói a declaração a partir do bloco serializado")]
    public void Ler_BlocoSerializado_ReconstroiDeclaracao()
    {
        JsonObject envelope = new() { ["cadastroDeReserva"] = SerializacaoDoCadastroDeReserva.Serializar(Cadastro()) };
        LeitorEnvelope leitor = new();

        CadastroDeReservaPorChamada? lida = LeituraDoCadastroDeReserva.Ler(leitor, envelope);

        leitor.Falhou.Should().BeFalse();
        lida.Should().Be(Cadastro());
    }

    [Fact(DisplayName = "Ler o bloco vazio devolve o processo sem reserva")]
    public void Ler_ListaVazia_DevolveSemReserva()
    {
        LeitorEnvelope leitor = new();

        CadastroDeReservaPorChamada? lida = LeituraDoCadastroDeReserva.Ler(leitor, new JsonObject { ["cadastroDeReserva"] = new JsonArray() });

        leitor.Falhou.Should().BeFalse();
        lida.Should().Be(CadastroDeReservaPorChamada.SemReserva);
    }

    [Fact(DisplayName = "O bloco ausente recusa o envelope")]
    public void Ler_BlocoAusente_Recusa()
    {
        LeitorEnvelope leitor = new();

        LeituraDoCadastroDeReserva.Ler(leitor, []);

        leitor.Falhou.Should().BeTrue();
    }

    [Theory(DisplayName = "A chave ausente em um item recusa o envelope")]
    [InlineData("numeroDaChamada")]
    [InlineData("pessoasDaReservaPorVaga")]
    public void Ler_ChaveDoItemAusente_Recusa(string chave)
    {
        JsonArray bloco = SerializacaoDoCadastroDeReserva.Serializar(Cadastro());
        bloco[0]!.AsObject().Remove(chave);
        LeitorEnvelope leitor = new();

        LeituraDoCadastroDeReserva.Ler(leitor, new JsonObject { ["cadastroDeReserva"] = bloco });

        leitor.Falhou.Should().BeTrue();
    }

    [Fact(DisplayName = "A quantidade que não é número inteiro recusa o envelope")]
    public void Ler_QuantidadeComTipoErrado_Recusa()
    {
        JsonArray bloco = SerializacaoDoCadastroDeReserva.Serializar(Cadastro());
        bloco[0]!["pessoasDaReservaPorVaga"] = "dois";
        LeitorEnvelope leitor = new();

        LeituraDoCadastroDeReserva.Ler(leitor, new JsonObject { ["cadastroDeReserva"] = bloco });

        leitor.Falhou.Should().BeTrue();
    }

    [Fact(DisplayName = "O hash do envelope muda quando uma chamada muda")]
    public void Hash_ChamadaAlterada_MudaResumo()
    {
        JsonObject envelope = new() { ["cadastroDeReserva"] = SerializacaoDoCadastroDeReserva.Serializar(Cadastro()) };
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        string original = perfil.HashHex(perfil.Serializar(envelope));

        envelope["cadastroDeReserva"]![1]!["pessoasDaReservaPorVaga"] = 4;

        perfil.HashHex(perfil.Serializar(envelope)).Should().NotBe(original);
    }

    [Fact(DisplayName = "As causas do domínio apontam o item e o campo no bloco, sem repetir o valor lido")]
    public void Ler_ChamadaRepetidaEQuantidadeNegativa_ApontaItemECampo()
    {
        JsonObject documento = DocumentoComChamadaRepetidaEQuantidadeNegativa();
        LeitorEnvelope leitor = new();

        LeituraDoCadastroDeReserva.Ler(leitor, documento);

        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("cadastroDeReserva[1].numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("cadastroDeReserva[1].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida));
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("-7", StringComparison.Ordinal));
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
    }

    private static JsonObject DocumentoComChamadaRepetidaEQuantidadeNegativa() => new()
    {
        ["cadastroDeReserva"] = new JsonArray(
            new JsonObject { ["numeroDaChamada"] = 1, ["pessoasDaReservaPorVaga"] = 2 },
            new JsonObject { ["numeroDaChamada"] = 1, ["pessoasDaReservaPorVaga"] = -7 }),
    };
}
