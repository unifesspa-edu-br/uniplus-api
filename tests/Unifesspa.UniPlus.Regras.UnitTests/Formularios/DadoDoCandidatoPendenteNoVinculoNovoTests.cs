namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O dado do candidato sem a proteção aprovada não aceita vínculo novo, e cada recusa o nomeia pelo
/// nome do catálogo, nunca pelo código (ADR-0153).
/// </summary>
public sealed class DadoDoCandidatoPendenteNoVinculoNovoTests
{
    private static readonly VinculosDeFatos SemVinculos = VinculosDeFatos.De([], []);

    [Fact(DisplayName = "Dois dados pendentes geram duas causas na ordem dos códigos, cada uma com o nome e sem o código")]
    public void VinculoNovo_DoisDadosPendentes_UmaCausaPorDadoNaOrdemDosCodigos()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(
            Pendente("Z_RENDA", "Renda familiar per capita"), Pendente("A_BENEFICIO", "Benefício social recebido"));

        Result resultado = ConferenciaNoCatalogo.VinculoNovo(catalogo, SemVinculos, VinculosDeFatos.De(["Z_RENDA", "A_BENEFICIO"], []));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code, e.Error.Message)).Should().Equal(
            (null, VinculoCatalogoErrorCodes.FatoComProtecaoPendente, MensagemDoDado("Benefício social recebido")),
            (null, VinculoCatalogoErrorCodes.FatoComProtecaoPendente, MensagemDoDado("Renda familiar per capita")));
        resultado.Errors.Should().OnlyContain(static e => !e.Error.Message.Contains("A_BENEFICIO") && !e.Error.Message.Contains("Z_RENDA"));
    }

    [Fact(DisplayName = "Dado pendente que o vínculo existente já usa não é recusado")]
    public void VinculoNovo_DadoPendenteJaVinculado_NaoRecusa()
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(Pendente("Z_RENDA", "Renda familiar per capita"));
        VinculosDeFatos usado = VinculosDeFatos.De(["Z_RENDA"], []);

        ConferenciaNoCatalogo.VinculoNovo(catalogo, usado, usado).IsSuccess.Should().BeTrue();
    }

    private static string MensagemDoDado(string nome) =>
        $"O dado do candidato '{nome}' aguarda a aprovação do Representante do controlador e não pode ser incluído agora. "
        + "Retire o dado do formulário, da regra, da condição ou da exigência de documento em que ele foi incluído, "
        + "ou peça ao administrador do catálogo que envie a solicitação de aprovação, se ainda não a enviou.";

    private static Dictionary<string, FatoDoCatalogo> Catalogo(params FatoDoCatalogo[] fatos) =>
        fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

    private static FatoDoCatalogo Pendente(string codigo, string nome) =>
        new(codigo, nome, TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, OrigemFato.Declarado, "CAMPO_FORMULARIO:" + codigo,
            null, EscopoFato.Candidato, Ativo: true, [], ProtecaoAprovada: false);
}
