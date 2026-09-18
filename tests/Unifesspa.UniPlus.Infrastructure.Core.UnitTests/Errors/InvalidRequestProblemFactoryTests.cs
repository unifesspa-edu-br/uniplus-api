namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// A recusa de binding se divide em duas, e confundir as duas orienta o cliente para o lado
/// errado: dizer "campo obrigatório não declarado" de um campo que veio — só que ilegível —
/// manda acrescentar o que já foi mandado.
/// </summary>
public sealed class InvalidRequestProblemFactoryTests
{
    private const string BaseDoCatalogo = "https://exemplo.invalid/erros/";

    [Fact(DisplayName = "Parâmetro que a requisição não traz é ausência: o code diz isso e o detail o nomeia")]
    public void CampoNaoFornecido_EhAusencia()
    {
        ActionContext contexto = Contexto(ParametroDeQuery("etapas"));
        // Nada fornecido: nem valor tentado, nem valor cru — não houve o que converter.
        contexto.ModelState.AddModelError("etapas", "The etapas field is required.");

        ProblemDetails problema = Executar(contexto);

        problema.Status.Should().Be(StatusCodes.Status400BadRequest);
        problema.Extensions["code"].Should().Be("uniplus.requisicao.campo_obrigatorio_ausente");
        problema.Detail.Should().Contain("etapas");
    }

    /// <summary>
    /// Campo que veio e não converte não é assunto deste factory: o valor FOI declarado, e a
    /// falha aconteceu no binding do parâmetro, não na leitura do corpo.
    /// </summary>
    /// <remarks>
    /// Devolver <see langword="null"/> aqui é o que preserva a mensagem de quem sabe mais sobre
    /// a causa. Uma recusa de <c>[RegularExpression]</c> num parâmetro de rota chega por este
    /// mesmo caminho e traz orientação escrita por nós, em pt-BR, dizendo qual é o formato
    /// esperado — capturá-la trocaria isso por "o corpo não pôde ser lido", que além de inútil
    /// seria falso, já que o binding deu certo.
    /// </remarks>
    [Fact(DisplayName = "Valor declarado que não converte é carga malformada — não campo ausente")]
    public void ValorPresenteQueNaoConverte_EhMalformada()
    {
        ActionContext contexto = Contexto(ParametroDeQuery("vigentes"));
        contexto.ModelState.SetModelValue("vigentes", rawValue: "abc", attemptedValue: "abc");
        // O binder guarda a EXCEÇÃO da conversão na entrada. É esse sinal que distingue
        // "não vira o tipo" de "virou o tipo e um validador reprovou depois".
        contexto.ModelState.TryAddModelException("vigentes", new FormatException("abc"));

        ProblemDetails problema = Executar(contexto);

        problema.Extensions["code"].Should().Be("uniplus.requisicao.malformada");
        problema.Detail.Should().NotContain("abc",
            "o envelope de erro não ecoa o valor rejeitado (ADR-0023)");
        problema.Detail.Should().NotContain("campo obrigatório",
            "o campo FOI declarado — mandá-lo declarar de novo é instrução que ele já cumpriu");
    }

    [Fact(DisplayName = "Validação que roda depois do binding não é capturada — a mensagem dela sobrevive")]
    public void ValidacaoAposBinding_Delega()
    {
        ActionContext contexto = Contexto();
        // É o caso de um [RegularExpression] em parâmetro de rota: o valor casou com o tipo, e
        // só depois um validador o reprovou.
        contexto.ModelState.SetModelValue("codigo", rawValue: "edital_abertura", attemptedValue: "edital_abertura");
        contexto.ModelState.AddModelError(
            "codigo",
            "Código do tipo de ato deve usar apenas letras maiúsculas sem acento, separadas por underscore.");

        InvalidRequestProblemFactory.TryBuild(contexto).Should().BeNull(
            "a mensagem do validador diz ao cliente o que fazer, e é melhor que qualquer "
            + "recusa genérica que este factory saiba escrever");
    }

    [Fact(DisplayName = "Parâmetro que casou não entra na recusa, mesmo quando outro falhou")]
    public void CampoValido_NaoEntraNaLista()
    {
        ActionContext contexto = Contexto(ParametroDeQuery("limite"), ParametroDeQuery("etapas"));
        // O ModelState traz TODAS as propriedades da requisição, não só as reprovadas.
        contexto.ModelState.SetModelValue("limite", rawValue: "10", attemptedValue: "10");
        contexto.ModelState.MarkFieldValid("limite");
        contexto.ModelState.AddModelError("etapas", "The etapas field is required.");

        ProblemDetails problema = Executar(contexto);

        problema.Detail.Should().Contain("etapas");
        problema.Detail.Should().NotContain("limite",
            "a entrada que casou não descreve defeito nenhum");
    }

