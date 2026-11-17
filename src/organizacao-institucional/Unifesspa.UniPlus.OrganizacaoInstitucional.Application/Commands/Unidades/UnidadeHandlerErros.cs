namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>As recusas que os handlers da Unidade devolvem por regra de negócio.</summary>
internal static class UnidadeHandlerErros
{
    internal static DomainError NaoEncontrada() =>
        new(UnidadeErrorCodes.NaoEncontrada, "Unidade não encontrada.");

    internal static DomainError SlugJaExiste(Slug slug) =>
        new(UnidadeErrorCodes.SlugJaExiste, $"Já existe uma Unidade viva com o slug '{slug}'.");

    internal static DomainError SiglaJaExiste(string? sigla) =>
        new(UnidadeErrorCodes.SiglaJaExiste, $"Já existe uma Unidade viva com a sigla '{sigla}'.");

    internal static DomainError CodigoJaExiste(string? codigo) =>
        new(UnidadeErrorCodes.CodigoJaExiste, $"Já existe uma Unidade viva com o código '{codigo}'.");

    internal static DomainError SuperiorNaoEncontrado() =>
        new(UnidadeErrorCodes.SuperiorNaoEncontrado, "A Unidade superior informada não foi encontrada.");

    internal static DomainError SuperiorIgualAUnidade() =>
        new(UnidadeErrorCodes.SuperiorFormaCiclo, "Uma Unidade não pode ser superior de si mesma.");

    internal static DomainError SuperiorDescendenteDaUnidade() =>
        new(
            UnidadeErrorCodes.SuperiorFormaCiclo,
            "A Unidade superior informada é descendente da Unidade sendo editada — formaria ciclo na hierarquia.");

    internal static DomainError RemocaoBloqueadaPorSubordinadas() =>
        new(
            UnidadeErrorCodes.RemocaoBloqueadaPorSubordinadas,
            "Não é possível remover uma Unidade que possui unidades subordinadas ativas.");

    internal static DomainError RemocaoBloqueadaPorInstituicao() =>
        new(
            UnidadeErrorCodes.RemocaoBloqueadaPorInstituicao,
            "Não é possível remover uma Unidade que é raiz de uma Instituição.");
}
