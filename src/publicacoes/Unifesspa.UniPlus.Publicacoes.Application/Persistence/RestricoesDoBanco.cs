namespace Unifesspa.UniPlus.Publicacoes.Application.Persistence;

/// <summary>
/// Nomes dos índices e das restrições do banco que a gravação do registro de atos traduz em recusa
/// nomeada ao ver a violação. O mapeamento do EF nomeia o objeto com a mesma constante, de modo que
/// renomear a tabela muda o nome em um lugar só. A exclusion constraint da vigência do tipo nasce
/// de script SQL do módulo; o teste de persistência provoca a violação no banco real e a compara
/// com a constante.
/// </summary>
public static class RestricoesDoBanco
{
    public const string AtoNormativoRetificadoUnico = "ux_atos_normativos_retificado";

    public const string ExclusividadeDeAtosObjetoTipoUnico = "ux_exclusividades_de_atos_objeto_tipo";

    public const string TipoDeAtoPublicadoCodigoVigencia = "ex_tipos_de_ato_publicado_codigo_vigencia";
}
