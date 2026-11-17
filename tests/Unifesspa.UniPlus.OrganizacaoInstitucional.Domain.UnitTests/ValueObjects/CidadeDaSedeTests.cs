namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A referência de cidade da sede: a conferência do trio, com cada erro no campo que ele descreve,
/// e o carimbo da proveniência e do instante, que só renova quando o trio informado muda.
/// </summary>
public sealed class CidadeDaSedeTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 22, 17, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CarimboAnterior = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly CidadeDaSede MarabaCarimbada =
        new("1504208", "Marabá", "PA", ReferenciaCidadeGeo.OrigemGeoApi, CarimboAnterior);

    private static IReadOnlyList<FieldError> ErrosDa(CidadeDaSede cidade) =>
        Instituicao.ValidarCampos(InstituicaoDeTeste.IdentificacaoDaUnifesspa, cidade, endereco: null).Errors;

    [Fact(DisplayName = "Cidade ausente por completo é válida")]
    public void Validar_SemNenhumFragmento_EhValida()
    {
        ErrosDa(CidadeDaSede.Nenhuma).Should().BeEmpty();
    }

    [Fact(DisplayName = "Só o código IBGE recusa o nome e a UF, cada um no seu campo")]
    public void Validar_SoComCodigoIbge_RecusaNomeEUfNosCamposDeCada()
    {
        IReadOnlyList<FieldError> erros = ErrosDa(new CidadeDaSede("1504208", null, null, null, null));

        erros.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("cidadeNome", CidadeReferenciaErrorCodes.NomeObrigatorio),
            ("cidadeUf", CidadeReferenciaErrorCodes.UfObrigatoria));
    }

    [Fact(DisplayName = "Só o nome e a UF recusam o código IBGE no campo do código")]
    public void Validar_SoComNomeEUf_RecusaOCodigoIbgeNoCampoDoCodigo()
    {
        IReadOnlyList<FieldError> erros = ErrosDa(new CidadeDaSede(null, "Marabá", "PA", null, null));

        erros.Should().ContainSingle().Which.Field.Should().Be("cidadeCodigoIbge");
    }

    [Fact(DisplayName = "UF incoerente com o prefixo do código recusa no campo da UF")]
    public void Validar_UfIncoerente_RecusaNoCampoDaUf()
    {
        IReadOnlyList<FieldError> erros = ErrosDa(new CidadeDaSede("1504208", "Marabá", "SP", null, null));

        FieldError erro = erros.Should().ContainSingle().Which;
        erro.Field.Should().Be("cidadeUf");
        erro.Error.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Nome acima do limite recusa no campo do nome")]
    public void Validar_NomeAcimaDoLimite_RecusaNoCampoDoNome()
    {
        string nomeLongo = new('A', ReferenciaCidadeGeo.NomeMaxLength + 1);

        IReadOnlyList<FieldError> erros = ErrosDa(new CidadeDaSede("1504208", nomeLongo, "PA", null, null));

        erros.Should().ContainSingle().Which.Field.Should().Be("cidadeNome");
    }

    [Fact(DisplayName = "Carimbada com outro código renova a proveniência e o instante")]
    public void Carimbada_CodigoMudou_RenovaProvenienciaEInstante()
    {
        CidadeDaSede informada = new("1501402", "Belém", "PA", null, null);

        CidadeDaSede carimbada = informada.Carimbada(MarabaCarimbada, Agora);

        carimbada.Should().Be(new CidadeDaSede("1501402", "Belém", "PA", ReferenciaCidadeGeo.OrigemGeoApi, Agora));
    }

    [Theory(DisplayName = "Carimbada com só o nome ou só a UF diferente renova o carimbo")]
    [InlineData("1504208", "Marabá do Pará", "PA")]
    [InlineData("1504208", "Marabá", "SP")]
    public void Carimbada_SoNomeOuSoUfMudou_RenovaOCarimbo(string codigo, string nome, string uf)
    {
        CidadeDaSede carimbada = new CidadeDaSede(codigo, nome, uf, null, null).Carimbada(MarabaCarimbada, Agora);

        carimbada.DisplayAtualizadoEm.Should().Be(Agora);
        carimbada.Origem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
    }

    [Fact(DisplayName = "Carimbada com o mesmo trio preserva a proveniência e o instante anteriores")]
    public void Carimbada_TrioNaoMudou_PreservaOCarimboAnterior()
    {
        CidadeDaSede informada = new("1504208", "Marabá", "PA", null, null);

        CidadeDaSede carimbada = informada.Carimbada(MarabaCarimbada, Agora);

        carimbada.Should().Be(MarabaCarimbada);
    }

    [Fact(DisplayName = "Carimbada que difere só por caixa e espaços não conta como mudança")]
    public void Carimbada_SoCaixaEEspacosDiferentes_NaoMudou()
    {
        CidadeDaSede informada = new(" 1504208 ", "  Marabá ", "pa", null, null);

        CidadeDaSede carimbada = informada.Carimbada(MarabaCarimbada, Agora);

        carimbada.Should().Be(MarabaCarimbada);
    }

    [Fact(DisplayName = "Carimbada de ausente para presente carimba com o instante atual")]
    public void Carimbada_AusentePresente_Carimba()
    {
        CidadeDaSede informada = new("1504208", "Marabá", "PA", null, null);

        CidadeDaSede carimbada = informada.Carimbada(CidadeDaSede.Nenhuma, Agora);

        carimbada.Should().Be(MarabaCarimbada with { DisplayAtualizadoEm = Agora });
    }

    [Fact(DisplayName = "Carimbada de presente para ausente zera trio, proveniência e instante")]
    public void Carimbada_PresenteAusente_ZeraTudo()
    {
        CidadeDaSede carimbada = CidadeDaSede.Nenhuma.Carimbada(MarabaCarimbada, Agora);

        carimbada.Should().Be(CidadeDaSede.Nenhuma);
    }

    [Fact(DisplayName = "Carimbada sem cidade nem antes nem depois segue sem cidade")]
    public void Carimbada_AusenteAusente_SegueAusente()
    {
        CidadeDaSede carimbada = new CidadeDaSede(null, "  ", null, "geo-api", Agora).Carimbada(CidadeDaSede.Nenhuma, Agora);

        carimbada.Should().Be(CidadeDaSede.Nenhuma);
    }

    [Fact(DisplayName = "Carimbada devolve o trio já normalizado")]
    public void Carimbada_ComTrioSujo_DevolveOTrioNormalizado()
    {
        CidadeDaSede informada = new(" 1504208 ", "  Marabá ", "pa", null, null);

        CidadeDaSede carimbada = informada.Carimbada(CidadeDaSede.Nenhuma, Agora);

        carimbada.Should().Be(new CidadeDaSede("1504208", "Marabá", "PA", ReferenciaCidadeGeo.OrigemGeoApi, Agora));
    }
}
