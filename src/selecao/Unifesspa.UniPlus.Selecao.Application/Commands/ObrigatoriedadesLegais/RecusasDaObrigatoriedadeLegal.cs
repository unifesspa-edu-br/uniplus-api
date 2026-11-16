namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As recusas de negócio, não de campo, de criar e atualizar uma obrigatoriedade legal.</summary>
internal static class RecusasDaObrigatoriedadeLegal
{
    internal static DomainError TipoProcessoNaoEncontradoOuInativo(string codigo) => new(
        "ObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo",
        $"Tipo de processo seletivo '{codigo}' não encontrado ou não está ativo.");

    internal static DomainError NaoEncontrada(Guid id) => new(
        "ObrigatoriedadeLegal.NaoEncontrada",
        $"ObrigatoriedadeLegal {id} não encontrada.");

    internal static DomainError RegraCodigoDuplicada(string? regraCodigo) => new(
        "ObrigatoriedadeLegal.RegraCodigoDuplicada",
        $"Já existe outra regra ativa com RegraCodigo '{regraCodigo?.Trim()}'.");

    internal static DomainError ConteudoCanonicoDuplicado() => new(
        "ObrigatoriedadeLegal.HashColisao",
        "Já existe outra regra ativa com o mesmo conteúdo canônico.");
}
