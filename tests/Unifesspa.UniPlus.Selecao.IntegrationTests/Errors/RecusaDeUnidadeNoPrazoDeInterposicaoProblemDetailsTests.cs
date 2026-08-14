namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// O que o administrador lê quando declara no prazo de interposição uma unidade que ele não
/// admite. As duas metades da resposta vêm de fontes independentes — o <c>title</c> do
/// registro de erros do módulo, o <c>detail</c> da mensagem que
/// <see cref="RegraRecursoFase.Criar"/> devolve — e só se encontram no
/// <see cref="ProblemDetails"/>. Uma metade certa e a outra errada é resposta errada, então
/// o teste percorre o caminho inteiro com os dois artefatos reais.
/// </summary>
/// <remarks>
/// As duas recusas têm causas e remediações distintas (UNI-REQ-0113), e por isso códigos
/// distintos: dia corrido encolheria a janela sempre que calhasse de cair em feriado, e se
/// resolve reescrevendo na unidade que o edital usa; fração de dia útil não tem leitura
/// unívoca, e se resolve declarando em horas. Cada <c>detail</c> precisa levar à sua saída —
/// mandar quem declarou fração "usar a unidade do edital" não ajudaria.
/// </remarks>
public sealed class RecusaDeUnidadeNoPrazoDeInterposicaoProblemDetailsTests
{
    private const string BaseUriDeErros = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    private static readonly IDomainErrorMapper MapperDoModulo = CriarMapperDoModulo();

    [Fact(DisplayName = "Dia corrido na interposição: 422 com title e detail que orientam dias úteis ou horas")]
    public void PrazoEmDiasCorridos_ProblemDetailsCompleto()
    {
        Result<RegraRecursoFase> resultado = Criar(prazoUnidade: UnidadePrazo.Dias, prazoValor: 5m);

        ProblemDetails problem = ProblemDetailsDe(resultado);

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Extensions["code"].Should().Be("uniplus.selecao.regra_recurso_fase.prazo_em_dias_corridos");
        problem.Type.Should().Be(BaseUriDeErros + "uniplus.selecao.regra_recurso_fase.prazo_em_dias_corridos");
        problem.Title.Should().Be("Prazo de interposição em dias corridos não é aceito");
        problem.Detail.Should().Be(
            "O prazo de interposição deve ser informado em dias úteis ou horas; dias corridos não são aceitos.");
    }

    [Fact(DisplayName = "Fração de dia útil: 422 com código próprio e detail que manda declarar em horas")]
    public void PrazoEmFracaoDeDiaUtil_ProblemDetailsCompleto()
    {
        Result<RegraRecursoFase> resultado = Criar(prazoUnidade: UnidadePrazo.DiasUteis, prazoValor: 1.5m);

        ProblemDetails problem = ProblemDetailsDe(resultado);

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Extensions["code"].Should().Be("uniplus.selecao.regra_recurso_fase.prazo_em_fracao_de_dia_util");
        problem.Type.Should().Be(BaseUriDeErros + "uniplus.selecao.regra_recurso_fase.prazo_em_fracao_de_dia_util");
        problem.Title.Should().Be("Prazo de interposição em dias úteis exige valor inteiro");
        problem.Detail.Should().Contain("horas",
            "quem declarou fração precisa ser levado à saída que serve ao caso dele, que é declarar em horas");
    }

    [Fact(DisplayName = "As duas recusas têm códigos distintos — remediação diferente não pode chegar com o mesmo type")]
    public void AsDuasRecusas_TemCodigosDistintos()
    {
        ProblemDetails corrido = ProblemDetailsDe(Criar(prazoUnidade: UnidadePrazo.Dias, prazoValor: 5m));
        ProblemDetails fracao = ProblemDetailsDe(Criar(prazoUnidade: UnidadePrazo.DiasUteis, prazoValor: 1.5m));

        corrido.Extensions["code"].Should().NotBe(fracao.Extensions["code"]);
        corrido.Type.Should().NotBe(fracao.Type,
            "o type resolve numa página do catálogo público, e as duas páginas explicam saídas diferentes");
    }

    [Theory(DisplayName = "Contraprova: suspensividade em dias úteis é aceita — é outro relógio, com outra regra")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SuspensividadeEmDiasUteis_NaoRecusa(bool primeiraEmDiasUteis, bool segundaEmDiasUteis)
    {
        Result<RegraRecursoFase> resultado = Criar(
            susp1Unidade: primeiraEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias,
            susp2Unidade: segundaEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias);

        resultado.IsSuccess.Should().BeTrue(
            "a suspensividade admite as três unidades; o que a contagem em dia útil exige é a convenção "
                + "declarada pelo processo, e essa é invariante da raiz, verificada ao gerar versão");
    }

    private static Result<RegraRecursoFase> Criar(
        UnidadePrazo prazoUnidade = UnidadePrazo.Horas,
        decimal prazoValor = 48m,
        UnidadePrazo? susp1Unidade = null,
        UnidadePrazo? susp2Unidade = null)
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        ArgsRegraPrazoRecurso args = new(
            PrazoValor: prazoValor,
            PrazoUnidade: prazoUnidade,
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: susp1Unidade is null ? null : 5m,
            SuspensividadePrimeiraInstanciaUnidade: susp1Unidade,
            SuspensividadeSegundaInstanciaValor: susp2Unidade is null ? null : 5m,
            SuspensividadeSegundaInstanciaUnidade: susp2Unidade);

        return RegraRecursoFase.Criar(regra, args);
    }

    private static ProblemDetails ProblemDetailsDe(Result<RegraRecursoFase> resultado)
    {
        resultado.IsFailure.Should().BeTrue("o teste só faz sentido sobre a recusa");

        ObjectResult actionResult = (ObjectResult)resultado.ToActionResult(MapperDoModulo);
        return (ProblemDetails)actionResult.Value!;
    }

    /// <summary>
    /// Monta o mapper pelo mesmo caminho da produção — o registro do módulo servido por DI —
    /// para que o teste leia os <c>title</c> que a API realmente devolve, e não uma cópia.
    /// </summary>
    private static IDomainErrorMapper CriarMapperDoModulo()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProblemTypeOptions.SectionName}:{nameof(ProblemTypeOptions.BaseUri)}"] = BaseUriDeErros,
            })
            .Build();

        ServiceCollection services = new();
        services.AddDomainErrorMapper(configuracao);
        services.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>();

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDomainErrorMapper>();
    }
}
