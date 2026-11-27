namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.CorpoDaRequisicao;

using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>O middleware do limite do corpo montado com o limite geral de teste, e o contexto HTTP em memória que o exercita.</summary>
internal sealed class MiddlewareDoLimiteDeTeste
{
    internal const long LimiteGeral = 100;

    private readonly bool[] _proximoChamado = [false];

    internal RegistroEmMemoria<LimiteDoCorpoDaRequisicaoMiddleware> Registro { get; } = new();

    internal bool ProximoChamado => _proximoChamado[0];

    internal LimiteDoCorpoDaRequisicaoMiddleware Middleware { get; }

    internal MiddlewareDoLimiteDeTeste()
    {
        ProblemTypeUriFactory fabrica = new(Options.Create(new ProblemTypeOptions { BaseUri = "https://catalogo.exemplo/erros/" }));
        Middleware = new LimiteDoCorpoDaRequisicaoMiddleware(
            _ =>
            {
                _proximoChamado[0] = true;
                return Task.CompletedTask;
            },
            Options.Create(new LimiteDoCorpoDaRequisicaoOptions { TamanhoMaximoEmBytes = LimiteGeral }),
            fabrica,
            Registro);
    }

    internal static DefaultHttpContext Contexto(long? tamanhoDeclarado)
    {
        DefaultHttpContext contexto = new();
        contexto.Response.Body = new MemoryStream();
        contexto.Request.Path = "/teste/rota";
        contexto.Request.ContentLength = tamanhoDeclarado;
        return contexto;
    }

    internal static void DeclararLimiteDaOperacao(HttpContext contexto, long limite) =>
        contexto.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new RequestSizeLimitAttribute(limite)), "operacao"));

    internal static async Task<JsonDocument> LerRespostaAsync(HttpContext contexto)
    {
        contexto.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(contexto.Response.Body);
    }

    internal sealed class LimiteDoServidor : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; init; }

        public long? MaxRequestBodySize { get; set; }
    }

    internal sealed class RegistroEmMemoria<T> : ILogger<T>
    {
        internal List<(LogLevel Nivel, string Mensagem)> Entradas { get; } = [];

        internal List<Exception> Excecoes { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Entradas.Add((logLevel, formatter(state, exception)));
            if (exception is not null)
            {
                Excecoes.Add(exception);
            }
        }
    }
}
