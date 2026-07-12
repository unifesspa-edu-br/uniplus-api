namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Dados documentais do ato usados pelos testes. Em produção o operador os declara no
/// request (ADR-0108) — aqui são fixos para que o teste fale de uma coisa só.
/// </summary>
internal static class DadosDoAtoDeTeste
{
    public const string TipoAbertura = "EDITAL_ABERTURA";
    public const string TipoRetificacao = "EDITAL_RETIFICACAO";

    public static DadosDoAto Padrao => new(
        "CEPS", "EDITAL", 2026, DateOnly.FromDateTime(DateTime.UtcNow), "Diretor do CEPS", TipoAbertura);

    public static DadosDoAto Retificacao => new(
        "CEPS", "EDITAL", 2026, DateOnly.FromDateTime(DateTime.UtcNow), "Diretor do CEPS", TipoRetificacao);
}
