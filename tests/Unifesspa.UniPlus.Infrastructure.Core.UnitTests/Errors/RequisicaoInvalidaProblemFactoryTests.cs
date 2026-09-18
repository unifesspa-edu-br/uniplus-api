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
public sealed class RequisicaoInvalidaProblemFactoryTests
{
    private const string BaseDoCatalogo = "https://exemplo.invalid/erros/";

    [Fact(DisplayName = "Campo que a carga não traz é ausência: o code diz isso e o detail o nomeia")]
    public void CampoNaoFornecido_EhAusencia()
    {
        ActionContext contexto = Contexto();
        // Nada fornecido: nem valor tentado, nem valor cru — não houve o que converter.
        contexto.ModelState.AddModelError("etapas", "The etapas field is required.");

        ProblemDetails problema = Executar(contexto);

        problema.Status.Should().Be(StatusCodes.Status400BadRequest);
        problema.Extensions["code"].Should().Be("uniplus.requisicao.campo_obrigatorio_ausente");
        problema.Detail.Should().Contain("etapas");
    }

    [Fact(DisplayName = "Campo declarado que não converte é carga malformada, não campo ausente")]
    public void ValorPresenteQueNaoConverte_EhMalformada()
    {
        ActionContext contexto = Contexto();
        // O cliente MANDOU o campo; ele só não vira o tipo declarado. Mandá-lo acrescentar o
        // campo seria mandá-lo repetir o que fez.
        contexto.ModelState.SetModelValue("vigentes", rawValue: "abc", attemptedValue: "abc");
        contexto.ModelState.AddModelError("vigentes", "The value 'abc' is not valid.");

        ProblemDetails problema = Executar(contexto);

        problema.Status.Should().Be(StatusCodes.Status400BadRequest);
        problema.Extensions["code"].Should().Be("uniplus.requisicao.malformada");
        problema.Detail.Should().NotContain("vigentes",
            "afirmar que o campo não foi declarado é falso quando ele veio — e nomeá-lo aqui "
            + "daria ao cliente uma instrução que ele já cumpriu");
    }

    [Fact(DisplayName = "Campo que casou não entra na recusa, mesmo quando outro falhou")]
    public void CampoValido_NaoEntraNaLista()
    {
        ActionContext contexto = Contexto();
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

    private static ProblemDetails Executar(ActionContext contexto)
    {
        IActionResult resultado = RequisicaoInvalidaProblemFactory.Build(contexto);
        return (ProblemDetails)((ObjectResult)resultado).Value!;
    }

    private static ActionContext Contexto()
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton<IProblemTypeUriFactory>(
            new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions { BaseUri = BaseDoCatalogo })));
        servicos.AddSingleton<IDomainErrorRegistration, RegistroDeTeste>();
        servicos.AddSingleton<IDomainErrorMapper>(sp => new DomainErrorMappingRegistry(
            sp.GetServices<IDomainErrorRegistration>(),
            sp.GetRequiredService<IProblemTypeUriFactory>()));

        DefaultHttpContext http = new() { RequestServices = servicos.BuildServiceProvider() };
        return new ActionContext(http, new RouteData(), new ActionDescriptor());
    }

    /// <summary>Só os dois códigos que este factory emite — o resto do catálogo não importa aqui.</summary>
    private sealed class RegistroDeTeste : IDomainErrorRegistration
    {
        public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
        [
            new(RequisicaoInvalidaErrorCodes.CampoObrigatorioAusente,
                new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.campo_obrigatorio_ausente", "Campo obrigatório ausente")),
            new(RequisicaoInvalidaErrorCodes.Malformada,
                new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.malformada", "Requisição malformada")),
        ];
    }
}
