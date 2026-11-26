namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDoModeloDeFormulario;
using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>
/// O que o modelo declara de si — se serve à fase de inscrição e se é obrigatório — e o que a declaração
/// decide: os blocos admitidos, o impedimento e os pressupostos (UNI-REQ-0144, ADR-0171).
/// </summary>
public sealed class ModeloFormularioDeclaracaoTests
{
    private static readonly PaginaDoModelo Modalidades = new(
        "MODALIDADES", 1, TipoPaginaFormulario.Bloco, BlocoSistema.ModalidadesCalculadas, "Modalidades", null, null, null);

    private static ConteudoDoModelo ConteudoComModalidades() =>
        new("Formulário", [Dados, Modalidades, Revisao with { Ordem = 2 }], [Item("CERTIFICADO", 0)], [], [], []);

    [Theory(DisplayName = "O modelo guarda a declaração informada na criação")]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Criar_GuardaADeclaracaoInformada(bool serveAFaseDeInscricao, bool obrigatorio)
    {
        ModeloFormulario modelo = Criar(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio), Conteudo([Item("CERTIFICADO", 0)])).Value!;

        modelo.ServeAFaseDeInscricao.Should().Be(serveAFaseDeInscricao);
        modelo.Obrigatorio.Should().Be(obrigatorio);
        modelo.Declaracao.Should().Be(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio));
    }

    [Fact(DisplayName = "A edição do modelo mantém a declaração")]
    public void Atualizar_MantemADeclaracao()
    {
        ModeloFormulario modelo = Criar(DeOutraFaseFacultativa, Conteudo([Item("CERTIFICADO", 0)])).Value!;

        Result atualizado = modelo.Atualizar("Outro nome", null, null, Conteudo([Item("DIPLOMA", 0)]), SemDerivacoes);

        atualizado.IsSuccess.Should().BeTrue();
        modelo.Declaracao.Should().Be(DeOutraFaseFacultativa);
    }

    [Fact(DisplayName = "A declaração ausente é recusada no cadastro com o caminho da declaração")]
    public void DeclaracaoAusente_RecusaComOCaminhoDaDeclaracao()
    {
        Result<ModeloFormulario> modelo = Criar(null, Conteudo([Item("CERTIFICADO", 0)]));

        modelo.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "declaracao",
            Error = new { Code = ModeloFormularioErrorCodes.DeclaracaoIncompleta },
        });
    }

    [Fact(DisplayName = "A declaração ausente não esconde as recusas do cadastro nem as do conteúdo que não dependem dela")]
    public void DeclaracaoAusente_AcumulaComAsDemaisRecusas()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            null, "Nome", null, null, "TIPO", Conteudo([Item("CERTIFICADO", 0)], "SEXO", "SEXO"), SemDerivacoes);

        modelo.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [ModeloFormularioErrorCodes.CodigoObrigatorio, ModeloFormularioErrorCodes.DeclaracaoIncompleta, ModeloFormularioErrorCodes.PressupostoRepetido]);
    }

    [Theory(DisplayName = "O modelo que serve à fase de inscrição admite as modalidades calculadas, obrigatório ou facultativo")]
    [InlineData(true)]
    [InlineData(false)]
    public void ServeAInscricao_AdmiteModalidadesCalculadas(bool obrigatorio)
    {
        Result<ModeloFormulario> modelo = Criar(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, obrigatorio), ConteudoComModalidades());

        modelo.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "O modelo de outra fase recusa as modalidades calculadas, obrigatório ou facultativo")]
    [InlineData(true)]
    [InlineData(false)]
    public void OutraFase_RecusaModalidadesCalculadas(bool obrigatorio)
    {
        Result<ModeloFormulario> modelo = Criar(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: false, obrigatorio), ConteudoComModalidades());

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.BlocoNaoAdmitido);
    }

    [Fact(DisplayName = "O modelo de outra fase é gravado sem tipo de processo e serve a qualquer processo")]
    public void OutraFase_SemTipoDeProcesso_EGravado()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "DADOS_COMPLEMENTARES", "Dados complementares", null, DeOutraFaseObrigatoria, null, Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);

        modelo.IsSuccess.Should().BeTrue();
        modelo.Value!.TipoProcessoCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "O item com impedimento só é aceito no modelo que serve à fase de inscrição, mesmo facultativo")]
    public void Impedimento_SoNoModeloQueServeAInscricao()
    {
        ItemDoModelo comImpedimento = Item("CERTIFICADO", 0).ComImpedimento(new Impedimento(Quando("CERTIFICADO"), "Já possui o certificado."));
        ConteudoDoModelo conteudo = Conteudo([comImpedimento]);

        Criar(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, Obrigatorio: false), conteudo).IsSuccess.Should().BeTrue();
        Criar(DeOutraFaseFacultativa, conteudo).Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ImpedimentoForaDaInscricao);
    }

    [Fact(DisplayName = "O modelo facultativo que serve à fase de inscrição não tem pressupostos, como o obrigatório")]
    public void ServeAInscricaoFacultativo_ComPressuposto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, Obrigatorio: false), Conteudo([Item("CERTIFICADO", 0)], "SEXO"));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoNaInscricao);
    }

    [Fact(DisplayName = "O modelo facultativo de outra fase aceita pressupostos")]
    public void OutraFaseFacultativo_ComPressuposto_Aceita() =>
        Criar(DeOutraFaseFacultativa, Conteudo([Item("CERTIFICADO", 0)], "SEXO")).IsSuccess.Should().BeTrue();
}
