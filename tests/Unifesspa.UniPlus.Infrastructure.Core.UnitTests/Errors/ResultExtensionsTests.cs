namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Text.RegularExpressions;

using AwesomeAssertions;

using Kernel.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class ResultExtensionsTests
{
    private const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    private static readonly DomainErrorMapping MappingNaoEncontrado =
        new(StatusCodes.Status404NotFound, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado");

    private static readonly DomainErrorMapping MappingValidacao =
        new(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.ja_publicado", "Edital já publicado");

    private static readonly DomainErrorMapping MappingConflitoRetentavel = new(
        StatusCodes.Status409Conflict,
        "uniplus.configuracao.termo_consentimento.conflito_de_concorrencia",
        "O rascunho foi modificado concorrentemente",
        RetryableConflict: true);

    private static readonly DomainErrorMapping MappingSiglaObrigatoria =
        new(StatusCodes.Status422UnprocessableEntity, "uniplus.configuracao.campus.sigla_obrigatoria", "Sigla obrigatória");

    private static readonly DomainErrorMapping MappingNomeObrigatorio =
        new(StatusCodes.Status422UnprocessableEntity, "uniplus.configuracao.campus.nome_obrigatorio", "Nome obrigatório");

    private static readonly DomainErrorMapping MappingRecusaAposGravacao = new(
        StatusCodes.Status422UnprocessableEntity,
        "uniplus.selecao.edital.recusado_apos_gravacao",
        "Edital recusado depois da gravação",
        RecusaAposGravacao: true);

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

        problem.Type.Should().Be(BaseUriDoCatalogo + "uniplus.selecao.edital.nao_encontrado");
    }

    [Fact]
    public void ToActionResult_DadoCodigoNaoMapeado_TypeDeveUsarFallback()
    {
        IDomainErrorMapper mapper = CriarMapper();
        Result resultado = Result.Failure(new DomainError("Codigo.X", "Erro."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Type.Should().Be(BaseUriDoCatalogo + "uniplus.erro_nao_mapeado");
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

    // ─── ValidationFailure multi-erro — errors[] (ADR-0023, ADR-0125) ────

    /// <remarks>
    /// ADR-0023: errors[] é condicional a ter Field associado (ValidationFailure), não a ter mais de
    /// uma violação — uma única Sigla vazia ainda é erro de validação e o cliente precisa achar o
    /// field sem tratar "erro único" como caso especial.
    /// </remarks>
    [Fact]
    public void ToActionResult_ComValidationFailureDeUmErro_DeveConterExtensionErrorsComUmElemento()
    {
        IDomainErrorMapper mapper = CriarMapper(("Campus.SiglaObrigatoria", MappingValidacao));
        Result resultado = Result.ValidationFailure(
            [new FieldError("Sigla", new DomainError("Campus.SiglaObrigatoria", "Sigla obrigatória."))]);

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        dynamic[] erros = ItensDeErrors(problem);
        ((int)erros.Length).Should().Be(1);
        ((string)erros[0].field).Should().Be("Sigla");
    }

    [Fact]
    public void ToActionResult_ComConflitoSemCampo_NaoDeveConterExtensionErrors()
    {
        // Um Failure comum (ex.: SiglaJaExiste, 409) vira FieldError(null, error) só
        // pela forma interna do Result — a violação não tem campo, então errors[]
        // continua fora do wire.
        IDomainErrorMapper mapper = CriarMapper(
            ("Campus.SiglaJaExiste", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.configuracao.campus.sigla_ja_existe", "Sigla já existe")));
        Result resultado = Result.Failure(new DomainError("Campus.SiglaJaExiste", "Já existe um Campus vivo com essa sigla."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Extensions.Should().NotContainKey("errors");
    }

    [Fact]
    public void ToActionResult_ComConflitoAtribuidoACampo_DeveConterExtensionErrors()
    {
        IDomainErrorMapper mapper = CriarMapper(
            ("Campus.SiglaJaExiste", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.configuracao.campus.sigla_ja_existe", "Sigla já existe")));
        Result resultado = Result.ValidationFailure(
            [new FieldError("Sigla", new DomainError("Campus.SiglaJaExiste", "Já existe um Campus vivo com essa sigla."))]);

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        dynamic[] erros = ItensDeErrors(problem);
        ((string)erros[0].field).Should().Be("Sigla");
    }

    // ─── Conflito retentável (ADR-0134) ────────────────────────────────────

    /// <remarks>
    /// O nome do campo e a posição dele (raiz do corpo) são contrato: o filtro de idempotência lê
    /// exatamente "retryable" na raiz da resposta para decidir se a reserva da chave é liberada.
    /// Uma renomeação silenciosa faria o conflito de corrida voltar a ocupar a chave por 24 h sem
    /// quebrar nenhum outro teste.
    /// </remarks>
    [Fact]
    public void ToActionResult_ComConflitoDeclaradoRetentavel_DeveTrazerRetryableNaRaizDoEnvelope()
    {
        IDomainErrorMapper mapper = CriarMapper(("TermoConsentimento.ConflitoDeConcorrencia", MappingConflitoRetentavel));
        Result resultado = Result.Failure(new DomainError(
            "TermoConsentimento.ConflitoDeConcorrencia", "O rascunho foi modificado concorrentemente."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Extensions.Should().ContainKey("retryable");
        problem.Extensions["retryable"].Should().Be(true);
    }

    [Fact]
    public void ToActionResult_ComConflitoDuravel_NaoDeveTrazerRetryable()
    {
        // A ausência é o sinal de "não conte com repetir": um campo presente em toda
        // resposta de erro seria ruído, e o conflito de estado que permanece não deve
        // prometer ao cliente uma repetição que dará o mesmo resultado.
        IDomainErrorMapper mapper = CriarMapper((
            "Campus.SiglaJaExiste",
            new DomainErrorMapping(
                StatusCodes.Status409Conflict, "uniplus.configuracao.campus.sigla_ja_existe", "Sigla já existe")));
        Result resultado = Result.Failure(
            new DomainError("Campus.SiglaJaExiste", "Já existe um Campus vivo com essa sigla."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Extensions.Should().NotContainKey("retryable");
    }

    [Fact]
    public void ToActionResult_ComCodigoNaoMapeado_NaoDeveTrazerRetryable()
    {
        // Não se afirma sobre um código que o catálogo não conhece, e o lado seguro é o
        // que preserva a resposta guardada.
        IDomainErrorMapper mapper = CriarMapper();
        Result resultado = Result.Failure(new DomainError("Codigo.Desconhecido", "Erro desconhecido."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Extensions.Should().NotContainKey("retryable");
    }

    [Fact]
    public void ToActionResult_ComFailureDeRegraDeNegocioMapeadoPara422_NaoDeveConterExtensionErrors()
    {
        // Nem todo 422 é erro de validação de campo: um Result.Failure comum (ex.:
        // Campus responsável não encontrado, snapshot vigente ausente) também
        // resolve para 422 por regra de negócio, sem nenhum "field" associado — a
        // condição não pode ser "status == 422", ou o array sairia com field: null.
        IDomainErrorMapper mapper = CriarMapper(
            ("LocalOferta.CampusResponsavelNaoEncontrado", MappingValidacao));
        Result resultado = Result.Failure(
            new DomainError("LocalOferta.CampusResponsavelNaoEncontrado", "O Campus responsável informado não foi encontrado."));

        ProblemDetails problem = ExtrairProblemDetails(resultado.ToActionResult(mapper));

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Extensions.Should().NotContainKey("errors");
    }

    [Fact]
    public void ToActionResult_ComVariasViolacoes_RaizDescreveAPrimeira()
    {
        IDomainErrorMapper mapper = CriarMapper(
            ("Campus.SiglaObrigatoria", MappingSiglaObrigatoria),
            ("Campus.NomeObrigatorio", MappingNomeObrigatorio));

        ProblemDetails problem = ExtrairProblemDetails(ViolacoesDeSiglaENome().ToActionResult(mapper));

        problem.Extensions["code"].Should().Be("uniplus.configuracao.campus.sigla_obrigatoria");
        problem.Title.Should().Be("Sigla obrigatória");
        problem.Detail.Should().Be("Sigla do Campus é obrigatória.");
    }

    [Fact]
    public void ToActionResult_ComVariasViolacoes_ErrorsTrazCadaCampoComOCodeTraduzido()
    {
        IDomainErrorMapper mapper = CriarMapper(
            ("Campus.SiglaObrigatoria", MappingSiglaObrigatoria),
            ("Campus.NomeObrigatorio", MappingNomeObrigatorio));

        ProblemDetails problem = ExtrairProblemDetails(ViolacoesDeSiglaENome().ToActionResult(mapper));

        dynamic[] erros = ItensDeErrors(problem);
        ((int)erros.Length).Should().Be(2);
        ((string)erros[0].field).Should().Be("Sigla");
        ((string)erros[0].code).Should().Be("uniplus.configuracao.campus.sigla_obrigatoria");
        ((string)erros[1].field).Should().Be("Nome");
        ((string)erros[1].code).Should().Be("uniplus.configuracao.campus.nome_obrigatorio");
    }

    [Fact]
    public void ToActionResult_ComRecusaAposGravacao_DevolveRespostaDeRecusaAposGravacaoCom422()
    {
        IDomainErrorMapper mapper = CriarMapper(("Edital.RecusadoAposGravacao", MappingRecusaAposGravacao));
        Result resultado = Result.Failure(new DomainError("Edital.RecusadoAposGravacao", "Edital recusado depois da gravação."));

        IActionResult actionResult = resultado.ToActionResult(mapper);

        ObjectResult resposta = actionResult.Should().BeOfType<RespostaDeRecusaAposGravacao>().Subject;
        resposta.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
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

        // Só o registro de status/title é stub: o type sai da fábrica real, sobre a base
        // que o teste declara, para que a asserção descreva a URI que o consumidor recebe.
        ProblemTypeUriFactory fabricaDeType = new(
            Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo }));

        return new StubDomainErrorMapper(dict, fabricaDeType);
    }

    private static Result ViolacoesDeSiglaENome() =>
        Result.ValidationFailure(
        [
            new FieldError("Sigla", new DomainError("Campus.SiglaObrigatoria", "Sigla do Campus é obrigatória.")),
            new FieldError("Nome", new DomainError("Campus.NomeObrigatorio", "Nome do Campus é obrigatório.")),
        ]);

    private static dynamic[] ItensDeErrors(ProblemDetails problem)
    {
        object? erros = problem.Extensions["errors"];
        erros.Should().NotBeNull();
        return ((System.Collections.IEnumerable)erros!).Cast<dynamic>().ToArray();
    }

    private static ProblemDetails ExtrairProblemDetails(IActionResult actionResult)
    {
        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        return objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
    }

    private sealed class StubDomainErrorMapper(
        Dictionary<string, DomainErrorMapping> map,
        IProblemTypeUriFactory problemTypeUriFactory) : IDomainErrorMapper
    {
        public bool TryGetMapping(string code, out DomainErrorMapping mapping)
        {
            bool found = map.TryGetValue(code, out DomainErrorMapping? m);
            mapping = m!;
            return found;
        }

        public string GetProblemTypeUri(string code) => problemTypeUriFactory.Build(code);
    }
}
