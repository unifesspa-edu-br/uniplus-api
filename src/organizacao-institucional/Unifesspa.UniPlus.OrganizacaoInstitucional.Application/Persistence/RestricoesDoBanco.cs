namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Persistence;

/// <summary>
/// Nomes dos índices e das restrições do banco que um handler da Organização Institucional traduz
/// em recusa nomeada ao ver a violação. O mapeamento do EF nomeia o objeto com a mesma constante,
/// de modo que renomear a tabela muda o nome em um lugar só.
/// </summary>
public static class RestricoesDoBanco
{
    public const string InstituicaoSingletonVivo = "ix_instituicoes_singleton_vivo";
}
