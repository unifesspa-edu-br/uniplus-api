namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>Um registro da trilha de acesso reduzido ao que identifica a decisão: quem, o quê, sobre qual recurso e com que resultado.</summary>
internal sealed record TrilhaDeAcesso(string Ator, string Permissao, string ResourceTipo, Guid? ResourceId, ResultadoDoAcesso Resultado)
{
    /// <summary>Os registros do único desfecho gravado pelo verificador.</summary>
    public static IEnumerable<TrilhaDeAcesso> Do(VerificadorDeTeste verificador) =>
        verificador.Gravados.Should().ContainSingle().Which
            .Select(static r => new TrilhaDeAcesso(r.AtorSubject, r.Permissao, r.ResourceTipo, r.ResourceId, r.Resultado));
}
