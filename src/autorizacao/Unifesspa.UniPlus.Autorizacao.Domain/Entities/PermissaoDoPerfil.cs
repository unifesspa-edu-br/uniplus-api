namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// Uma permissão do catálogo reunida por um perfil. Guarda só o código, que é o
/// identificador estável da permissão: nome e sensibilidade pertencem ao catálogo e são
/// lidos dele a cada consulta.
/// </summary>
public sealed class PermissaoDoPerfil
{
    public string Codigo { get; private set; }

    internal PermissaoDoPerfil(string codigo)
    {
        Codigo = codigo;
    }
}
