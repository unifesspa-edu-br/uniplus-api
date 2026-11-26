namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CadastroDeReservaNoEnvelopeTests
{
    private static CadastroDeReservaPorChamada Cadastro() =>
        CadastroDeReservaPorChamada.Declarar([new(2, 3, "ASSUME_NA_MESMA_CHAMADA"), new(1, 0, null)]).Value!;

    [Fact(DisplayName = "Serializar escreve as chamadas em ordem crescente, com a chamada sem reserva incluída")]
    public void Serializar_EscreveEmOrdemCrescente()
    {
        JsonArray bloco = SerializacaoDoCadastroDeReserva.Serializar(Cadastro());

        bloco.Select(static item => (item!["numeroDaChamada"]!.GetValue<int>(), item["pessoasDaReservaPorVaga"]!.GetValue<int>()))
            .Should().Equal((1, 0), (2, 3));
    }

    [Fact(DisplayName = "Serializar escreve o código da participação e o rótulo no contexto só na chamada com reserva")]
    public void Serializar_EscreveParticipacaoERotuloNoContexto()
    {
        JsonArray bloco = SerializacaoDoCadastroDeReserva.Serializar(Cadastro());

        bloco[0]!.AsObject().Should().ContainKey("participacaoDaReserva").WhoseValue.Should().BeNull();
        bloco[0]!.AsObject().Should().NotContainKey("contexto");
        bloco[1]!["participacaoDaReserva"]!.GetValue<string>().Should().Be("ASSUME_NA_MESMA_CHAMADA");
        bloco[1]!["contexto"]!["participacaoDaReservaRotulo"]!.GetValue<string>().Should().Be("A reserva assume na mesma chamada");
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
    [InlineData("participacaoDaReserva")]
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

    [Fact(DisplayName = "A participação que não é texto recusa o envelope")]
    public void Ler_ParticipacaoComTipoErrado_Recusa()
    {
        JsonArray bloco = SerializacaoDoCadastroDeReserva.Serializar(Cadastro());
        bloco[1]!["participacaoDaReserva"] = 7;
        LeitorEnvelope leitor = new();

        LeituraDoCadastroDeReserva.Ler(leitor, new JsonObject { ["cadastroDeReserva"] = bloco });

        leitor.Falhou.Should().BeTrue();
    }

    [Fact(DisplayName = "O hash do envelope muda quando a participação de uma chamada muda")]
    public void Hash_ParticipacaoAlterada_MudaResumo()
    {
        JsonObject envelope = new() { ["cadastroDeReserva"] = SerializacaoDoCadastroDeReserva.Serializar(Cadastro()) };
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        string original = perfil.HashHex(perfil.Serializar(envelope));

        envelope["cadastroDeReserva"]![1]!["participacaoDaReserva"] = "ANTECIPA_A_HABILITACAO";

        perfil.HashHex(perfil.Serializar(envelope)).Should().NotBe(original);
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

    [Fact(DisplayName = "A participação incoerente com a quantidade ou desconhecida é recusada pelo domínio e aponta o campo no bloco")]
    public void Ler_ParticipacaoIncoerente_ApontaItemECampo()
    {
        JsonObject documento = DocumentoComParticipacoesIncoerentes();
        LeitorEnvelope leitor = new();

        LeituraDoCadastroDeReserva.Ler(leitor, documento);

        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("cadastroDeReserva[0].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoAusente),
            ("cadastroDeReserva[1].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoSemReserva),
            ("cadastroDeReserva[2].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoDesconhecida));
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
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
            Item(1, 2, "ASSUME_NA_MESMA_CHAMADA"),
            Item(1, -7, null)),
    };

    private static JsonObject DocumentoComParticipacoesIncoerentes() => new()
    {
        ["cadastroDeReserva"] = new JsonArray(Item(1, 2, null), Item(2, 0, "ASSUME_NA_MESMA_CHAMADA"), Item(3, 1, "OUTRA")),
    };

    private static JsonObject Item(int numero, int pessoas, string? participacao) => new()
    {
        ["numeroDaChamada"] = numero,
        ["pessoasDaReservaPorVaga"] = pessoas,
        ["participacaoDaReserva"] = participacao,
    };
}
