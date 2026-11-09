namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// Tipos mínimos que servem de contraprova à regra da composição do perfil: os três primeiros leem a composição por
/// fora dos donos e têm de ser rejeitados; o último só lê o nome do perfil e tem de ser aceito.
/// </summary>
internal static class FixturesDeComposicaoDoPerfil
{
    internal sealed class DependeDaPermissaoDoPerfil
    {
        public IReadOnlyCollection<PermissaoDoPerfil>? Permissoes { get; set; }
    }

    internal sealed class DependeDaPermissaoDaVersao
    {
        public IReadOnlyCollection<PermissaoDaVersao>? Permissoes { get; set; }
    }

    internal sealed class LeAVersaoVigenteDoPerfil(Perfil perfil)
    {
        public object? Vigente() => perfil.VersaoVigente;
    }

    internal sealed class LeAComposicaoEmVigorDoPerfil(Perfil perfil)
    {
        public IReadOnlyList<string> Codigos() => perfil.ComposicaoEmVigor;
    }

    internal sealed class LeSoONomeDoPerfil(Perfil perfil)
    {
        public string Nome() => perfil.Nome;
    }
}
