namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services.FatoDeTesteDaAlteracao;

public sealed class CodigosNaAlteracaoDaDefinicaoTests
{
    [Fact]
    public void AlterarDefinicao_EixosInferidosDesconhecidos_RecusaSomenteEixosNaoEditaveis()
    {
        FatoCandidato fato = Derivado("PERFIL");
        DefinicaoDoFato definicao = Definicao(TipoDoDado.SimOuNao) with
        {
            FonteValores = CodigoInformado<FonteValoresFato>.Ler("INEXISTENTE"),
            Formato = CodigoInformado<FormatoTexto>.Ler("INEXISTENTE"),
        };
        Result resultado = fato.AlterarDefinicao(definicao, Catalogo(fato));
        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("fonteValores", FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato),
            ("formato", FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato));
    }

    [Fact]
    public void AlterarDefinicao_FatoDeSistemaComCodigosDesconhecidos_AcumulaRecusas()
    {
        FatoCandidato fato = Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.FatoCandidatoDeTeste.Criar(sistema: true).Value!;
        DefinicaoDoFato definicao = Definicao(null, escopo: null) with
        {
            FonteValores = CodigoInformado<FonteValoresFato>.Ler("INEXISTENTE"),
            Formato = CodigoInformado<FormatoTexto>.Ler("INEXISTENTE"),
            Escopo = CodigoInformado<EscopoFato>.Ler("INEXISTENTE"),
        };
        fato.AlterarDefinicao(definicao, Catalogo(fato)).Errors.Select(static erro => erro.Error.Code).Should().Equal(
            FatoCandidatoErrorCodes.FonteValoresInvalida, FatoCandidatoErrorCodes.FormatoInvalido, FatoCandidatoErrorCodes.EscopoInvalido);
    }

    [Fact]
    public void AlterarDefinicao_EscopoDesconhecidoEmAgregado_RecusaSomenteEixoNaoEditavel()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato fato = Agregado("CATEGORIAS_RENDA", membro);
        DefinicaoDoFato definicao = Definicao(null, escopo: null) with { Escopo = CodigoInformado<EscopoFato>.Ler("INEXISTENTE") };
        fato.AlterarDefinicao(definicao, Catalogo(membro, fato)).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato);
    }
}
