namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>As regras do campo lidas da entrada, em que a parte recusada na leitura é nula e conta como conforme.</summary>
public sealed class RegrasLidasDoCampoTests
{
    private const string Mensagem = "Quem tem vínculo com o PARFOR não pode se inscrever neste processo.";

    [Fact(DisplayName = "As regras de um campo lido por inteiro levam as mesmas partes")]
    public void De_RegrasDoCampo_LevaAsPartes()
    {
        PredicadoDnf exibicao = Predicado(("ANTERIOR", true));
        Impedimento impedimento = new(Predicado(("CAMPO", true)), Mensagem);
        RestricaoValor tamanho = new TamanhoTexto(1, 10);

        RegrasLidasDoCampo lidas = RegrasLidasDoCampo.De(new RegrasDoCampo(exibicao, Obrigatoriedade.Sempre, [tamanho], impedimento));

        lidas.Exibicao.Should().BeSameAs(exibicao);
        lidas.Obrigatoriedade.Should().Be(Obrigatoriedade.Sempre);
        lidas.Restricoes.Should().Equal(tamanho);
        lidas.Impedimento.Should().BeSameAs(impedimento);
    }

    [Fact(DisplayName = "Regras que não citam o próprio campo e cabem no tipo não são recusadas")]
    public void ConferirNoCampo_RegrasCoerentes_SemRecusa() =>
        Lidas(Predicado(("ANTERIOR", true)), Obrigatoriedade.Sempre).ConferirNoCampo("CAMPO", TipoDeCampo.SimOuNao).Should().BeEmpty();

