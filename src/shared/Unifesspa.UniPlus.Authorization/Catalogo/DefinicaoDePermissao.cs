namespace Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>Definição de uma permissão lida do atributo de um membro de enum.</summary>
/// <param name="Codigo">Código <c>{módulo}:{resource}:{ação}</c>.</param>
/// <param name="Nome">Nome legível.</param>
/// <param name="Descricao">Descrição do que a permissão autoriza.</param>
/// <param name="Modulo">Módulo dono da permissão.</param>
/// <param name="Resource">Resource sobre o qual a permissão age.</param>
/// <param name="Acao">Ação que a permissão autoriza.</param>
/// <param name="Requisito">Requisito que o ponto de decisão avalia.</param>
/// <param name="ReservadaAGovernanca">Só perfis de governança podem reunir a permissão.</param>
/// <param name="Natureza">A natureza do item que a permissão libera na projeção; nenhuma quando é uma ação.</param>
public sealed record DefinicaoDePermissao(
    string Codigo,
    string Nome,
    string Descricao,
    string Modulo,
    string Resource,
    string Acao,
    PermissionRequirement Requisito,
    bool ReservadaAGovernanca = false,
    NaturezaDoItem Natureza = NaturezaDoItem.Nenhuma)
{
    /// <summary>
    /// A permissão é de unidade: o ponto de decisão exige a unidade proprietária do resource no contexto. As demais
    /// permissões são institucionais.
    /// </summary>
    public bool DeUnidade => Requisito.EscopoContextoObrigatorio.Contains(CamposDeContexto.UnidadeProprietariaId, StringComparer.Ordinal);
}
