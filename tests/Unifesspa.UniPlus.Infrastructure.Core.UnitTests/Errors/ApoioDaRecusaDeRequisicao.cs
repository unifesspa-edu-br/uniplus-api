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
/// O contexto de ação e o registro de erros mínimos para exercitar a fábrica da recusa de
/// requisição, com a descrição de tipos que o MVC monta (inclusive a obrigatoriedade implícita).
/// </summary>
internal static class ApoioDaRecusaDeRequisicao
{
    private const string BaseDoCatalogo = "https://exemplo.invalid/erros/";

    public static ProblemDetails Executar(ActionContext contexto)
    {
        IActionResult resultado = InvalidRequestProblemFactory.TryBuild(contexto)
            .Should().NotBeNull("este caso é de leitura da requisição e o factory tem de responder por ele").And.Subject as IActionResult
            ?? throw new InvalidOperationException();
        return (ProblemDetails)((ObjectResult)resultado).Value!;
    }

    public static ParameterDescriptor ParametroDeQuery(string nome) => new()
    {
        Name = nome,
        ParameterType = typeof(string),
        BindingInfo = new BindingInfo { BindingSource = BindingSource.Query },
    };

    public static ParameterDescriptor ParametroDeCorpo(string nome, Type? tipo = null) => new()
    {
        Name = nome,
        ParameterType = tipo ?? typeof(object),
        BindingInfo = new BindingInfo { BindingSource = BindingSource.Body },
    };

    public static ActionContext Contexto(params ParameterDescriptor[] parametros)
    {
        ServiceCollection servicos = new();
        servicos.AddMvcCore().AddDataAnnotations();
        servicos.AddSingleton<IProblemTypeUriFactory>(
            new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions { BaseUri = BaseDoCatalogo })));
        servicos.AddSingleton<IDomainErrorRegistration, RegistroDeTeste>();
        servicos.AddSingleton<IDomainErrorMapper>(sp => new DomainErrorMappingRegistry(
            sp.GetServices<IDomainErrorRegistration>(),
            sp.GetRequiredService<IProblemTypeUriFactory>()));

        DefaultHttpContext http = new() { RequestServices = servicos.BuildServiceProvider() };
        return new ActionContext(http, new RouteData(), new ActionDescriptor { Parameters = parametros });
    }

    /// <summary>Só os códigos que este factory emite — o resto do catálogo não importa aqui.</summary>
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
