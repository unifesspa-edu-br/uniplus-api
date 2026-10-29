namespace Unifesspa.UniPlus.Publicacoes.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Leitura da definição de um <see cref="PermissaoPublicacoes"/>. Uma extensão por enum, e
/// não uma genérica sobre <see cref="Enum"/>: enum não implementa interface, e a
/// genérica alcançaria todos os enums do sistema (ADR-0151).
/// </summary>
public static class PermissaoPublicacoesExtensions
{
    extension(PermissaoPublicacoes permissao)
    {
        /// <summary>Código <c>{módulo}:{recurso}:{ação}</c>.</summary>
        public string Codigo => permissao.Definicao().Codigo;

        /// <summary>Nome legível.</summary>
        public string Nome => permissao.Definicao().Nome;

        /// <summary>Descrição do que a permissão autoriza.</summary>
        public string Descricao => permissao.Definicao().Descricao;

        /// <summary>Módulo dono.</summary>
        public string Modulo => permissao.Definicao().Modulo;

        /// <summary>Recurso sobre o qual a permissão age.</summary>
        public string Recurso => permissao.Definicao().Recurso;

        /// <summary>Ação que a permissão autoriza.</summary>
        public string Acao => permissao.Definicao().Acao;

        /// <summary>Requisito que o ponto de decisão avalia.</summary>
        public PermissionRequirement Requisito => permissao.Definicao().Requisito;
    }

    private static DefinicaoDePermissao Definicao(this PermissaoPublicacoes permissao)
        => CatalogoDePermissoes<PermissaoPublicacoes>.Obter(permissao);
}
