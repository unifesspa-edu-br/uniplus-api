namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Invariantes da forma plena de <see cref="ObrigatoriedadeLegal"/>: campos populados, hash computado sobre o
/// conteúdo final, full-replace na atualização e recusa de predicado sem forma.
/// </summary>
public sealed class ObrigatoriedadeLegalCriarTests
{
    private static readonly PredicadoObrigatoriedade PredicadoBase = new EtapaObrigatoria("ProvaObjetiva");
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly TextoNormativo TextoBase = new("Descrição", "Lei 12.711/2012 art.1º", null, null);

    private static Result<ObrigatoriedadeLegal> Nova(
        string regraCodigo = "X",
        PredicadoObrigatoriedade? predicado = null,
        string tipoProcessoCodigo = TodosOsTiposDeProcesso.Codigo,
        TextoNormativo? texto = null,
        DateOnly? vigenciaFim = null) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo, CategoriaObrigatoriedade.Etapa, regraCodigo, predicado ?? PredicadoBase, texto ?? TextoBase, Inicio, vigenciaFim);

    private static Result Atualizar(ObrigatoriedadeLegal regra, PredicadoObrigatoriedade predicado, TextoNormativo texto) =>
        regra.Atualizar(regra.TipoProcessoCodigo, regra.Categoria, regra.RegraCodigo, predicado, texto, regra.VigenciaInicio, regra.VigenciaFim);

    [Fact(DisplayName = "Criar regra universal global popula campos e computa hash")]
    public void Criar_RegraUniversalGlobal_OK()
    {
        ObrigatoriedadeLegal regra = Nova("ETAPA_OBRIGATORIA").Value!;

        regra.TipoProcessoCodigo.Should().Be(TodosOsTiposDeProcesso.Codigo);
        regra.Categoria.Should().Be(CategoriaObrigatoriedade.Etapa);
        regra.RegraCodigo.Should().Be("ETAPA_OBRIGATORIA");
        HashCanonicalComputer.IsValidHashShape(regra.Hash).Should().BeTrue();
    }

    [Theory(DisplayName = "TipoProcessoCodigo estruturalmente válido é aceito; a atividade é validada pelo catálogo no handler")]
    [InlineData(TodosOsTiposDeProcesso.Codigo)]
    [InlineData("SISU")]
    [InlineData("INDIGENA_E_QUILOMBOLA")]
    [InlineData("PORTADOR_DE_DIPLOMA")]
    [InlineData("SISU_ANTIGO")]
    public void Criar_TipoProcessoCodigoEstruturalmenteValido_Aceita(string tipoProcessoCodigo)
    {
        Result<ObrigatoriedadeLegal> resultado = Nova(tipoProcessoCodigo: tipoProcessoCodigo);

        resultado.IsSuccess.Should().BeTrue("o domínio só conhece a estrutura do código; a existência é do handler");
        resultado.Value!.TipoProcessoCodigo.Should().Be(tipoProcessoCodigo);
    }

    [Fact]
    public void Criar_TipoComOitentaCaracteres_ConservaCodigo()
    {
        string codigo = new('A', 80);

        Nova(tipoProcessoCodigo: codigo).Value!.TipoProcessoCodigo.Should().Be(codigo);
    }

    [Fact(DisplayName = "Atualizar recomputa o hash quando campo semântico muda")]
    public void Atualizar_AlteraBaseLegal_HashMuda()
    {
        ObrigatoriedadeLegal regra = Nova().Value!;
        string hashAntes = regra.Hash;

        Result r = Atualizar(regra, PredicadoBase, TextoBase with { BaseLegal = "Lei 14.723/2023 art.2º" });

        r.IsSuccess.Should().BeTrue();
        regra.Hash.Should().NotBe(hashAntes);
        regra.BaseLegal.Should().Be("Lei 14.723/2023 art.2º");
    }

    [Fact(DisplayName = "Atualizar é full-replace — passar null em opcional limpa o estado anterior")]
    public void Atualizar_FullReplace_LimpaOpcionaisNaoPassados()
    {
        ObrigatoriedadeLegal regra = Nova(
            texto: TextoBase with { AtoNormativo = "https://www.planalto.gov.br/lei", PortariaInternaCodigo = "PORT/2026/01" }).Value!;

        Result r = Atualizar(regra, regra.Predicado, new TextoNormativo(regra.DescricaoHumana, regra.BaseLegal, null, null));

        r.IsSuccess.Should().BeTrue();
        regra.AtoNormativoUrl.Should().BeNull();
        regra.PortariaInternaCodigo.Should().BeNull();
    }

    [Theory(DisplayName = "Predicado sem forma é recusado pela factory, sem chegar a persistir")]
    [MemberData(nameof(PredicadosSemForma))]
    public void Criar_PredicadoSemForma_Falha(PredicadoObrigatoriedade predicado, string codigoEsperado)
    {
        Result<ObrigatoriedadeLegal> r = Nova(predicado: predicado);

        r.Errors.Should().ContainSingle().Which.Should()
            .Match<FieldError>(e => e.Error.Code == codigoEsperado && e.Field == "predicado");
    }

    public static TheoryData<PredicadoObrigatoriedade, string> PredicadosSemForma() => new()
    {
        { new EtapaObrigatoria(""), "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco" },
        { new EtapaObrigatoria("   "), "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco" },
        { new DocumentoObrigatorioParaModalidade("LB_PPI", "  "), "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco" },
        { new ModalidadesMinimas(["AC", "   "]), "ObrigatoriedadeLegal.ModalidadesMinimasVazia" },
    };

    [Fact(DisplayName = "Atualizar para modalidades mínimas com código em branco é recusado e não muda a regra")]
    public void Atualizar_ModalidadesMinimasComCodigoEmBranco_Falha()
    {
        ObrigatoriedadeLegal regra = Nova(predicado: new ModalidadesMinimas(["AC"])).Value!;

        Result r = Atualizar(regra, new ModalidadesMinimas(["AC", "   "]), TextoBase);

        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.ModalidadesMinimasVazia");
        regra.Predicado.Should().BeOfType<ModalidadesMinimas>()
            .Which.Codigos.Should().ContainSingle("a recusa acontece antes de aplicar o payload");
    }
}