    [Fact(DisplayName = "A recusa do desserializador nomeia os campos e NÃO o tipo que os declara")]
    public void RecusaDoDesserializador_NomeiaCamposSemOTipo()
    {
        ActionContext contexto = Contexto();
        contexto.ModelState.AddModelError(
            "$[0]",
            "JSON deserialization for type 'Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.EtapaProcessoInput' "
            + "was missing required properties including: 'produtos'; 'bancas'; 'recursos'.");

        ProblemDetails problema = Executar(contexto);

        problema.Extensions["code"].Should().Be("uniplus.requisicao.campo_obrigatorio_ausente");
        problema.Detail.Should().Contain("produtos").And.Contain("bancas").And.Contain("recursos");

        // O nome do tipo vem ANTES de `including:` na mensagem, e entre as mesmas aspas simples
        // dos nomes de campo. É por isso que o recorte começa depois daquela palavra.
        problema.Detail.Should().NotContain("Unifesspa.UniPlus",
            "o envelope de erro não expõe nome de tipo, namespace nem caminho de arquivo (ADR-0023)");
        problema.Detail.Should().NotContain("EtapaProcessoInput");
    }

    /// <summary>
    /// Corpo ausente não é campo ausente: não adianta mandar o cliente declarar um campo quando
    /// não chegou documento nenhum onde procurá-lo.
    /// </summary>
    /// <remarks>
    /// O binder registra essa falha sob o nome do parâmetro que vem do corpo — ou sob a chave
    /// vazia, quando não há o que nomear —, o que faz a recusa parecer um campo faltando se
    /// nada distinguir os dois casos.
    /// </remarks>
    [Fact(DisplayName = "Corpo ausente tem code próprio, e não é anunciado como campo faltando")]
    public void CorpoAusente_TemCodeProprio()
    {
        ActionContext contexto = Contexto();
        contexto.ModelState.AddModelError(string.Empty, "A non-empty request body is required.");

        ProblemDetails problema = Executar(contexto);

        problema.Extensions["code"].Should().Be("uniplus.requisicao.corpo_ausente");
        problema.Detail.Should().NotContain("campo obrigatório");
    }

    /// <summary>
    /// Quando o corpo VEIO e não desserializa, o MVC reprova também o parâmetro que o receberia
    /// — então a pergunta "faltou corpo?" tem de vir depois de "o corpo é ilegível?".
    /// </summary>
    [Fact(DisplayName = "Corpo presente e ilegível não é confundido com corpo ausente")]
    public void CorpoIlegivel_NaoEhCorpoAusente()
    {
        ActionContext contexto = Contexto();
        // As duas entradas que o MVC produz nesse caso: a do parâmetro e a da posição no
        // documento. É a segunda que prova que documento houve.
        contexto.ModelState.AddModelError(string.Empty, "The etapas field is required.");
        contexto.ModelState.AddModelError("$", "'x' is an invalid start of a value.");

        ProblemDetails problema = Executar(contexto);

        problema.Extensions["code"].Should().Be("uniplus.requisicao.malformada");
    }

    /// <summary>
    /// Propriedade de dentro do corpo reprovada DEPOIS da desserialização não é ausência, ainda
    /// que pareça: o input formatter não preenche valor tentado nem valor cru para o modelo,
    /// nem quando a chave veio com <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Chamar isso de campo não declarado diria ao cliente que ele não mandou o que mandou, e
    /// descartaria a mensagem de quem de fato reprovou — que é justamente o que este factory
    /// deixa passar de propósito.
    /// </remarks>
    [Fact(DisplayName = "Propriedade do corpo reprovada após a desserialização não vira campo ausente")]
    public void PropriedadeDoCorpoReprovadaAposDesserializar_Delega()
    {
        ActionContext contexto = Contexto();
        // É o que o MVC produz para `"orgao": null` num campo declarado não-anulável: entrada
        // inválida, sem valor tentado e sem valor cru, e nenhuma posição de documento apontada.
        contexto.ModelState.AddModelError("Orgao", "The Orgao field is required.");

        InvalidRequestProblemFactory.TryBuild(contexto).Should().BeNull(
            "a chave não é de parâmetro do binder, então o sinal de ausência não vale ali");
    }

