namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Xunit;

/// <summary>
/// A declaração dos tipos de decisão de uma fase ou etapa é conferida como chegou, acumulando todas as
/// recusas por posição, e só depois vira a lista canônica que a entidade guarda.
/// </summary>
public sealed class TiposDeDecisaoDeclaradosTests
{
    private const string Campo = "tiposDeDecisao";

    private static (string? Campo, string Codigo)[] Recusas(IReadOnlyList<string?>? bruto, string campo = Campo) =>
        [.. TiposDeDecisaoDeclarados.Validar(bruto, campo).Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "Declaração ausente não declara tipo nenhum e não recusa")]
    public void Validar_Nulo_NaoRecusa()
    {
        Recusas(null).Should().BeEmpty();
        TiposDeDecisaoDeclarados.Canonicos(null).Should().BeEmpty();
    }

    [Fact(DisplayName = "Declaração vazia não declara tipo nenhum e não recusa")]
    public void Validar_Vazio_NaoRecusa()
    {
        Recusas([]).Should().BeEmpty();
        TiposDeDecisaoDeclarados.Canonicos([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Um tipo do vocabulário é aceito")]
    public void Validar_UmTipo_Aceita()
    {
        Recusas([TipoMotivoDeDecisaoCodigo.Classificacao]).Should().BeEmpty();
        TiposDeDecisaoDeclarados.Canonicos([TipoMotivoDeDecisaoCodigo.Classificacao])
            .Should().Equal(TipoMotivoDeDecisaoCodigo.Classificacao);
    }

    [Fact(DisplayName = "Vários tipos distintos são aceitos")]
    public void Validar_VariosTiposDistintos_Aceita()
    {
        IReadOnlyList<string?> bruto = [.. TipoMotivoDeDecisaoCodigo.Codigos];

        Recusas(bruto).Should().BeEmpty();
        TiposDeDecisaoDeclarados.Canonicos(bruto).Should().HaveCount(TipoMotivoDeDecisaoCodigo.Codigos.Count);
    }

    [Fact(DisplayName = "Os códigos canônicos saem em ordem alfabética ordinal, qualquer que seja a ordem de entrada")]
    public void Canonicos_OrdemDeEntradaDiferente_SaiEmOrdemAlfabeticaOrdinal()
    {
        IReadOnlyList<string?> naOrdemDoVocabulario = [.. TipoMotivoDeDecisaoCodigo.Codigos];
        IReadOnlyList<string> canonicos = TiposDeDecisaoDeclarados.Canonicos(naOrdemDoVocabulario);

        canonicos.Should().Equal(
            TipoMotivoDeDecisaoCodigo.AnaliseDocumental,
            TipoMotivoDeDecisaoCodigo.AvaliacaoBiopsicossocial,
            TipoMotivoDeDecisaoCodigo.Classificacao,
            TipoMotivoDeDecisaoCodigo.GabaritoDaProva,
            TipoMotivoDeDecisaoCodigo.Heteroidentificacao,
            TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes,
            TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa,
            TipoMotivoDeDecisaoCodigo.NotaDaEtapa);
    }

    [Fact(DisplayName = "Os códigos canônicos saem sem repetição")]
    public void Canonicos_TipoRepetido_SaiUmaVez()
    {
        TiposDeDecisaoDeclarados.Canonicos(
            [TipoMotivoDeDecisaoCodigo.Classificacao, TipoMotivoDeDecisaoCodigo.Classificacao])
            .Should().Equal(TipoMotivoDeDecisaoCodigo.Classificacao);
    }

    [Theory(DisplayName = "Texto fora do vocabulário é tipo inválido na posição de entrada, listando os aceitos sem repetir o valor")]
    [InlineData("RECURSO_DA_BANCA")]
    [InlineData("classificacao")]
    [InlineData("CLASSIFICACAO ")]
    [InlineData("NENHUM")]
    public void Validar_ForaDoVocabulario_RecusaComoInvalido(string tipo)
    {
        IReadOnlyList<FieldError> erros = TiposDeDecisaoDeclarados.Validar([TipoMotivoDeDecisaoCodigo.Classificacao, tipo], Campo);

        FieldError erro = erros.Should().ContainSingle().Subject;
        erro.Field.Should().Be("tiposDeDecisao[1]");
        erro.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.TipoInvalido);
        erro.Error.Message.Should().Contain(TipoMotivoDeDecisaoCodigo.CodigosEmTexto).And.NotContain(tipo);
    }

    [Fact(DisplayName = "Elemento nulo ou em branco é tipo inválido na própria posição")]
    public void Validar_NuloEBranco_RecusaComoInvalidoPorPosicao()
    {
        Recusas([TipoMotivoDeDecisaoCodigo.Classificacao, null, "", "   "]).Should().Equal(
            ("tiposDeDecisao[1]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("tiposDeDecisao[3]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "O tipo válido repetido é recusado na posição de cada repetição, e não na primeira")]
    public void Validar_TipoRepetido_RecusaDaSegundaOcorrenciaEmDiante()
    {
        IReadOnlyList<string?> bruto =
        [
            TipoMotivoDeDecisaoCodigo.Classificacao,
            TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa,
            TipoMotivoDeDecisaoCodigo.Classificacao,
            TipoMotivoDeDecisaoCodigo.Classificacao,
        ];

        Recusas(bruto).Should().Equal(
            ("tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado),
            ("tiposDeDecisao[3]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado));
    }

    [Fact(DisplayName = "A recusa de repetição cita o tipo repetido, que é do vocabulário")]
    public void Validar_TipoRepetido_MensagemCitaOCodigoDoTipo()
    {
        IReadOnlyList<FieldError> erros = TiposDeDecisaoDeclarados.Validar(
            [TipoMotivoDeDecisaoCodigo.Heteroidentificacao, TipoMotivoDeDecisaoCodigo.Heteroidentificacao], Campo);

        erros.Should().ContainSingle().Which.Error.Message.Should().Contain(TipoMotivoDeDecisaoCodigo.Heteroidentificacao);
    }

    [Fact(DisplayName = "Tipo inválido e tipo repetido na mesma declaração acumulam, na ordem das posições")]
    public void Validar_InvalidoERepetidoJuntos_AcumulaTudo()
    {
        Recusas(["RECURSO_DA_BANCA", TipoMotivoDeDecisaoCodigo.Classificacao, TipoMotivoDeDecisaoCodigo.Classificacao, null]).Should().Equal(
            ("tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado),
            ("tiposDeDecisao[3]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "Valores inválidos repetidos ou nulos repetidos são tipo inválido em cada posição, nunca repetição")]
    public void Validar_InvalidosRepetidos_SaoInvalidosEmCadaPosicao()
    {
        Recusas(["RECURSO_DA_BANCA", "RECURSO_DA_BANCA", null, null]).Should().OnlyContain(
            static recusa => recusa.Codigo == MotivoDeDecisaoErrorCodes.TipoInvalido).And.HaveCount(4);
    }

    [Fact(DisplayName = "O campo informado pelo chamador prefixa a posição de cada recusa")]
    public void Validar_CampoInformado_PrefixaAPosicao()
    {
        Recusas([null], campo: "tipos").Should().Equal(("tipos[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "A conferência não altera a entrada e repetida dá o mesmo resultado")]
    public void Validar_Repetida_NaoAlteraAEntradaEDaOMesmoResultado()
    {
        List<string?> bruto = [TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, "RECURSO_DA_BANCA", TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa];

        (string? Campo, string Codigo)[] primeira = Recusas(bruto);

        Recusas(bruto).Should().Equal(primeira);
        bruto.Should().Equal(TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, "RECURSO_DA_BANCA", TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa);
    }

    [Fact(DisplayName = "A declaração recusada não tem tipos aceitos: quem depende do dado não o lê recusado")]
    public void Aceitos_DeclaracaoRecusada_DevolveNulo()
    {
        TiposDeDecisaoDeclarados.Aceitos(["RECURSO_DA_BANCA", TipoMotivoDeDecisaoCodigo.Classificacao]).Should().BeNull();
        TiposDeDecisaoDeclarados.Aceitos([TipoMotivoDeDecisaoCodigo.Classificacao, TipoMotivoDeDecisaoCodigo.Classificacao]).Should().BeNull();
    }

    [Fact(DisplayName = "A declaração aceita devolve o conjunto dos tipos declarados")]
    public void Aceitos_DeclaracaoAceita_DevolveOConjunto()
    {
        TiposDeDecisaoDeclarados.Aceitos([TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, TipoMotivoDeDecisaoCodigo.Classificacao])
            .Should().BeEquivalentTo([TipoMotivoDeDecisao.IsencaoDaTaxa, TipoMotivoDeDecisao.Classificacao]);
    }

    [Fact(DisplayName = "A declaração ausente ou vazia é aceita e não declara tipo nenhum")]
    public void Aceitos_DeclaracaoAusenteOuVazia_DevolveConjuntoVazio()
    {
        TiposDeDecisaoDeclarados.Aceitos(null).Should().BeEmpty();
        TiposDeDecisaoDeclarados.Aceitos([]).Should().BeEmpty();
    }
}
