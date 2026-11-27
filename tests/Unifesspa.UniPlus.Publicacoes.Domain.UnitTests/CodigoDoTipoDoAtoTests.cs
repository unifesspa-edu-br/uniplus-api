namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>O código do tipo no registro do ato: a mesma regra do cadastro do tipo, conferida como chegou.</summary>
public sealed class CodigoDoTipoDoAtoTests
{
    [Theory(DisplayName = "Código do tipo nulo, vazio ou em branco é recusado como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ConferirCodigoDoTipo_Vazio_RecusaComoObrigatorio(string? codigo)
    {
        ParteConferida<string> parte = EssenciaDocumentalDoAto.ConferirCodigoDoTipo(codigo);

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("tipoCodigo", TipoAtoPublicadoErrorCodes.CodigoObrigatorio));
    }

    [Theory(DisplayName = "Código do tipo fora do formato ou acima de 60 caracteres é recusado pelo formato")]
    [InlineData("edital")]
    [InlineData(" EDITAL")]
    [InlineData("EDITAL__ABERTURA")]
    public void ConferirCodigoDoTipo_ForaDoFormato_RecusaPeloFormato(string codigo)
    {
        ParteConferida<string> parte = EssenciaDocumentalDoAto.ConferirCodigoDoTipo(codigo);

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("tipoCodigo", TipoAtoPublicadoErrorCodes.CodigoFormato));
        parte.Recusas[0].Error.Message.Should().NotContain(codigo);
    }

    [Fact(DisplayName = "Código do tipo com 61 caracteres é recusado e com 60 é aceito")]
    public void ConferirCodigoDoTipo_Limite_RecusaAcimaDe60()
    {
        EssenciaDocumentalDoAto.ConferirCodigoDoTipo(new string('A', 61)).EstaAceita.Should().BeFalse();
        EssenciaDocumentalDoAto.ConferirCodigoDoTipo(new string('A', 60)).EstaAceita.Should().BeTrue();
    }

    [Fact(DisplayName = "Código do tipo no formato é aceito como chegou")]
    public void ConferirCodigoDoTipo_NoFormato_AceitoSemAlterar()
    {
        EssenciaDocumentalDoAto.ConferirCodigoDoTipo("EDITAL_ABERTURA").Valor.Should().Be("EDITAL_ABERTURA");
    }
}
