namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary><see cref="IDominiosInstitucionais"/> sobre <c>Autorizacao:Instalacao:DominiosInstitucionais</c>.</summary>
public sealed class DominiosInstitucionais : IDominiosInstitucionais
{
    private const string DominioPadrao = "unifesspa.edu.br";

    private readonly string[] _dominios;

    public DominiosInstitucionais(IOptions<InstalacaoOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        // O binder acrescenta os itens da configuração ao valor inicial da lista em vez de substituí-lo; por isso o
        // padrão só vale quando a configuração não declara domínio nenhum.
        _dominios = opcoes.Value.DominiosInstitucionais.Length > 0 ? opcoes.Value.DominiosInstitucionais : [DominioPadrao];
    }

    public bool Contem(string dominio)
    {
        ArgumentNullException.ThrowIfNull(dominio);
        return _dominios.Contains(dominio, StringComparer.OrdinalIgnoreCase);
    }
}
