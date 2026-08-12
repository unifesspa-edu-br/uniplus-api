namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
/// O que o administrador lê quando configura recurso em dias úteis. As duas metades da
/// resposta vêm de fontes independentes — o <c>title</c> do registro de erros do módulo, o
/// <c>detail</c> da mensagem que <see cref="RegraRecursoFase.Criar"/> devolve — e só se
/// encontram no <see cref="ProblemDetails"/>. Uma metade certa e a outra errada é resposta
/// errada, então o teste percorre o caminho inteiro com os dois artefatos reais.
/// </summary>
/// <remarks>
/// As duas recusas não têm a mesma causa (UNI-REQ-0080): no prazo de interposição, dias
/// úteis é proibido em definitivo — é o prazo que fecha a porta do candidato, e errar para
/// menos cercearia direito; na suspensividade, dias úteis é admissível, mas exige calendário
/// vigente e algoritmo de contagem versionado, e é o algoritmo que ainda não existe. Nenhuma
/// das duas decorre de ausência do calendário de dias úteis, que já é cadastrável no módulo
/// Configuração — daí a contraprova literal ao fim de cada caso.
/// <para>
/// Os dois <c>code</c> conservam o sufixo <c>sem_calendario</c>: são chave estável de
/// consumidor e integram o URI <c>type</c>. O teste fixa isso para que a correção editorial
/// não vire, por descuido, quebra de contrato.
/// </para>
/// </remarks>
public sealed class RecusaDiasUteisNoRecursoProblemDetailsTests
{
    private const string BaseUriDeErros = "https://uniplus.unifesspa.edu.br/errors/";

    private const string CausaDesmentida = "calendário";

    private static readonly IDomainErrorMapper MapperDoModulo = CriarMapperDoModulo();

    [Fact(DisplayName = "Prazo de interposição em dias úteis: 422 com title e detail que orientam horas ou dias corridos")]
    public void PrazoDeInterposicaoEmDiasUteis_ProblemDetailsCompleto()
    {
        Result<RegraRecursoFase> resultado = Criar(prazoUnidade: UnidadePrazo.DiasUteis);

        ProblemDetails problem = ProblemDetailsDe(resultado);

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Extensions["code"].Should().Be("uniplus.selecao.regra_recurso_fase.prazo_em_dias_uteis_sem_calendario");
        problem.Type.Should().Be(BaseUriDeErros + "uniplus.selecao.regra_recurso_fase.prazo_em_dias_uteis_sem_calendario");
        problem.Title.Should().Be("Prazo de interposição em dias úteis não permitido");
        problem.Detail.Should().Be(
            "O prazo de interposição deve ser informado em horas ou dias corridos; dias úteis não são aceitos.");

        problem.Title.Should().NotContain(CausaDesmentida);
        problem.Detail.Should().NotContain(CausaDesmentida,
            "a proibição é permanente e independe de calendário — atribuí-la ao calendário faria o "
                + "administrador cadastrar um dataset que não destrava a configuração");
    }

    [Theory(DisplayName = "Suspensividade em dias úteis: 422 com title e detail que apontam o algoritmo de contagem ausente")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SuspensividadeEmDiasUteis_ProblemDetailsCompleto(bool primeiraEmDiasUteis, bool segundaEmDiasUteis)
    {
        Result<RegraRecursoFase> resultado = Criar(
            susp1Unidade: primeiraEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias,
            susp2Unidade: segundaEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias);

        ProblemDetails problem = ProblemDetailsDe(resultado);

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Extensions["code"].Should().Be("uniplus.selecao.regra_recurso_fase.suspensividade_em_dias_uteis_sem_calendario");
        problem.Type.Should().Be(BaseUriDeErros + "uniplus.selecao.regra_recurso_fase.suspensividade_em_dias_uteis_sem_calendario");
        problem.Title.Should().Be("Suspensividade em dias úteis indisponível");
        problem.Detail.Should().Be(
            "A suspensividade em dias úteis, em qualquer uma das duas instâncias, exige calendário vigente e "
                + "algoritmo de contagem versionado; o algoritmo ainda não está disponível.");

        problem.Title.Should().NotContain(CausaDesmentida);
        problem.Detail.Should().NotContain("não há calendário",
            "o calendário de dias úteis já é cadastrável e pode estar vigente — o que falta é o "
                + "algoritmo de contagem versionado");
    }

    private static Result<RegraRecursoFase> Criar(
        UnidadePrazo prazoUnidade = UnidadePrazo.Horas,
        UnidadePrazo? susp1Unidade = null,
        UnidadePrazo? susp2Unidade = null)
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        ArgsRegraPrazoRecurso args = new(
            PrazoValor: 48m,
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
        ServiceCollection services = new();
        services.AddDomainErrorMapper();
        services.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>();

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDomainErrorMapper>();
    }
}
