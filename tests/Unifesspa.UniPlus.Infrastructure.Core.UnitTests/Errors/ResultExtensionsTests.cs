namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public class ResultExtensionsTests
{
    private static readonly DomainErrorMapping MappingNaoEncontrado =
        new(StatusCodes.Status404NotFound, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado");

    private static readonly DomainErrorMapping MappingValidacao =
        new(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.ja_publicado", "Edital já publicado");

    // ─── Status HTTP correto ───────────────────────────────────────────────

    [Fact]
    public void ToActionResult_DadoCodigoMapeadoParaNotFound_DeveRetornar404()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.NaoEncontrado", MappingNaoEncontrado));
        Result resultado = Result.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        ObjectResult actionResult = (ObjectResult)resultado.ToActionResult(mapper);

        actionResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToActionResult_DadoCodigoMapeadoParaUnprocessable_DeveRetornar422()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.JaPublicado", MappingValidacao));
        Result resultado = Result.Failure(new DomainError("Edital.JaPublicado", "Edital já foi publicado."));

        ObjectResult actionResult = (ObjectResult)resultado.ToActionResult(mapper);

        actionResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void ToActionResult_DadoCodigoNaoMapeado_DeveRetornarFallback400()
    {
        IDomainErrorMapper mapper = CriarMapper();
        Result resultado = Result.Failure(new DomainError("Codigo.Desconhecido", "Erro desconhecido."));

        ObjectResult actionResult = (ObjectResult)resultado.ToActionResult(mapper);

        actionResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // ─── ProblemDetails — type URI ─────────────────────────────────────────

    [Fact]
    public void ToActionResult_DadoCodigoMapeado_TypeDeveConterUriBaseComCode()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.NaoEncontrado", MappingNaoEncontrado));
        Result resultado = Result.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Type.Should().Be("https://uniplus.unifesspa.edu.br/errors/uniplus.selecao.edital.nao_encontrado");
    }

    [Fact]
    public void ToActionResult_DadoCodigoNaoMapeado_TypeDeveUsarFallback()
    {
        IDomainErrorMapper mapper = CriarMapper();
        Result resultado = Result.Failure(new DomainError("Codigo.X", "Erro."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Type.Should().Be("https://uniplus.unifesspa.edu.br/errors/uniplus.erro_nao_mapeado");
    }

    // ─── Extensão "code" ──────────────────────────────────────────────────

    [Fact]
    public void ToActionResult_DadoCodigoMapeado_ExtensionCodeDeveSerTaxonomiaCompleta()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.NaoEncontrado", MappingNaoEncontrado));
        Result resultado = Result.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Extensions["code"].Should().Be("uniplus.selecao.edital.nao_encontrado");
    }

    [Fact]
    public void ToActionResult_DadoCodigoNaoMapeado_ExtensionCodeDeveTerFallback()
    {
        IDomainErrorMapper mapper = CriarMapper();
        Result resultado = Result.Failure(new DomainError("Codigo.X", "Erro."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Extensions["code"].Should().Be("uniplus.erro_nao_mapeado");
    }

    // ─── Extensão "traceId" — 32 hex lowercase (W3C) ─────────────────────

    [Fact]
    public void ToActionResult_SemActivityCorrente_TraceIdDeveTer32HexLowercase()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.NaoEncontrado", MappingNaoEncontrado));
        Result resultado = Result.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        string? traceId = problem.Extensions["traceId"]?.ToString();
        traceId.Should().NotBeNullOrEmpty();
        traceId!.Length.Should().Be(32);
        Regex.IsMatch(traceId, "^[0-9a-f]{32}$").Should().BeTrue("traceId deve ser 32 caracteres hex lowercase (W3C)");
    }

    // ─── Extensão "instance" — URN uuid ───────────────────────────────────

    [Fact]
    public void ToActionResult_InstanceDeveSerUrnUuid()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.NaoEncontrado", MappingNaoEncontrado));
        Result resultado = Result.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Instance.Should().StartWith("urn:uuid:");
        Guid.TryParse(problem.Instance!["urn:uuid:".Length..], out _).Should().BeTrue();
    }

    // ─── Overload genérico Result<T> ──────────────────────────────────────

    [Fact]
    public void ToActionResultGenerico_DadoCodigoMapeado_DeveRetornarStatusCorreto()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.NaoEncontrado", MappingNaoEncontrado));
        Result<Guid> resultado = Result<Guid>.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        ObjectResult actionResult = (ObjectResult)resultado.ToActionResult(mapper);

        actionResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    // ─── Guards — IsSuccess = true lança ──────────────────────────────────

    [Fact]
    public void ToActionResult_DadoIsSuccessTrue_DeveLancarInvalidOperationException()
    {
        IDomainErrorMapper mapper = CriarMapper();
        Result resultado = Result.Success();

        Action acao = () => resultado.ToActionResult(mapper);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToActionResultGenerico_DadoIsSuccessTrue_DeveLancarInvalidOperationException()
    {
        IDomainErrorMapper mapper = CriarMapper();
        Result<Guid> resultado = Result<Guid>.Success(Guid.NewGuid());

        Action acao = () => resultado.ToActionResult(mapper);

        acao.Should().Throw<InvalidOperationException>();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static StubDomainErrorMapper CriarMapper(params (string Code, DomainErrorMapping Mapping)[] entradas)
    {
        Dictionary<string, DomainErrorMapping> dict = entradas
            .ToDictionary(e => e.Code, e => e.Mapping, StringComparer.OrdinalIgnoreCase);
        return new StubDomainErrorMapper(dict);
    }

    private static ProblemDetails ExtrairProblemDetails(IActionResult actionResult)
    {
        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        return objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
    }

    private sealed class StubDomainErrorMapper(Dictionary<string, DomainErrorMapping> map) : IDomainErrorMapper
    {
        public bool TryGetMapping(string code, out DomainErrorMapping mapping)
        {
            bool found = map.TryGetValue(code, out DomainErrorMapping? m);
            mapping = m!;
            return found;
        }
    }
}
