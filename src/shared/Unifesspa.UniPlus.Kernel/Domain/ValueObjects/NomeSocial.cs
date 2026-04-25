namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

public sealed record NomeSocial
{
    public string NomeCivil { get; }
    public string? Nome { get; }
    public bool UsaNomeSocial => !string.IsNullOrWhiteSpace(Nome);
    public string NomeExibicao => UsaNomeSocial ? Nome! : NomeCivil;

    // Parâmetro nomeado para casar com a propriedade Nome — EF Core
    // mapeia constructor params para properties por convenção case-insensitive.
    private NomeSocial(string nomeCivil, string? nome)
    {
        NomeCivil = nomeCivil;
        Nome = nome;
    }

    public static Result<NomeSocial> Criar(string? nomeCivil, string? nomeSocial = null)
    {
        if (string.IsNullOrWhiteSpace(nomeCivil))
            return Result<NomeSocial>.Failure(new DomainError("NomeSocial.NomeCivilVazio", "Nome civil é obrigatório."));

        return Result<NomeSocial>.Success(new NomeSocial(nomeCivil.Trim(), nomeSocial?.Trim()));
    }

    public override string ToString() => NomeExibicao;
}
