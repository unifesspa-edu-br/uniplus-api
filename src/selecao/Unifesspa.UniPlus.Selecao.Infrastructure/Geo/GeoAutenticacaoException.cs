namespace Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

/// <summary>A autenticação client_credentials contra o Keycloak, para falar com o Geo, falhou.</summary>
public sealed class GeoAutenticacaoException : Exception
{
    public GeoAutenticacaoException(string message) : base(message) { }

    public GeoAutenticacaoException(string message, Exception innerException) : base(message, innerException) { }

    public GeoAutenticacaoException() { }
}
