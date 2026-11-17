namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Construtor de dados de teste da Unidade: o Centro de Processos Seletivos (CEPS), do tipo
/// Centro, vigente desde 1º/1/2026, sem superior e sem cidade, salvo o que o cenário trocar.
/// </summary>
internal static class UnidadeDeTeste
{
    public static readonly DateOnly InicioDaVigencia = new(2026, 1, 1);
    public static readonly Slug SlugDoCeps = Slug.From("ceps").Value!;

    public static IdentificadoresDaUnidade IdentificadoresDoCeps(string? alias = null) =>
        new(SlugDoCeps.Valor, "CEPS", "0001", alias);

    public static Result<Unidade> Criar(
        IdentificadoresDaUnidade? identificadores = null,
        CidadeDaUnidade cidade = default) =>
        Unidade.Criar(
            "Centro de Processos Seletivos",
            identificadores ?? IdentificadoresDoCeps(),
            cidade,
            new VigenciaDaUnidade(InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

    /// <summary>Atualiza o CEPS reapresentando os mesmos identificadores, um mês depois do início.</summary>
    public static Result AtualizarSemMudarIdentificadores(
        Unidade unidade, string nome, CidadeDaUnidade cidade, bool unidadeAcademica) =>
        unidade.Atualizar(
            nome,
            new MudancaDeIdentificadores(IdentificadoresDoCeps(), InicioDaVigencia.AddMonths(1), null),
            cidade,
            null,
            null,
            TipoUnidade.Centro,
            unidadeAcademica);
}
