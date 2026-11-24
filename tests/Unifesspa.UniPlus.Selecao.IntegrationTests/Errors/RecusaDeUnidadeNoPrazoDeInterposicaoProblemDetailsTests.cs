namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
    private const string BaseUriDeErros = MapperDoModuloDeTeste.BaseUriDeErros;

    private static readonly IDomainErrorMapper MapperDoModulo = MapperDoModuloDeTeste.Criar();

    [Fact(DisplayName = "Dia corrido na interposição: 422 com title e detail que orientam dias úteis ou horas")]
    public void PrazoEmDiasCorridos_ProblemDetailsCompleto()
    {
        Result<RegraRecursoFase> resultado = Criar(UnidadePrazo.Dias, 5m);

        ProblemDetails problem = ProblemDetailsDe(resultado);

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Extensions["code"].Should().Be("uniplus.selecao.configuracao_do_recurso_da_fase.prazo_em_dias_corridos");
        problem.Type.Should().Be(BaseUriDeErros + "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_em_dias_corridos");
        problem.Title.Should().Be("O prazo do recurso não aceita dias corridos");
        problem.Detail.Should().Be(
            "O prazo de interposição deve ser informado em dias úteis ou em horas; dias corridos não são aceitos.");
    }

    [Fact(DisplayName = "Fração de dia útil: 422 com código próprio e detail que manda declarar em horas")]
    public void PrazoEmFracaoDeDiaUtil_ProblemDetailsCompleto()
    {
        Result<RegraRecursoFase> resultado = Criar(UnidadePrazo.DiasUteis, 1.5m);

        ProblemDetails problem = ProblemDetailsDe(resultado);

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Extensions["code"].Should().Be("uniplus.selecao.configuracao_do_recurso_da_fase.prazo_em_fracao_de_dia_util");
        problem.Type.Should().Be(BaseUriDeErros + "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_em_fracao_de_dia_util");
        problem.Title.Should().Be("O prazo do recurso em dias úteis exige número inteiro");
        problem.Detail.Should().Contain("HORAS",
            "quem declarou fração precisa ser levado à saída que serve ao caso dele, que é declarar em horas");
    }

    [Fact(DisplayName = "As duas recusas têm códigos distintos — remediação diferente não pode chegar com o mesmo type")]
    public void AsDuasRecusas_TemCodigosDistintos()
    {
        ProblemDetails corrido = ProblemDetailsDe(Criar(UnidadePrazo.Dias, 5m));
        ProblemDetails fracao = ProblemDetailsDe(Criar(UnidadePrazo.DiasUteis, 1.5m));

        corrido.Extensions["code"].Should().NotBe(fracao.Extensions["code"]);
        corrido.Type.Should().NotBe(fracao.Type,
            "o type resolve numa página do catálogo público, e as duas páginas explicam saídas diferentes");
    }

    [Fact(DisplayName = "Contraprova: o prazo da 2ª instância em dias úteis é aceito, e dia corrido nele é recusado no campo próprio")]
    public void PrazoDaSegundaInstancia_AceitaDiasUteisERecusaDiasCorridos()
    {
        Result<RegraRecursoFase> aceito = CriarComSegundaInstancia(UnidadePrazo.DiasUteis);
        Result<RegraRecursoFase> recusado = CriarComSegundaInstancia(UnidadePrazo.Dias);

        aceito.IsSuccess.Should().BeTrue();
        recusado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("RegraRecursoFase.PrazoEmDiasCorridos");
        ProblemDetailsDe(recusado).Detail.Should().Contain("prazo da 2ª instância");
    }

    private static Result<RegraRecursoFase> CriarComSegundaInstancia(UnidadePrazo unidade) =>
        RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64)),
            ParametrosDoRecursoDeTeste.Declarados(
                2m, UnidadePrazo.DiasUteis, admiteInstanciaSuperior: true, segundaInstancia: new PrazoDoRecurso(3m, unidade)),
            Guid.CreateVersion7());

    private static Result<RegraRecursoFase> Criar(UnidadePrazo prazoUnidade, decimal prazoValor) =>
        RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64)),
            ParametrosDoRecursoDeTeste.Declarados(prazoValor, prazoUnidade),
            Guid.CreateVersion7());

    private static ProblemDetails ProblemDetailsDe(Result<RegraRecursoFase> resultado)
    {
        resultado.IsFailure.Should().BeTrue("o teste só faz sentido sobre a recusa");

        ObjectResult actionResult = (ObjectResult)resultado.ToActionResult(MapperDoModulo);
        return (ProblemDetails)actionResult.Value!;
    }
}