    /// <summary>
    /// O documento JSON <c>null</c> É um corpo: o parâmetro fica nulo e a exigência implícita
    /// reprova igual, mas o cliente enviou algo.
    /// </summary>
    /// <remarks>
    /// Sem olhar o tamanho do conteúdo, as duas situações são indistinguíveis pelo
    /// <c>ModelState</c>, e mandar acrescentar corpo a quem já mandou um é orientação que não
    /// leva a lugar nenhum. Aqui a recusa correta é de conteúdo, não de ausência.
    /// </remarks>
    [Fact(DisplayName = "Corpo JSON null não é corpo ausente — o cliente enviou documento")]
    public void CorpoJsonNull_NaoEhCorpoAusente()
    {
        ActionContext contexto = Contexto(ParametroDeCorpo("comando"));
        contexto.HttpContext.Request.ContentLength = 4;
        contexto.ModelState.AddModelError("comando", "The comando field is required.");

        InvalidRequestProblemFactory.TryBuild(contexto).Should().BeNull(
            "documento houve, e quem reprovou foi a exigência implícita depois de desserializar "
            + "— mensagem que pertence à cadeia de validação, não a este factory");
    }

    /// <summary>
    /// Comprimento desconhecido não é comprimento zero: um documento em <c>chunked</c>, ou um
    /// corpo HTTP/2 sem comprimento declarado, chega sem <c>Content-Length</c> e existe.
    /// </summary>
    /// <remarks>
    /// Este é o caso ambíguo, e a regra dele é não afirmar: dizer "não mandou corpo" a quem
    /// mandou um em chunked é o mesmo erro do corpo <c>null</c>, por um caminho diferente.
    /// </remarks>
    [Fact(DisplayName = "Corpo de tamanho não declarado não é tratado como ausente")]
    public void CorpoDeTamanhoDesconhecido_NaoEhAusente()
    {
        ActionContext contexto = Contexto(ParametroDeCorpo("comando"));
        contexto.HttpContext.Request.ContentLength = null;
        contexto.HttpContext.Request.Headers.TransferEncoding = "chunked";
        contexto.ModelState.AddModelError("comando", "The comando field is required.");

        InvalidRequestProblemFactory.TryBuild(contexto).Should().BeNull(
            "sem comprimento declarado não dá para provar que não veio documento, e afirmar "
            + "ausência que não se prova é o erro que este factory existe para não cometer");
    }

    [Fact(DisplayName = "Requisição sem comprimento e sem codificação de transferência não tem corpo — e aí a ausência é provável")]
    public void SemComprimentoESemTransferEncoding_EhAusencia()
    {
        ActionContext contexto = Contexto(ParametroDeCorpo("comando"));
        contexto.HttpContext.Request.ContentLength = null;
        contexto.ModelState.AddModelError("comando", "The comando field is required.");

        ProblemDetails problema = Executar(contexto);

        // É a regra do próprio HTTP: sem comprimento e sem codificação de transferência, não há
        // corpo. Aqui a ausência não é suposição, é o que o protocolo diz.
        problema.Extensions["code"].Should().Be("uniplus.requisicao.corpo_ausente");
    }

    private static ProblemDetails Executar(ActionContext contexto)
    {
        IActionResult resultado = InvalidRequestProblemFactory.TryBuild(contexto)
            .Should().NotBeNull("este caso é de leitura da requisição e o factory tem de responder por ele").And.Subject as IActionResult
            ?? throw new InvalidOperationException();
        return (ProblemDetails)((ObjectResult)resultado).Value!;
    }

    private static ParameterDescriptor ParametroDeQuery(string nome) => new()
    {
        Name = nome,
        BindingInfo = new BindingInfo { BindingSource = BindingSource.Query },
    };

    private static ParameterDescriptor ParametroDeCorpo(string nome) => new()
    {
        Name = nome,
        BindingInfo = new BindingInfo { BindingSource = BindingSource.Body },
    };

    private static ActionContext Contexto(params ParameterDescriptor[] parametros)
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton<IProblemTypeUriFactory>(
            new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions { BaseUri = BaseDoCatalogo })));
        servicos.AddSingleton<IDomainErrorRegistration, RegistroDeTeste>();
        servicos.AddSingleton<IDomainErrorMapper>(sp => new DomainErrorMappingRegistry(
            sp.GetServices<IDomainErrorRegistration>(),
            sp.GetRequiredService<IProblemTypeUriFactory>()));

        DefaultHttpContext http = new() { RequestServices = servicos.BuildServiceProvider() };
        return new ActionContext(http, new RouteData(), new ActionDescriptor { Parameters = parametros });
    }

    /// <summary>Só os dois códigos que este factory emite — o resto do catálogo não importa aqui.</summary>
    private sealed class RegistroDeTeste : IDomainErrorRegistration
    {
        public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
        [
            new(InvalidRequestErrorCodes.MissingRequiredField,
                new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.campo_obrigatorio_ausente", "Campo obrigatório ausente")),
            new(InvalidRequestErrorCodes.Malformed,
                new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.malformada", "Requisição malformada")),
            new(InvalidRequestErrorCodes.MissingBody,
                new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.corpo_ausente", "Requisição sem corpo")),
        ];
    }
}
