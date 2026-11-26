namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// Como o modelo guarda o cadastro de reserva por chamada: uma linha por chamada, com o número, a quantidade e a
/// participação da reserva, e as restrições que impedem número menor que 1, quantidade negativa, participação fora das
/// duas opções e participação incoerente com a quantidade. Não abre conexão: só o modelo e o conversor.
/// </summary>
public sealed class CadastroDeReservaNoModeloTests
{
    private const string Tabela = "cadastros_de_reserva";

    [Fact(DisplayName = "A tabela guarda o número da chamada, a quantidade e a participação, todas comentadas")]
    public void Tabela_GuardaNumeroQuantidadeEParticipacao()
    {
        IReadOnlyList<ColunaDoModelo> colunas = ModeloDaSelecao.Colunas(Tabela);

        colunas.Select(static c => c.Nome).Should().Contain(["numero_da_chamada", "pessoas_da_reserva_por_vaga", "participacao_da_reserva"]).And.HaveCount(5);
        colunas.Should().OnlyContain(static c => !string.IsNullOrWhiteSpace(c.Comentario));
        colunas.Where(static c => c.Anulavel).Select(static c => c.Nome).Should().Equal("participacao_da_reserva");
        ModeloDaSelecao.ComentarioDaTabela(typeof(CadastroDeReservaDaChamada)).Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "A participação da reserva é gravada pelo código de contrato e o comentário lista os códigos")]
    public void Participacao_GravadaPeloCodigoDeContrato()
    {
        ColunaDoModelo participacao = ModeloDaSelecao.Colunas(Tabela).Single(static c => c.Nome == "participacao_da_reserva");

        participacao.Comentario.Should().Contain("ANTECIPA_A_HABILITACAO").And.Contain("ASSUME_NA_MESMA_CHAMADA");
    }

    [Fact(DisplayName = "O banco recusa participação fora das duas opções e participação incoerente com a quantidade")]
    public void Restricoes_ParticipacaoNasDuasOpcoesECoerenteComAQuantidade()
    {
        ModeloDaSelecao.SqlDaRestricao(typeof(CadastroDeReservaDaChamada), "ck_cadastros_de_reserva_participacao_da_reserva")
            .Should().Be("participacao_da_reserva IS NULL OR participacao_da_reserva IN ('ANTECIPA_A_HABILITACAO', 'ASSUME_NA_MESMA_CHAMADA')");
        ModeloDaSelecao.SqlDaRestricao(typeof(CadastroDeReservaDaChamada), "ck_cadastros_de_reserva_participacao_e_quantidade")
            .Should().Be("(pessoas_da_reserva_por_vaga = 0) = (participacao_da_reserva IS NULL)");
    }

    [Fact(DisplayName = "O número da chamada tem de ser ao menos 1 e a quantidade não pode ser negativa")]
    public void Restricoes_NumeroAPartirDeUmEQuantidadeNaoNegativa()
    {
        ModeloDaSelecao.SqlDaRestricao(typeof(CadastroDeReservaDaChamada), "ck_cadastros_de_reserva_numero_da_chamada")
            .Should().Be("numero_da_chamada >= 1");
        ModeloDaSelecao.SqlDaRestricao(typeof(CadastroDeReservaDaChamada), "ck_cadastros_de_reserva_pessoas_da_reserva_por_vaga")
            .Should().Be("pessoas_da_reserva_por_vaga >= 0");
    }

    [Fact(DisplayName = "O conversor grava o número da chamada como inteiro e o lê de volta igual")]
    public void Conversor_NumeroValido_IdaEVolta()
    {
        NumeroDaChamadaValueConverter conversor = new();
        NumeroDaChamada numero = NumeroDaChamada.Criar(3).Value!;

        int gravado = conversor.ConvertToProviderExpression.Compile()(numero);
        NumeroDaChamada lido = conversor.ConvertFromProviderExpression.Compile()(gravado);

        gravado.Should().Be(3);
        lido.Should().Be(numero);
    }

    [Fact(DisplayName = "O número inválido no banco causa erro sem expor o valor lido")]
    public void Conversor_NumeroInvalidoNoBanco_LancaErroSemExporOValor()
    {
        NumeroDaChamadaValueConverter conversor = new();
        Action leitura = () => conversor.ConvertFromProviderExpression.Compile()(-9876);

        leitura.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain("-9876");
    }
}
