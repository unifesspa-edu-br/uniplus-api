namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Persistence;

/// <summary>
/// Nomes dos índices e das restrições do banco que um handler da Organização Institucional traduz
/// em recusa nomeada ao ver a violação. O mapeamento do EF nomeia o objeto com a mesma constante,
/// de modo que renomear a tabela muda o nome em um lugar só.
/// </summary>
public static class RestricoesDoBanco
{
    public const string InstituicaoUnicaViva = "ix_instituicoes_instituicao_unica_vivo";

    public const string UnidadeSlugVivo = "ux_unidades_identificador_no_endereco_web_ativos";

    public const string UnidadeSiglaVivo = "ix_unidades_sigla_vivo";

    public const string UnidadeCodigoVivo = "ix_unidades_codigo_vivo";
}
