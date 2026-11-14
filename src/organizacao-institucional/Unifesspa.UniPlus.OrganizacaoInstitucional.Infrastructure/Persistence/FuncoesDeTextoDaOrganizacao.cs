namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

// Stub mapeado como DB function no contexto via HasDbFunction. O EF Core requer que o tipo
// declarante seja public para que o tradutor LINQ-to-SQL reconheça as chamadas na árvore de
// expressão. Nunca é chamado em C#: o stub sempre lança para detectar uso acidental.
public static class FuncoesDeTextoDaOrganizacao
{
    // Mapeia para immutable_unaccent(text), criada pelo script Scripts/Pre/001_extensoes_e_funcao_de_busca.sql.
    // Remove diacríticos de forma IMMUTABLE e viabiliza índices GIN de expressão sobre campos de texto.
    public static string ImmutableUnaccent(string? text) =>
        throw new InvalidOperationException(
            "FuncoesDeTextoDaOrganizacao.ImmutableUnaccent é um stub de EF Core e não pode ser chamado diretamente.");
}
