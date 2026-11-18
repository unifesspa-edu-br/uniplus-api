namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A unidade acadêmica do curso como o SIGAA a identifica: o identificador e o nome, que a
/// origem entrega juntos.
/// </summary>
public sealed record UnidadeNoSigaa(IdDaUnidadeNoSigaa Id, NomeDaUnidade Nome)
{
    public static Result<UnidadeNoSigaa> Criar(int id, string? nome) =>
        IdDaUnidadeNoSigaa.Criar(id).Encadear(idDaUnidade =>
            NomeDaUnidade.Criar(nome).Compor(nomeDaUnidade => new UnidadeNoSigaa(idDaUnidade, nomeDaUnidade)));
}
