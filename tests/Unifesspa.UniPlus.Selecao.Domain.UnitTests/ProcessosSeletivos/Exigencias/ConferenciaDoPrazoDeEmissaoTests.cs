namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O prazo máximo de emissão do documento é opcional e congela com a exigência (UNI-REQ-0016, UNI-REQ-0078);
/// o comprovante de residência do conjunto básico da habilitação não pode ficar sem ele (UNI-REQ-0151). O
/// valor é sempre o declarado na exigência, e a âncora tem de existir no cronograma do processo.
/// </summary>
public sealed class ConferenciaDoPrazoDeEmissaoTests
{
    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

    private static IdadeMaximaEmissao Idade(int valor, ReferenciaTipoIdadeEmissao referencia, Guid? faseId = null) =>
        IdadeMaximaEmissao.Criar(valor, UnidadeIdade.Dias, referencia, null, faseId).Value!;

    [Fact(DisplayName = "A exigência sem prazo é aceita quando o prazo não é obrigatório")]
    public void SemPrazoEFacultativo_Aceita()
    {
        ConferenciaDoPrazoDeEmissao.Conferir([_inscricao, _habilitacao], [new PrazoDeEmissaoDaExigencia(PrazoObrigatorio: false, null)])
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "O comprovante do conjunto básico sem prazo é recusado")]
    public void ConjuntoBasicoSemPrazo_Recusa()
    {
        ConferenciaDoPrazoDeEmissao.Conferir([_inscricao, _habilitacao], [new PrazoDeEmissaoDaExigencia(PrazoObrigatorio: true, null)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(PrazoDeEmissaoErrorCodes.PrazoObrigatorio);
    }

    [Theory(DisplayName = "O comprovante do conjunto básico aceita o prazo declarado, qualquer que seja o valor")]
    [InlineData(30)]
    [InlineData(90)]
    public void ConjuntoBasicoComPrazo_AceitaOValorDeclarado(int dias)
    {
        ConferenciaDoPrazoDeEmissao.Conferir(
            [_inscricao, _habilitacao],
            [new PrazoDeEmissaoDaExigencia(PrazoObrigatorio: true, Idade(dias, ReferenciaTipoIdadeEmissao.FimInscricao))])
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A âncora de fase do cronograma é aceita")]
    public void AncoraDeFaseDoCronograma_Aceita()
    {
        ConferenciaDoPrazoDeEmissao.Conferir(
            [_inscricao, _habilitacao],
            [new PrazoDeEmissaoDaExigencia(false, Idade(90, ReferenciaTipoIdadeEmissao.InicioFase, _habilitacao.Id))])
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A âncora de fase fora do cronograma é recusada")]
    public void AncoraDeFaseForaDoCronograma_Recusa()
    {
        ConferenciaDoPrazoDeEmissao.Conferir(
            [_inscricao, _habilitacao],
            [new PrazoDeEmissaoDaExigencia(false, Idade(90, ReferenciaTipoIdadeEmissao.FimFase, Guid.CreateVersion7()))])
            .Should().ContainSingle().Which.Error.Code.Should().Be("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "A âncora de fase sem o extremo definido é recusada")]
    public void AncoraDeFaseSemExtremo_Recusa()
    {
        FaseCronograma semDatas = FaseDeTeste.Criar(4, "RESULTADO", comDatas: false);

        ConferenciaDoPrazoDeEmissao.Conferir(
            [_inscricao, semDatas],
            [new PrazoDeEmissaoDaExigencia(false, Idade(90, ReferenciaTipoIdadeEmissao.FimFase, semDatas.Id))])
            .Should().ContainSingle().Which.Error.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    [Fact(DisplayName = "O fim da inscrição sem fase que coleta inscrição é recusado")]
    public void FimDaInscricaoSemFaseDeInscricao_Recusa()
    {
        ConferenciaDoPrazoDeEmissao.Conferir(
            [_habilitacao], [new PrazoDeEmissaoDaExigencia(false, Idade(90, ReferenciaTipoIdadeEmissao.FimInscricao))])
            .Should().ContainSingle().Which.Error.Code.Should().Be("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "O fim da inscrição sem fim definido em nenhuma fase de inscrição é recusado")]
    public void FimDaInscricaoSemFim_Recusa()
    {
        FaseCronograma inscricaoSemDatas = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true, comDatas: false);

        ConferenciaDoPrazoDeEmissao.Conferir(
            [inscricaoSemDatas, _habilitacao], [new PrazoDeEmissaoDaExigencia(false, Idade(90, ReferenciaTipoIdadeEmissao.FimInscricao))])
            .Should().ContainSingle().Which.Error.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    [Fact(DisplayName = "A recusa aponta a exigência pelo caminho e não repete o identificador da fase")]
    public void Recusa_ApontaACaminhoSemEcoarAFase()
    {
        Guid faseInexistente = Guid.CreateVersion7();

        FieldError[] recusas = [.. ConferenciaDoPrazoDeEmissao.Conferir(
            [_inscricao],
            [new PrazoDeEmissaoDaExigencia(false, null), new PrazoDeEmissaoDaExigencia(false, Idade(90, ReferenciaTipoIdadeEmissao.InicioFase, faseInexistente))])];

        recusas.Single().Field.Should().Be("exigencias[1].idadeMaximaEmissao");
        recusas.Single().Error.Message.Should().NotContain(faseInexistente.ToString());
    }
}
