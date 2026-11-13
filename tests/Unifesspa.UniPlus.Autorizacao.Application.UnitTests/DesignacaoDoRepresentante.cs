namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;

/// <summary>
/// A designação de Representante do controlador que os testes da revogação usam: do perfil de sistema dele, na
/// instituição, sem prazo, concedida por Diogo um mês antes do instante informado.
/// </summary>
internal static class DesignacaoDoRepresentante
{
    private static readonly Identidade Diogo = new("https://idp.test/realms/unifesspa", "diogo.souza");

    public static Concessao De(Identidade pessoa, string? email, DateTimeOffset instante)
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca(
            "Representante do controlador", "Decide em nome da instituição.", ["autorizacao:revisoes:conduzir"], new CatalogoLivre(),
            TipoDeGovernanca.RepresentanteDoControlador);
        typeof(Perfil).GetProperty(nameof(Perfil.Id))!.SetValue(perfil, PerfisDeSistema.RepresentanteDoControladorId);

        return ConcessaoBuilder.DoPerfil(perfil, new CatalogoLivre())
            .Para(pessoa, email)
            .ConcedidaPor(Diogo)
            .Em(instante.AddDays(-30))
            .Construir();
    }
}
