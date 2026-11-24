namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>As regras do campo sobre as respostas anteriores (UNI-REQ-0145).</summary>
public sealed class RegrasDoCampoTests
{
    private const string Mensagem = "Quem tem vínculo com o PARFOR não pode se inscrever neste processo.";

    [Fact(DisplayName = "Sem obrigatoriedade ou sem restrições, a construção é defeito de quem chama")]
    public void Construtor_ObrigatoriedadeOuRestricoesNulas_Lanca()
    {
        Action semObrigatoriedade = () => _ = new RegrasDoCampo(null, null!, []);
        Action semRestricoes = () => _ = new RegrasDoCampo(null, Obrigatoriedade.Nunca, null!);

        semObrigatoriedade.Should().Throw<ArgumentNullException>();
        semRestricoes.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "A lista de restrições é copiada: mudar a original depois não muda as regras")]
    public void Construtor_ListaDeRestricoes_ECopiada()
    {
        List<RestricaoValor> original = [new MunicipiosDaUf("UF")];
        RegrasDoCampo regras = new(null, Obrigatoriedade.Nunca, original);

        original.Clear();

        regras.Restricoes.Should().ContainSingle();
    }

    [Fact(DisplayName = "Os fatos citados reúnem exibição, obrigatoriedade e restrições, sem repetição, na ordem")]
    public void FatosCitadosPor_TodasAsRegras_SemRepeticao()
    {
        RegrasDoCampo regras = new(
            Predicado(("A", true), ("B", true)),
            Obrigatoriedade.Quando(Predicado(("B", true), ("C", true))),
            [new MunicipiosDaUf("D")]);

        regras.FatosCitadosPor("CAMPO").Should().Equal("A", "B", "C", "D");
    }

    [Fact(DisplayName = "O impedimento cita a resposta do próprio campo, que não é dependência")]
    public void FatosCitadosPor_ImpedimentoCitandoOProprioCampo_NaoContaOProprioCampo()
    {
        RegrasDoCampo regras = new(null, Obrigatoriedade.Nunca, [], new Impedimento(Predicado(("CAMPO", true), ("ANTERIOR", true)), Mensagem));

        regras.FatosCitadosPor("CAMPO").Should().Equal("ANTERIOR");
    }

    [Fact(DisplayName = "Sem regra nenhuma, nenhum fato é citado")]
    public void FatosCitadosPor_SemRegras_Vazio() =>
        new RegrasDoCampo(null, Obrigatoriedade.Sempre, []).FatosCitadosPor("CAMPO").Should().BeEmpty();

    [Fact(DisplayName = "As restrições ficam na ordem do tipo, e o resto das regras não muda")]
    public void ComRestricoesNaOrdemDoTipo_RestricoesDesordenadas_OrdenaPeloTipo()
    {
        PredicadoDnf exibicao = Predicado(("A", true));
        RegrasDoCampo regras = new(exibicao, Obrigatoriedade.Sempre, [new MunicipiosDaUf("UF"), new TamanhoTexto(1, 10)]);

        RegrasDoCampo ordenadas = regras.ComRestricoesNaOrdemDoTipo();

        ordenadas.Restricoes.Select(static r => r.Tipo).Should().Equal(TipoRestricaoValor.TamanhoTexto, TipoRestricaoValor.MunicipiosDaUf);
        ordenadas.Exibicao.Should().BeSameAs(exibicao);
        ordenadas.Obrigatoriedade.Should().Be(Obrigatoriedade.Sempre);
        regras.Restricoes.Select(static r => r.Tipo).Should().Equal(TipoRestricaoValor.MunicipiosDaUf, TipoRestricaoValor.TamanhoTexto);
    }

    [Fact(DisplayName = "Regras que não citam o próprio campo e cabem no tipo não são recusadas")]
    public void ConferirNoCampo_RegrasCoerentes_SemRecusa() =>
        new RegrasDoCampo(Predicado(("ANTERIOR", true)), Obrigatoriedade.Sempre, []).ConferirNoCampo("CAMPO", TipoRenderizacao.SimOuNao).Should().BeEmpty();

    [Fact(DisplayName = "A exibição e a obrigatoriedade que citam o próprio campo são recusadas, nessa ordem")]
    public void ConferirNoCampo_ExibicaoEObrigatoriedadeAutorreferentes_RecusaAsDuas()
    {
        RegrasDoCampo regras = new(Predicado(("CAMPO", true)), Obrigatoriedade.Quando(Predicado(("CAMPO", true))), []);

        regras.ConferirNoCampo("CAMPO", TipoRenderizacao.SimOuNao).Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("exibicao", ItemFormularioErrorCodes.RegraAutorreferente),
            ("obrigatoriedade.predicado", ItemFormularioErrorCodes.RegraAutorreferente));
    }

    [Fact(DisplayName = "A restrição fora do tipo do campo é recusada na posição dela")]
    public void ConferirNoCampo_RestricaoForaDoTipo_RecusaNaPosicao()
    {
        RegrasDoCampo regras = new(null, Obrigatoriedade.Nunca, [new TamanhoTexto(1, 10)]);

        FieldError erro = regras.ConferirNoCampo("CAMPO", TipoRenderizacao.SimOuNao).Should().ContainSingle().Subject;

        erro.Field.Should().Be("restricoes[0]");
        erro.Error.Code.Should().Be(ItemFormularioErrorCodes.RestricaoIncoerente);
    }

    [Fact(DisplayName = "O impedimento num campo que não cabe é recusado")]
    public void ConferirNoCampo_ImpedimentoEmCampoDeTexto_Recusa()
    {
        RegrasDoCampo regras = new(null, Obrigatoriedade.Nunca, [], new Impedimento(Predicado(("CAMPO", true)), Mensagem));

        regras.ConferirNoCampo("CAMPO", TipoRenderizacao.Texto).Should().ContainSingle()
            .Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ImpedimentoNaoCabeNoCampo);
    }

    private static PredicadoDnf Predicado(params (string Fato, object Valor)[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. condicoes.Select(static c => (0, CondicaoDnf.Criar(c.Fato, Operador.Igual, JsonSerializer.SerializeToElement(c.Valor)).Value!))]).Value!;
}
