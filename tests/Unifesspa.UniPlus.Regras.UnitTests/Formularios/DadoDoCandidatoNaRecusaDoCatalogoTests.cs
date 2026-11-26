namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As recusas do catálogo que não são de aprovação pendente (dado desconhecido, não coletável ou
/// desativado, e valor desativado) dizem "dado do candidato" e o nome do catálogo, nunca o código.
/// </summary>
public sealed class DadoDoCandidatoNaRecusaDoCatalogoTests
{
    private static readonly VinculosDeFatos SemVinculos = VinculosDeFatos.De([], []);

    [Fact(DisplayName = "O dado desconhecido é recusado sem repetir o código informado")]
    public void FatoDoItem_DadoQueOCatalogoNaoTem_RecusaSemOCodigo()
    {
        DomainError? recusa = ConferenciaNoCatalogo.FatoDoItem("CODIGO_QUE_NAO_EXISTE", Catalogo());

        recusa.Should().Be(new DomainError(ItemFormularioErrorCodes.FatoDesconhecido, "O dado do candidato informado não existe no catálogo."));
    }

    [Fact(DisplayName = "O dado desconhecido do campo do grupo é recusado sem repetir o código informado")]
    public void FatoDoSubitem_DadoQueOCatalogoNaoTem_RecusaSemOCodigo()
    {
        DomainError? recusa = ConferenciaNoCatalogo.FatoDoSubitem("CODIGO_QUE_NAO_EXISTE", Catalogo());

        recusa.Should().Be(new DomainError(ItemFormularioErrorCodes.FatoDesconhecido, "O dado do candidato informado não existe no catálogo."));
    }

    [Fact(DisplayName = "O dado de membro de grupo posto num campo comum é recusado com o nome do catálogo")]
    public void FatoDoItem_DadoDeMembroDeGrupo_RecusaComONome()
    {
        DomainError? recusa = ConferenciaNoCatalogo.FatoDoItem("PARENTESCO", Catalogo(Dado("PARENTESCO", "Grau de parentesco", escopo: EscopoFato.MembroGrupo)));

        recusa.Should().Be(new DomainError(
            ItemFormularioErrorCodes.FatoNaoColetavel,
            "O dado do candidato 'Grau de parentesco' não é coletável num campo: só o dado declarado do próprio candidato é respondido no formulário "
            + "— dados derivados, calculados e de membro de grupo não."));
    }

    [Fact(DisplayName = "O dado do próprio candidato posto num campo de grupo é recusado com o nome do catálogo")]
    public void FatoDoSubitem_DadoDoProprioCandidato_RecusaComONome()
    {
        DomainError? recusa = ConferenciaNoCatalogo.FatoDoSubitem("RENDA", Catalogo(Dado("RENDA", "Renda familiar per capita")));

        recusa.Should().Be(new DomainError(
            ItemFormularioErrorCodes.FatoNaoColetavel,
            "O dado do candidato 'Renda familiar per capita' não é coletável num campo do grupo: só o dado declarado de membro de grupo é respondido "
            + "em cada ocorrência — dados do próprio candidato, derivados e calculados não."));
    }

    [Fact(DisplayName = "Dois dados desativados geram duas causas na ordem dos códigos, cada uma com o nome e sem o código")]
    public void VinculoNovo_DoisDadosDesativados_UmaCausaPorDadoNaOrdemDosCodigos()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(
            Dado("Z_RENDA", "Renda familiar per capita", ativo: false), Dado("A_BENEFICIO", "Benefício social recebido", ativo: false));

        Result resultado = ConferenciaNoCatalogo.VinculoNovo(catalogo, SemVinculos, VinculosDeFatos.De(["Z_RENDA", "A_BENEFICIO"], []));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code, e.Error.Message)).Should().Equal(
            (null, VinculoCatalogoErrorCodes.FatoDesativado, "O dado do candidato 'Benefício social recebido' está desativado no catálogo e não aceita vínculo novo."),
            (null, VinculoCatalogoErrorCodes.FatoDesativado, "O dado do candidato 'Renda familiar per capita' está desativado no catálogo e não aceita vínculo novo."));
    }

    [Fact(DisplayName = "O valor desativado é recusado com o nome da opção e o nome do dado, sem os códigos")]
    public void VinculoNovo_ValorDesativadoComNome_RecusaComOsNomes()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(Dado("ZONA", "Zona de residência", valores: [new("RURAL", "Zona rural", false)]));

        Result resultado = ConferenciaNoCatalogo.VinculoNovo(catalogo, SemVinculos, VinculosDeFatos.De([], [], [("ZONA", "RURAL")]));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code, e.Error.Message)).Should().Equal(
            (null, VinculoCatalogoErrorCodes.ValorDesativado,
                "O valor 'Zona rural' do dado do candidato 'Zona de residência' está desativado no catálogo e não aceita vínculo novo."));
    }

    [Fact(DisplayName = "O valor desativado que o catálogo não descreve é recusado sem o código do valor")]
    public void VinculoNovo_ValorDesativadoSemNome_RecusaSemOCodigoDoValor()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(Dado("ZONA", "Zona de residência", valores: [new("RURAL", null, false)]));

        Result resultado = ConferenciaNoCatalogo.VinculoNovo(catalogo, SemVinculos, VinculosDeFatos.De([], [], [("ZONA", "RURAL")]));

        resultado.Errors.Select(static e => e.Error.Message).Should().Equal(
            "Um valor do dado do candidato 'Zona de residência' está desativado no catálogo e não aceita vínculo novo.");
    }

    [Fact(DisplayName = "O dado e o valor desativados que o vínculo existente já usa não são recusados")]
    public void VinculoNovo_DesativadosJaVinculados_NaoRecusa()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(
            Dado("ZONA", "Zona de residência", valores: [new("RURAL", "Zona rural", false)]), Dado("RENDA", "Renda familiar per capita", ativo: false));
        VinculosDeFatos usado = VinculosDeFatos.De(["RENDA"], [], [("ZONA", "RURAL")]);

        ConferenciaNoCatalogo.VinculoNovo(catalogo, usado, usado).IsSuccess.Should().BeTrue();
    }

    private static Dictionary<string, FatoDoCatalogo> Catalogo(params FatoDoCatalogo[] fatos) =>
        fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

    private static FatoDoCatalogo Dado(
        string codigo, string nome, EscopoFato escopo = EscopoFato.Candidato, bool ativo = true, IReadOnlyList<ValorDoCatalogo>? valores = null) =>
        new(codigo, nome, TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, OrigemFato.Declarado, "CAMPO_FORMULARIO:" + codigo,
            FonteValoresFato.Catalogo, escopo, ativo, valores ?? [], ProtecaoAprovada: true);
}
