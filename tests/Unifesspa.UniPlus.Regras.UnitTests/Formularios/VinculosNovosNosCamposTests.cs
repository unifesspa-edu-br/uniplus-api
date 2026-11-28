namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A conferência do vínculo novo no catálogo, quando o pedido diz em que campo cada dado e cada valor foi citado: a recusa
/// aponta o campo, o mesmo dado citado em dois campos é recusado nos dois, e os tipos de recusa saem antes da posição dos campos.
/// </summary>
public sealed class VinculosNovosNosCamposTests
{
    private static readonly VinculosDeFatos SemVinculos = VinculosDeFatos.De([], []);

    private static VinculosDoCampo Cita(string campo, string fato) => new(campo, VinculosDeFatos.De([fato], []));

    private static VinculosDoCampo CitaValor(string campo, string fato, string valor) => new(campo, VinculosDeFatos.De([], [], [(fato, valor)]));

    [Fact(DisplayName = "O mesmo dado desativado citado em dois campos gera duas recusas, cada uma no seu campo")]
    public void DoisCamposComOMesmoDadoDesativado_DuasRecusas()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(Dado("RENDA", "Renda familiar per capita", ativo: false));

        IReadOnlyList<FieldError> recusas = ConferenciaNoCatalogo.VinculosNovosNosCampos(
            catalogo, SemVinculos, [Cita("a[0].fato", "RENDA"), Cita("a[1].fato", "RENDA")]);

        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("a[0].fato", VinculoCatalogoErrorCodes.FatoDesativado),
            ("a[1].fato", VinculoCatalogoErrorCodes.FatoDesativado));
    }

    [Fact(DisplayName = "O tipo da recusa vem antes da posição do campo: dado desativado, proteção pendente e valor desativado, nessa ordem")]
    public void TipoDaRecusaAntesDaPosicaoDoCampo()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(
            Dado("ZONA", "Zona de residência", valores: [new("RURAL", "Zona rural", false)]),
            Dado("BENEFICIO", "Benefício social recebido", protecaoAprovada: false),
            Dado("RENDA", "Renda familiar per capita", ativo: false));

        IReadOnlyList<FieldError> recusas = ConferenciaNoCatalogo.VinculosNovosNosCampos(
            catalogo, SemVinculos, [CitaValor("c[0].valor", "ZONA", "RURAL"), Cita("c[1].fato", "BENEFICIO"), Cita("c[2].fato", "RENDA")]);

        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("c[2].fato", VinculoCatalogoErrorCodes.FatoDesativado),
            ("c[1].fato", VinculoCatalogoErrorCodes.FatoComProtecaoPendente),
            ("c[0].valor", VinculoCatalogoErrorCodes.ValorDesativado));
    }

    [Fact(DisplayName = "O mesmo valor desativado citado em dois campos gera duas recusas, cada uma no seu campo")]
    public void DoisCamposComOMesmoValorDesativado_DuasRecusas()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(Dado("ZONA", "Zona de residência", valores: [new("RURAL", "Zona rural", false)]));

        IReadOnlyList<FieldError> recusas = ConferenciaNoCatalogo.VinculosNovosNosCampos(
            catalogo, SemVinculos, [CitaValor("c[0].valor", "ZONA", "RURAL"), CitaValor("c[1].valor", "ZONA", "RURAL")]);

        recusas.Select(static recusa => recusa.Field).Should().Equal("c[0].valor", "c[1].valor");
    }

    [Fact(DisplayName = "O campo que cita dado já vinculado não é recusado, e o outro campo sim")]
    public void CampoComDadoJaVinculado_NaoRecusa()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(
            Dado("RENDA", "Renda familiar per capita", ativo: false), Dado("BENEFICIO", "Benefício social recebido", ativo: false));

        IReadOnlyList<FieldError> recusas = ConferenciaNoCatalogo.VinculosNovosNosCampos(
            catalogo, VinculosDeFatos.De(["RENDA"], []), [Cita("c[0].fato", "RENDA"), Cita("c[1].fato", "BENEFICIO")]);

        recusas.Should().ContainSingle().Which.Field.Should().Be("c[1].fato");
    }

    [Fact(DisplayName = "O vínculo novo sem campos continua recusando sem apontar campo, uma recusa por dado")]
    public void VinculoNovo_ContinuaSemCampo()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(Dado("RENDA", "Renda familiar per capita", ativo: false));

        Result resultado = ConferenciaNoCatalogo.VinculoNovo(catalogo, SemVinculos, VinculosDeFatos.De(["RENDA"], []));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }

    private static Dictionary<string, FatoDoCatalogo> Catalogo(params FatoDoCatalogo[] fatos) =>
        fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

    private static FatoDoCatalogo Dado(
        string codigo, string nome, bool ativo = true, bool protecaoAprovada = true, IReadOnlyList<ValorDoCatalogo>? valores = null) =>
        new(codigo, nome, TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, OrigemFato.Declarado, "CAMPO_FORMULARIO:" + codigo,
            FonteValoresFato.Catalogo, EscopoFato.Candidato, ativo, valores ?? [], protecaoAprovada);

    [Fact(DisplayName = "Dois valores desativados na mesma condição são duas recusas no mesmo campo, uma para cada valor")]
    public void DoisValoresDesativadosNaMesmaCondicao_UmaRecusaParaCadaValor()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(
            Dado("ZONA", "Zona de residência", valores: [new("RURAL", "Zona rural", false), new("RIBEIRINHA", "Zona ribeirinha", false)]));
        VinculosDoCampo condicao = new("c[0].valor", VinculosDeFatos.De([], [], [("ZONA", "RURAL"), ("ZONA", "RIBEIRINHA")]));

        IReadOnlyList<FieldError> recusas = ConferenciaNoCatalogo.VinculosNovosNosCampos(catalogo, SemVinculos, [condicao]);

        recusas.Select(static recusa => recusa.Field).Should().Equal("c[0].valor", "c[0].valor");
        recusas.Select(static recusa => recusa.Error.Message).Should().OnlyHaveUniqueItems();
    }
}
