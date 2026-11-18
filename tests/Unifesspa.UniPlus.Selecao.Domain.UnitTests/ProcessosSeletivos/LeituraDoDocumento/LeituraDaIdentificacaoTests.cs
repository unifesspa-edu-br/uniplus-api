namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>A identificação é resolvida nos cadastros citados: o que o cadastro não tem vira parte recusada que aponta o campo, e o ausente fica sem valor.</summary>
public sealed class LeituraDaIdentificacaoTests
{
    [Fact(DisplayName = "A identificação completa entrega o tipo, a unidade, a localidade e a convenção dos cadastros, sem parte recusada")]
    public void Identificacao_Completa_ResolveTudo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao());

        lidos.Identidade.Nome.Should().Be("Processo Seletivo de Exemplo 2027");
        lidos.Identidade.TipoProcesso!.Codigo.Should().Be("SiSU");
        lidos.Identidade.UnidadeAdministradora!.Codigo.Should().Be("CEPS");
        lidos.Identidade.OrigemCandidatos.Should().Be(OrigemCandidatos.ImportacaoExterna);
        lidos.Cronograma.Localidade!.CodigoIbge.Should().Be("1504208");
        lidos.Cronograma.AlgoritmoContagemPrazo!.Codigo.Should().Be(CodigoDaConvencao);
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo de processo que o cadastro não tem aponta o campo que o cita e deixa o tipo sem valor")]
    public void TipoDeProcesso_Inexistente_ApontaOCampo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { TipoProcessoCodigo = "TIPO-DIGITADO" });

        lidos.Identidade.TipoProcesso.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/identificacao/tipoProcessoCodigo", ProcessoSeletivoErrorCodes.TipoProcessoNaoEncontradoOuInativo));
    }

    [Fact(DisplayName = "A unidade administradora que o cadastro não tem aponta o campo que a cita")]
    public void UnidadeAdministradora_Inexistente_ApontaOCampo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { UnidadeAdministradoraCodigo = "UNIDADE-DIGITADA" });

        lidos.Identidade.UnidadeAdministradora.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/identificacao/unidadeAdministradoraCodigo", ProcessoSeletivoErrorCodes.UnidadeAdministradoraNaoEncontrada));
    }

    [Fact(DisplayName = "O município que o Geo não conhece reaproveita o código público do município inexistente")]
    public void Localidade_Inexistente_UsaOCodigoDoMunicipio()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { LocalidadeCodigoIbge = "9999999" });

        lidos.Cronograma.Localidade.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/identificacao/localidadeCodigoIbge", MunicipioErrorCodes.NaoEncontrado));
    }

    [Fact(DisplayName = "A regra citada como convenção que não é algoritmo de contagem é recusada no campo da convenção")]
    public void Convencao_QueNaoEAlgoritmo_ApontaOCampo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { AlgoritmoContagemPrazo = new(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1") });

        lidos.Cronograma.AlgoritmoContagemPrazo.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/identificacao/algoritmoContagemPrazo/codigo", ProcessoSeletivoErrorCodes.AlgoritmoContagemPrazoNaoEncontrado));
    }

    [Fact(DisplayName = "A versão inexistente da convenção aponta o código citado")]
    public void Convencao_ComVersaoInexistente_ApontaOCodigo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { AlgoritmoContagemPrazo = new(CodigoDaConvencao, "versao-inexistente") });

        PartesRecusadas(lidos).Should().Equal(("/identificacao/algoritmoContagemPrazo/codigo", ProcessoSeletivoErrorCodes.AlgoritmoContagemPrazoNaoEncontrado));
    }

    [Fact(DisplayName = "Os campos de identificação ausentes ficam sem valor e sem parte recusada")]
    public void Identificacao_SemCodigos_FicaSemValorESemRecusa()
    {
        IdentificacaoECronogramaLidos lidos = Ler(new IdentificacaoNoDocumento(null, null, null, OrigemCandidatos.Nenhuma, null, null, null));

        lidos.Identidade.TipoProcesso.Should().BeNull();
        lidos.Identidade.UnidadeAdministradora.Should().BeNull();
        lidos.Identidade.IdentificadorLegivel.Should().BeNull();
        lidos.Cronograma.Localidade.Should().BeNull();
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os códigos inexistentes de campos diferentes acumulam, e a mensagem não repete o texto digitado")]
    public void CodigosInexistentes_Acumulam_SemOTextoDigitado()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with
        {
            TipoProcessoCodigo = "TIPO-DIGITADO",
            UnidadeAdministradoraCodigo = "UNIDADE-DIGITADA",
            LocalidadeCodigoIbge = "9999999",
        });

        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal(
            "/identificacao/tipoProcessoCodigo", "/identificacao/unidadeAdministradoraCodigo", "/identificacao/localidadeCodigoIbge");
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("DIGITAD", StringComparison.Ordinal) || mensagem.Contains("9999999", StringComparison.Ordinal));
    }
}
