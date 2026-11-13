namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// O cadastro separa o tipo que não veio do tipo que veio e não pertence ao vocabulário, e nenhuma
/// recusa devolve o valor recebido: só valores de catálogo resolvidos no servidor entram nas mensagens.
/// </summary>
public sealed class TipoESentidoDoCadastroDeMotivoTests
{
    private const string Descricao = "Renda familiar per capita acima do limite legal.";
    private const string CodigoRecusado = "codigo recusado marcador";
    private const string SubclassificacaoRecusada = "SUBCLASSIFICACAO_RECUSADA_MARCADOR";
    private const string DescricaoRecusada = "descricao recusada marcador\0";

    [Theory(DisplayName = "Tipo ausente ou em branco é recusado como obrigatório e lista os tipos aceitos")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_TipoAusente_RecusaComoObrigatorio(string? tipo)
    {
        Result<CamposValidados> resultado = Validar(tipo);

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("tipo");
        erro.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.TipoObrigatorio);
        erro.Error.Message.Should().StartWith("Informe o tipo do motivo.").And.Contain(TipoMotivoDeDecisaoCodigo.CodigosEmTexto);
    }

    [Theory(DisplayName = "Texto fora do vocabulário é recusado como inválido, sem repetir o valor recebido")]
    [InlineData("RECURSO_DA_BANCA")]
    [InlineData("isencao_da_taxa")]
    [InlineData("NENHUM")]
    public void Validar_TipoDesconhecido_RecusaComoInvalidoSemOValor(string tipo)
    {
        Result<CamposValidados> resultado = Validar(tipo);

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("tipo");
        erro.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.TipoInvalido);
        erro.Error.Message.Should().Contain(TipoMotivoDeDecisaoCodigo.CodigosEmTexto).And.NotContain(tipo);
    }

    [Theory(DisplayName = "Cada código do vocabulário é convertido para o tipo correspondente")]
    [InlineData(TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, TipoMotivoDeDecisao.IsencaoDaTaxa)]
    [InlineData(TipoMotivoDeDecisaoCodigo.Classificacao, TipoMotivoDeDecisao.Classificacao)]
    public void Converter_CodigoDoVocabulario_DevolveOTipo(string codigo, TipoMotivoDeDecisao esperado)
    {
        TipoDoMotivoInformado.Converter(codigo).Value.Should().Be(esperado);
    }

    [Fact(DisplayName = "Sentido não admitido pelo tipo é recusado sem citar o sentido recebido")]
    public void Validar_SentidoNaoAdmitido_RecusaSemCitarOSentido()
    {
        CamposDoMotivo campos = new(TipoMotivoDeDecisaoCodigo.Classificacao, "NOTA_ZERADA", Descricao, null, (SentidoDoMotivo)99);

        Result<CamposValidados> resultado = ValidacaoDoMotivo.Validar(campos);

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("sentido");
        erro.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.SentidoNaoAdmitido);
        erro.Error.Message.Should().Be("O sentido informado não é admitido para este tipo de motivo.");
    }

    [Fact(DisplayName = "Nenhuma mensagem do cadastro repete o valor recusado em código, subclassificação ou descrição")]
    public void Validar_CamposInvalidos_NaoRepeteOsValoresRecebidos()
    {
        Result<CamposValidados> resultado = ValidacaoDoMotivo.Validar(new CamposDoMotivo(
            TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, CodigoRecusado, DescricaoRecusada, SubclassificacaoRecusada, SentidoDoMotivo.Nenhum));

        resultado.Errors.Select(erro => erro.Error.Code).Should().BeEquivalentTo(
        [
            MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido,
            MotivoDeDecisaoErrorCodes.DescricaoCaractereInvalido,
            MotivoDeDecisaoErrorCodes.SubclassificacaoInvalida,
            MotivoDeDecisaoErrorCodes.SentidoObrigatorio,
        ]);
        resultado.Errors.Select(erro => erro.Error.Message).Should().OnlyContain(mensagem =>
            !mensagem.Contains("marcador", StringComparison.OrdinalIgnoreCase));
    }

    private static Result<CamposValidados> Validar(string? tipo) =>
        ValidacaoDoMotivo.Validar(new CamposDoMotivo(tipo, "RENDA_ACIMA_DO_LIMITE", Descricao, null, SentidoDoMotivo.Desfavoravel));
}
