namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>A regra de homologação das inscrições é montada pelos valores do domínio; o que eles recusam acumula, aponta o campo de dentro da regra e recusa a regra inteira, sem repetir o texto digitado.</summary>
public sealed class LeituraDaRegraDeHomologacaoDaFaseTests
{
    private const string Motivo = "SEM_RESULTADO_NO_RETORNO";
    private const string CaminhoDaRegra = "/cronograma/fases/0/regraDeHomologacao";

    private static FaseNoDocumento ComHomologacao(params CondicaoNoDocumento[] condicoes) =>
        ComAlternativas(new AlternativaNoDocumento(condicoes));

    private static FaseNoDocumento ComAlternativas(params AlternativaNoDocumento[] alternativas) =>
        Fase(0, "homologacao", "RESULTADO") with
        {
            Publicacoes = new([], false, null, new RegraDeHomologacaoNoDocumento(AtoDoResultado, alternativas)),
        };

    private static CondicaoNoDocumento ComEnquadramento(string json) =>
        Condicao(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2025, Motivo) with { Enquadramento = JsonDocument.Parse(json).RootElement };

    [Fact(DisplayName = "A regra com condição válida entra na fase com o resultado e o motivo que o documento declara")]
    public void Regra_Valida_EntraNaFase()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao(Condicao(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2025, Motivo))));

        lidos.Cronograma.CronogramaFases.Single().RegraDeHomologacao!.ResultadoAtoCodigo.Should().Be(AtoDoResultado);
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O enquadramento válido entra na condição, montado pelo predicado do domínio")]
    public void Enquadramento_Valido_EntraNaCondicao()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao(ComEnquadramento("[[{\"fato\":\"cota\",\"operador\":\"IGUAL\",\"valor\":\"A\"}]]"))));

        lidos.Cronograma.CronogramaFases.Single().RegraDeHomologacao!.Alternativas.Single().Condicoes.Single().Enquadramento.Should().NotBeNull();
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O enquadramento de forma inválida é recusado no campo do enquadramento, sem exceção")]
    public void Enquadramento_DeFormaInvalida_ApontaOCampo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao(ComEnquadramento("{}"))));

        lidos.Cronograma.CronogramaFases.Single().RegraDeHomologacao.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(($"{CaminhoDaRegra}/alternativas/0/condicoes/0/enquadramento", "PredicadoDnf.FormaJsonInvalida"));
    }

    [Fact(DisplayName = "A condição do enquadramento que o domínio recusa é recusada no mesmo campo")]
    public void Enquadramento_ComCondicaoIncoerente_ApontaOCampo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao(ComEnquadramento("[[{\"fato\":\"cota\",\"operador\":\"EM\",\"valor\":\"A\"}]]"))));

        PartesRecusadas(lidos).Should().Equal(($"{CaminhoDaRegra}/alternativas/0/condicoes/0/enquadramento", "CondicaoDnf.FormaIncoerenteComOperador"));
    }

    [Fact(DisplayName = "O tipo de condição ausente ou que o sistema não apura recusa a regra no campo da condição")]
    public void Condicao_SemTipoApurado_ApontaACondicao()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao(Condicao(null, 2025, Motivo))));

        lidos.Cronograma.CronogramaFases.Single().RegraDeHomologacao.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(($"{CaminhoDaRegra}/alternativas/0/condicoes/0", HomologacaoInscricaoErrorCodes.CondicaoNaoDisponivel));
    }

    [Fact(DisplayName = "Edição do exame inválida e motivo mal formado na mesma alternativa acumulam as duas causas, cada uma na sua condição")]
    public void Condicoes_Invalidas_Acumulam()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao(
            Condicao(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 1900, Motivo),
            Condicao(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2025, "motivo mal formado"))));

        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal(
            $"{CaminhoDaRegra}/alternativas/0/condicoes/0",
            $"{CaminhoDaRegra}/alternativas/0/condicoes/1");
    }

    [Fact(DisplayName = "A edição e o motivo ausentes na mesma condição acumulam as recusas")]
    public void Condicao_SemEdicaoEMotivo_AcumulaRecusas()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao(
            new CondicaoNoDocumento(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, null, null, null))));

        (string? Ponteiro, string Codigo)[] recusas = PartesRecusadas(lidos);
        recusas.Should().HaveCount(2);
        recusas.Select(static parte => parte.Ponteiro).Should().Equal(
            $"{CaminhoDaRegra}/alternativas/0/condicoes/0",
            $"{CaminhoDaRegra}/alternativas/0/condicoes/0");
        recusas[0].Item2.Should().Be(HomologacaoInscricaoErrorCodes.EdicaoDoExameInvalida);
        recusas[1].Item2.Should().Be(MotivoDeDecisaoErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "A alternativa sem condição recusa a regra no campo da alternativa")]
    public void Alternativa_SemCondicao_ApontaAAlternativa()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComHomologacao()));

        PartesRecusadas(lidos).Should().Equal(($"{CaminhoDaRegra}/alternativas/0", HomologacaoInscricaoErrorCodes.AlternativaSemCondicao));
    }

    [Fact(DisplayName = "O resultado ausente recusa a regra no campo do resultado")]
    public void Resultado_Ausente_ApontaOResultado()
    {
        FaseNoDocumento fase = Fase(0, "homologacao", "RESULTADO") with
        {
            Publicacoes = new([], false, null, new RegraDeHomologacaoNoDocumento(null, [])),
        };

        PartesRecusadas(Ler(Documento(fase))).Should().Equal(($"{CaminhoDaRegra}/resultadoAtoCodigo", HomologacaoInscricaoErrorCodes.ResultadoNaoDeclarado));
    }

    [Fact(DisplayName = "A fase sem regra de homologação não tem parte recusada")]
    public void SemRegra_NaoTemParteRecusada()
    {
        Ler(Documento(Fase(0, "homologacao", "RESULTADO"))).Origem.PartesRecusadas.Should().BeEmpty();
    }
}
