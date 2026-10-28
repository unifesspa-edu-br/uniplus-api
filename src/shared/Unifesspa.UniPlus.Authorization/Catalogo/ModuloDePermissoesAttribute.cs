namespace Unifesspa.UniPlus.Authorization.Catalogo;

/// <summary>
/// Declara o módulo dono de um enum de permissões. O primeiro segmento do código
/// de cada membro do enum precisa ser este módulo (ADR-0151).
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class ModuloDePermissoesAttribute : Attribute
{
    /// <summary>Cria a declaração do módulo.</summary>
    /// <param name="modulo">Nome do módulo no formato dos códigos, em minúsculas (ex.: <c>selecao</c>).</param>
    public ModuloDePermissoesAttribute(string modulo)
    {
        Modulo = modulo;
    }

    /// <summary>Nome do módulo no formato dos códigos de permissão.</summary>
    public string Modulo { get; }
}
