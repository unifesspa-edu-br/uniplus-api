namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

using Xunit;

/// <summary>
/// O cadastro de reserva e a manifestação do documento passam pelas regras de domínio da gravação por dimensão: o bloco recusado
/// fica sem declaração, as recusas se acumulam apontando o item e o campo, e a manifestação não prevista se distingue da recusada.
/// </summary>
public sealed class CadastroDeReservaEManifestacaoLidosTests
{
    private static readonly CadastroDeReservaInformado?[] ChamadasRecusadas =
    [
        new(0, -1, "PARTICIPACAO_QUE_NAO_EXISTE"),
        new(1, 2, null),
        new(1, 0, "ANTECIPA_A_HABILITACAO"),
    ];

    private static DeclaracoesDoDocumento Documento(
        IReadOnlyList<CadastroDeReservaInformado?>? cadastro = null, string? manifestacao = null) =>
        new(new IdentificacaoNoDocumento(null, null, null, null, null, null, null), [])
        {
            CadastroDeReserva = cadastro ?? [],
            ManifestacaoDeInteresse = manifestacao,
        };

    private static (string? Ponteiro, string Codigo)[] Causas(CadastroDeReservaEManifestacaoLidos lidos) =>
        [.. lidos.Origem.PartesRecusadas.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code))];

    [Fact(DisplayName = "O cadastro de reserva conforme entrega as chamadas em ordem crescente e a manifestação o código do catálogo, sem recusa")]
    public void BlocosConformes_EntregamAsDeclaracoesSemRecusa()
    {
        CadastroDeReservaEManifestacaoLidos lidos = CadastroDeReservaEManifestacaoLidos.Ler(Documento(
            [new(2, 0, null), new(1, 3, "ASSUME_NA_MESMA_CHAMADA")], "CONFIRMA_A_CADA_CHAMADA"));

        lidos.CadastroDeReserva!.Chamadas.Select(static chamada => chamada.Chamada.Valor).Should().Equal(1, 2);
        lidos.CadastroDeReserva.Chamadas[0].Participacao.Should().Be(ParticipacaoDaReserva.AssumeNaMesmaChamada);
        lidos.ManifestacaoDeInteresse.Should().Be(ManifestacaoDeInteresse.ConfirmaACadaChamada);
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O documento sem os blocos é o processo sem cadastro de reserva e sem manifestação prevista, sem recusa")]
    public void BlocosAusentes_SaoSemReservaEManifestacaoNaoPrevista()
    {
        CadastroDeReservaEManifestacaoLidos lidos = CadastroDeReservaEManifestacaoLidos.Ler(Documento());

        lidos.CadastroDeReserva.Should().Be(CadastroDeReservaPorChamada.SemReserva);
        lidos.ManifestacaoDeInteresse.Should().BeNull();
        lidos.Origem.TemParteRecusadaEm(DeclaracaoDaManifestacaoDeInteresse.Campo).Should().BeFalse();
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas do cadastro de reserva se acumulam, cada uma no ponteiro do item e do campo, e o bloco fica sem declaração")]
    public void CadastroRecusado_AcumulaAsCausasNoPonteiroDoItem()
    {
        CadastroDeReservaEManifestacaoLidos lidos = CadastroDeReservaEManifestacaoLidos.Ler(Documento(ChamadasRecusadas, "UMA_VEZ"));

        lidos.CadastroDeReserva.Should().BeNull();
        lidos.ManifestacaoDeInteresse.Should().Be(ManifestacaoDeInteresse.UmaVez);
        Causas(lidos).Should().Equal(
            ("/cadastroDeReserva/0/numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido),
            ("/cadastroDeReserva/0/pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida),
            ("/cadastroDeReserva/0/participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoDesconhecida),
            ("/cadastroDeReserva/1/participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoAusente),
            ("/cadastroDeReserva/2/numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("/cadastroDeReserva/2/participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoSemReserva));
        lidos.Origem.PartesRecusadas.Should().OnlyContain(static parte => parte.Caminho == CadastroDeReservaPorChamada.Campo);
    }

    [Fact(DisplayName = "A manifestação desconhecida fica sem declaração e a recusa se distingue da manifestação não prevista pelo campo recusado")]
    public void ManifestacaoDesconhecida_FicaRecusadaNoCampo()
    {
        CadastroDeReservaEManifestacaoLidos lidos = CadastroDeReservaEManifestacaoLidos.Ler(Documento(manifestacao: "MANIFESTACAO_QUE_NAO_EXISTE"));

        lidos.ManifestacaoDeInteresse.Should().BeNull();
        lidos.Origem.TemParteRecusadaEm(DeclaracaoDaManifestacaoDeInteresse.Campo).Should().BeTrue();
        lidos.Origem.TemParteRecusadaEm(CadastroDeReservaPorChamada.Campo).Should().BeFalse();
        Causas(lidos).Should().Equal(("/manifestacaoDeInteresse", ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido));
    }

    [Fact(DisplayName = "As declarações nulas são defeito de quem chama")]
    public void DeclaracoesNulas_Lanca()
    {
        Action ler = () => CadastroDeReservaEManifestacaoLidos.Ler(null!);

        ler.Should().Throw<ArgumentNullException>();
    }
}
