namespace Unifesspa.UniPlus.SharedKernel.Domain.ValueObjects;

using Unifesspa.UniPlus.SharedKernel.Results;

public sealed record NomeSocial
{
    public string NomeCivil { get; }
    public string? Nome { get; }
    public bool UsaNomeSocial => !string.IsNullOrWhiteSpace(Nome);
    public string NomeExibicao => UsaNomeSocial ? Nome! : NomeCivil;

    private NomeSocial(string nomeCivil, string? nomeSocial)
    {
        NomeCivil = nomeCivil;
        Nome = nomeSocial;
    }

    public static Result<NomeSocial> Criar(string? nomeCivil, string? nomeSocial = null)
    {
        if (string.IsNullOrWhiteSpace(nomeCivil))
            return Result<NomeSocial>.Failure(new DomainError("NomeSocial.NomeCivilVazio", "Nome civil é obrigatório."));

        return Result<NomeSocial>.Success(new NomeSocial(nomeCivil.Trim(), nomeSocial?.Trim()));
    }

    public override string ToString() => NomeExibicao;
}
