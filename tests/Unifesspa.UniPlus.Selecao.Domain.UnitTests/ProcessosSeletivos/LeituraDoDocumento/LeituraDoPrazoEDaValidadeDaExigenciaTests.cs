namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.ExigenciasLidasDeTeste;

/// <summary>
/// A fase que a data de referência cita e que o cronograma não tem recusa o campo da chave, e as recusas dos demais campos do prazo e
/// da validade, que não dependem da referência, voltam junto.
/// </summary>
public sealed class LeituraDoPrazoEDaValidadeDaExigenciaTests
{
    private const string FaseDigitada = "digitada";

    private static DataDeReferenciaNoDocumento DaFase(string chave) => new(TipoDaDataDeReferencia.InicioDaFase, null, chave);

    [Fact(DisplayName = "O prazo com fase inexistente acumula a recusa da fase com a do valor, da unidade e do efeito")]
    public void Prazo_FaseInexistente_AcumulaOsDemaisCampos()
    {
        PrazoDeEmissaoNoDocumento prazo = new(EscolhaDoPrazoDeEmissao.ComPrazo, 0, null, DaFase(FaseDigitada), null);

        ExigenciasLidas lidas = Ler(Exigencia(0, "a") with { PrazoDeEmissao = prazo });

        Causas(lidas).Should().Equal(
            ("/documentosExigidos/exigencias/0/prazoDeEmissao/referencia/faseChave", "DataDeReferenciaDoDocumento.FaseNaoPertenceAoProcesso"),
            ("/documentosExigidos/exigencias/0/prazoDeEmissao/valor", "PrazoDeEmissao.ValorInvalido"),
            ("/documentosExigidos/exigencias/0/prazoDeEmissao/unidade", "PrazoDeEmissao.UnidadeInvalida"),
            ("/documentosExigidos/exigencias/0/prazoDeEmissao/efeitoForaDoPrazo", "EfeitoNaAnaliseDoDocumento.Invalido"));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains(FaseDigitada, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O prazo com fase inexistente e os demais campos válidos recusa só a fase")]
    public void Prazo_FaseInexistenteEDemaisCamposValidos_RecusaSoAFase()
    {
        PrazoDeEmissaoNoDocumento prazo = new(
            EscolhaDoPrazoDeEmissao.ComPrazo, 90, UnidadeDoPrazoDeEmissao.Dias, DaFase(FaseDigitada), EfeitoNaAnaliseDoDocumento.RecusarDocumento);

        Causas(Ler(Exigencia(0, "a") with { PrazoDeEmissao = prazo })).Should().Equal(
            ("/documentosExigidos/exigencias/0/prazoDeEmissao/referencia/faseChave", "DataDeReferenciaDoDocumento.FaseNaoPertenceAoProcesso"));
    }

    [Fact(DisplayName = "O prazo sem prazo escolhido não ganha recusa dos detalhes quando a fase não existe")]
    public void Prazo_SemPrazo_NaoGanhaRecusaDeValor()
    {
        PrazoDeEmissaoNoDocumento prazo = new(EscolhaDoPrazoDeEmissao.SemPrazo, null, null, DaFase(FaseDigitada), null);

        Causas(Ler(Exigencia(0, "a") with { PrazoDeEmissao = prazo })).Should().Equal(
            ("/documentosExigidos/exigencias/0/prazoDeEmissao/referencia/faseChave", "DataDeReferenciaDoDocumento.FaseNaoPertenceAoProcesso"));
    }

    [Fact(DisplayName = "A validade com fase inexistente acumula a recusa da fase com a do efeito do vencimento")]
    public void Validade_FaseInexistente_AcumulaOEfeito()
    {
        ValidadeNoDocumento validade = new(EscolhaDaValidade.ExigeDocumentoValido, DaFase(FaseDigitada), null);

        Causas(Ler(Exigencia(0, "a") with { Validade = validade })).Should().Equal(
            ("/documentosExigidos/exigencias/0/validade/referencia/faseChave", "DataDeReferenciaDoDocumento.FaseNaoPertenceAoProcesso"),
            ("/documentosExigidos/exigencias/0/validade/efeitoDoVencimento", "EfeitoNaAnaliseDoDocumento.Invalido"));
    }
}