    [Fact(DisplayName = "A exibição e a obrigatoriedade que citam o próprio campo são recusadas, nessa ordem")]
    public void ConferirNoCampo_ExibicaoEObrigatoriedadeAutorreferentes_RecusaAsDuas()
    {
        RegrasLidasDoCampo regras = Lidas(Predicado(("CAMPO", true)), Obrigatoriedade.Quando(Predicado(("CAMPO", true))));

        regras.ConferirNoCampo("CAMPO", TipoDeCampo.SimOuNao).Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("exibicao", ItemFormularioErrorCodes.RegraAutorreferente),
            ("obrigatoriedade.predicado", ItemFormularioErrorCodes.RegraAutorreferente));
    }

    [Fact(DisplayName = "A exibição e a obrigatoriedade recusadas na leitura não são conferidas")]
    public void ConferirNoCampo_ExibicaoEObrigatoriedadeNulas_SemRecusa() =>
        Lidas(null, null).ConferirNoCampo("CAMPO", TipoDeCampo.SimOuNao).Should().BeEmpty();

    [Fact(DisplayName = "A restrição fora do tipo do campo é recusada na posição dela")]
    public void ConferirNoCampo_RestricaoForaDoTipo_RecusaNaPosicao()
    {
        FieldError erro = Lidas(null, Obrigatoriedade.Nunca, new TamanhoTexto(1, 10)).ConferirNoCampo("CAMPO", TipoDeCampo.SimOuNao).Should().ContainSingle().Subject;

        erro.Field.Should().Be("restricoes[0]");
        erro.Error.Code.Should().Be(ItemFormularioErrorCodes.RestricaoIncoerente);
    }

    [Fact(DisplayName = "Sem o tipo do campo, a restrição fora do tipo não é recusada")]
    public void ConferirNoCampo_TipoNulo_NaoRecusaRestricaoForaDoTipo() =>
        Lidas(null, Obrigatoriedade.Nunca, new TamanhoTexto(1, 10)).ConferirNoCampo("CAMPO", null).Should().BeEmpty();

    [Fact(DisplayName = "A restrição recusada na leitura mantém a posição das demais")]
    public void ConferirNoCampo_RestricaoNulaNoMeio_MantemOIndiceDaSeguinte()
    {
        RegrasLidasDoCampo regras = new(null, Obrigatoriedade.Nunca, [null, new TamanhoTexto(1, 10)], null);

        regras.ConferirNoCampo("CAMPO", TipoDeCampo.SimOuNao).Should().ContainSingle().Which.Field.Should().Be("restricoes[1]");
    }

    [Fact(DisplayName = "A restrição que cita o próprio campo é recusada na posição dela")]
    public void ConferirNoCampo_RestricaoQueCitaOProprioCampo_Recusa() =>
        Lidas(null, Obrigatoriedade.Nunca, new MunicipiosDaUf("CAMPO")).ConferirNoCampo("CAMPO", TipoDeCampo.Municipio).Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "restricoes[0]" && e.Error.Code == ItemFormularioErrorCodes.RegraAutorreferente);

    [Fact(DisplayName = "Os limites da faixa com mais casas que o edital são recusados")]
    public void ConferirNoCampo_FaixaComCasasDemais_Recusa() =>
        Lidas(null, Obrigatoriedade.Nunca, new FaixaNumerica(0.00001m, null)).ConferirNoCampo("CAMPO", TipoDeCampo.Numero).Should().ContainSingle()
            .Which.Error.Code.Should().Be(RestricaoValorErrorCodes.LimitesIncoerentes);

    [Fact(DisplayName = "O tipo de restrição repetido é recusado, sem contar a restrição recusada na leitura")]
    public void ConferirNoCampo_TipoRepetido_RecusaSoSeAsDuasForemLidas()
    {
        RegrasLidasDoCampo repetidas = Lidas(null, Obrigatoriedade.Nunca, new TamanhoTexto(1, 10), new TamanhoTexto(2, 20));
        RegrasLidasDoCampo umaRecusada = new(null, Obrigatoriedade.Nunca, [new TamanhoTexto(1, 10), null], null);

        repetidas.ConferirNoCampo("CAMPO", TipoDeCampo.Texto).Should().ContainSingle().Which.Error.Code.Should().Be(RestricaoValorErrorCodes.TipoRepetido);
        umaRecusada.ConferirNoCampo("CAMPO", TipoDeCampo.Texto).Should().BeEmpty();
    }

    [Fact(DisplayName = "O campo de município sem a UF é recusado")]
    public void ConferirNoCampo_MunicipioSemUf_Recusa() =>
        Lidas(null, Obrigatoriedade.Nunca).ConferirNoCampo("CAMPO", TipoDeCampo.Municipio).Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "restricoes" && e.Error.Code == ItemFormularioErrorCodes.MunicipioSemUf);

    [Fact(DisplayName = "O campo de município com a UF não é recusado")]
    public void ConferirNoCampo_MunicipioComUf_SemRecusa() =>
        Lidas(null, Obrigatoriedade.Nunca, new MunicipiosDaUf("UF")).ConferirNoCampo("CAMPO", TipoDeCampo.Municipio).Should().BeEmpty();

    [Fact(DisplayName = "Sem o tipo do campo, o município sem UF não é recusado")]
    public void ConferirNoCampo_TipoNuloSemUf_SemRecusa() =>
        Lidas(null, Obrigatoriedade.Nunca).ConferirNoCampo("CAMPO", null).Should().BeEmpty();

    [Fact(DisplayName = "O município com restrição recusada na leitura não é recusado por falta da UF")]
    public void ConferirNoCampo_MunicipioComRestricaoRecusada_NaoCobraAUf()
    {
        RegrasLidasDoCampo regras = new(null, Obrigatoriedade.Nunca, [null], null);

        regras.ConferirNoCampo("CAMPO", TipoDeCampo.Municipio).Should().BeEmpty();
    }

    [Fact(DisplayName = "O impedimento num campo que não cabe é recusado")]
    public void ConferirNoCampo_ImpedimentoEmCampoDeTexto_Recusa() =>
        ComImpedimento(new Impedimento(Predicado(("CAMPO", true)), Mensagem)).ConferirNoCampo("CAMPO", TipoDeCampo.Texto).Should().ContainSingle()
            .Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ImpedimentoNaoCabeNoCampo);

    [Fact(DisplayName = "Sem o tipo do campo, o impedimento fora do tipo não é recusado")]
    public void ConferirNoCampo_ImpedimentoComTipoNulo_NaoConfereOTipo() =>
        ComImpedimento(new Impedimento(Predicado(("CAMPO", true)), Mensagem)).ConferirNoCampo("CAMPO", null).Should().BeEmpty();

    [Fact(DisplayName = "O impedimento sem a resposta do próprio campo e sem mensagem é recusado, mesmo sem o tipo")]
    public void ConferirNoCampo_ImpedimentoMalFormado_RecusaCondicaoEMensagem() =>
        ComImpedimento(new Impedimento(Predicado(("ANTERIOR", true)), "  ")).ConferirNoCampo("CAMPO", null).Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("impedimento.quando", ItemFormularioErrorCodes.ImpedimentoSemOProprioCampo),
            ("impedimento.mensagem", ItemFormularioErrorCodes.ImpedimentoMensagemInvalida));

    [Fact(DisplayName = "O impedimento recusado na leitura não é conferido")]
    public void ConferirNoCampo_ImpedimentoNulo_SemRecusa() =>
        ComImpedimento(null).ConferirNoCampo("CAMPO", TipoDeCampo.Texto).Should().BeEmpty();

    private static RegrasLidasDoCampo Lidas(PredicadoDnf? exibicao, Obrigatoriedade? obrigatoriedade, params RestricaoValor[] restricoes) =>
        new(exibicao, obrigatoriedade, restricoes, null);

    private static RegrasLidasDoCampo ComImpedimento(Impedimento? impedimento) =>
        new(null, Obrigatoriedade.Nunca, [], impedimento);

    private static PredicadoDnf Predicado(params (string Fato, object Valor)[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. condicoes.Select(static c => (0, CondicaoDnf.Criar(c.Fato, Operador.Igual, JsonSerializer.SerializeToElement(c.Valor)).Value!))]).Value!;
}
