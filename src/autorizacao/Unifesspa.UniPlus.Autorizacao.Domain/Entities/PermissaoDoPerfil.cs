namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Uma permissão do catálogo reunida por um perfil. Guarda só o código, que é o
/// identificador estável da permissão: nome e sensibilidade pertencem ao catálogo e são
/// lidos dele a cada consulta.
/// </summary>
public sealed class PermissaoDoPerfil
{
    public CodigoDePermissao Codigo { get; private set; }

    internal PermissaoDoPerfil(CodigoDePermissao codigo)
    {
        Codigo = codigo;
    }
}
