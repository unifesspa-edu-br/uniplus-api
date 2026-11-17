namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class EscopoDaPermissaoEfetivaTests
{
    [Fact(DisplayName = "O escopo guarda o que recebeu")]
    public void Criar_DadosValidos_GuardaOQueRecebeu()
    {
        Guid unidade = Guid.CreateVersion7();
        Guid processo = Guid.CreateVersion7();
        Guid chamada = Guid.CreateVersion7();

        EscopoDaPermissaoEfetiva escopo = EscopoDaPermissaoEfetiva.Criar(unidade, processo, chamada, "edital", institucional: false).Value!;

        (escopo.UnidadeId, escopo.ProcessoId, escopo.ChamadaId).Should().Be(((Guid?)unidade, (Guid?)processo, (Guid?)chamada));
        (escopo.ResourceTipoRestricao, escopo.Institucional).Should().Be(("edital", false));
    }

    [Theory(DisplayName = "O identificador informado como vazio é recusado")]
    [InlineData("unidade")]
    [InlineData("processo")]
    [InlineData("chamada")]
    public void Criar_IdentificadorGuidVazio_Recusa(string escopo)
    {
        Result<EscopoDaPermissaoEfetiva> resultado = EscopoDaPermissaoEfetiva.Criar(
            escopo == "unidade" ? Guid.Empty : null,
            escopo == "processo" ? Guid.Empty : null,
            escopo == "chamada" ? Guid.Empty : null,
            null,
            institucional: false);

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);
    }

    [Theory(DisplayName = "O tipo de resource fora do kebab-case é recusado")]
    [InlineData("TiposDocumento")]
    [InlineData("tipo_documento")]
    public void Criar_ResourceTipoForaDaConvencao_Recusa(string restricao) =>
        EscopoDaPermissaoEfetiva.Criar(null, null, null, restricao, institucional: false).Error!.Code
            .Should().Be(AuthorizationErrorCodes.EffectiveGrantResourceTipoRestricaoInvalido);

    [Fact(DisplayName = "O identificador vazio precede o tipo de resource inválido")]
    public void Criar_IdentificadorVazioETipoInvalido_RecusaOIdentificador() =>
        EscopoDaPermissaoEfetiva.Criar(Guid.Empty, null, null, "Tipo_Invalido", institucional: false).Error!.Code
            .Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);

    [Fact(DisplayName = "O tipo de resource em branco é guardado como veio, sem normalização")]
    public void Criar_ResourceTipoEmBranco_GuardaComoVeio() =>
        EscopoDaPermissaoEfetiva.Criar(null, null, null, "   ", institucional: true).Value!.ResourceTipoRestricao.Should().Be("   ");

    [Fact(DisplayName = "O escopo da unidade guarda a unidade e não é institucional")]
    public void DaUnidade_UnidadeValida_GuardaAUnidade()
    {
        Guid unidade = Guid.CreateVersion7();

        EscopoDaPermissaoEfetiva escopo = EscopoDaPermissaoEfetiva.DaUnidade(unidade).Value!;

        (escopo.UnidadeId, escopo.Institucional).Should().Be(((Guid?)unidade, false));
    }

    [Fact(DisplayName = "O escopo da unidade vazia é recusado")]
    public void DaUnidade_GuidVazio_Recusa() =>
        EscopoDaPermissaoEfetiva.DaUnidade(Guid.Empty).Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);

    [Fact(DisplayName = "A instituição é institucional, sem unidade e sem restrição")]
    public void Instituicao_EhInstitucionalSemRestricao()
    {
        (EscopoDaPermissaoEfetiva.Instituicao.Institucional, EscopoDaPermissaoEfetiva.Instituicao.UnidadeId).Should()
            .Be((true, (Guid?)null));
        EscopoDaPermissaoEfetiva.Instituicao.TemRestricao.Should().BeFalse();
    }

    [Fact(DisplayName = "O escopo nenhum não é institucional, não tem unidade e não tem restrição")]
    public void Nenhum_NaoTemUnidadeInstituicaoNemRestricao()
    {
        (EscopoDaPermissaoEfetiva.Nenhum.Institucional, EscopoDaPermissaoEfetiva.Nenhum.UnidadeId).Should()
            .Be((false, (Guid?)null));
        EscopoDaPermissaoEfetiva.Nenhum.TemRestricao.Should().BeFalse();
    }

    [Theory(DisplayName = "O tipo de resource nulo, vazio ou em espaços não conta como restrição")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TemRestricao_TipoEmBranco_NaoConta(string? tipo) =>
        EscopoDaPermissaoEfetiva.Criar(null, null, null, tipo, institucional: false).Value!.TemRestricao.Should().BeFalse();

    [Theory(DisplayName = "A unidade, o processo, a chamada ou o tipo de resource válido contam como restrição")]
    [InlineData("unidade")]
    [InlineData("processo")]
    [InlineData("chamada")]
    [InlineData("resource")]
    public void TemRestricao_UmEscopo_Conta(string escopo)
    {
        EscopoDaPermissaoEfetiva restrito = EscopoDaPermissaoEfetiva.Criar(
            escopo == "unidade" ? Guid.CreateVersion7() : null,
            escopo == "processo" ? Guid.CreateVersion7() : null,
            escopo == "chamada" ? Guid.CreateVersion7() : null,
            escopo == "resource" ? "edital" : null,
            institucional: false).Value!;

        restrito.TemRestricao.Should().BeTrue();
    }
}
