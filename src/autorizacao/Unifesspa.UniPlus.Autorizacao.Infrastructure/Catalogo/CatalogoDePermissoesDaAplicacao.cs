namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Porta de consulta do catálogo para o domínio do perfil, apoiada no catálogo da aplicação
/// que reúne as permissões de todos os módulos.
/// </summary>
public sealed class CatalogoDePermissoesDaAplicacao : ICatalogoDePermissoesConsultavel
{
    private readonly CatalogoDaAplicacao _catalogo;

    public CatalogoDePermissoesDaAplicacao(CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        _catalogo = catalogo;
    }

    public bool Existe(string codigo) => _catalogo.TentarObter(codigo, out _);

    public bool ReservadaAGovernanca(string codigo) =>
        _catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao) && definicao!.ReservadaAGovernanca;
}
