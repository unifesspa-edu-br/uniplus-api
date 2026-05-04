namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Results;

public sealed record NomeSocial
{
    public string NomeCivil { get; }
    public string? Nome { get; }
    public bool UsaNomeSocial => !string.IsNullOrWhiteSpace(Nome);
    public string NomeExibicao => UsaNomeSocial ? Nome! : NomeCivil;

    // O parâmetro do construtor de cópia é renomeado para `nome` (em vez de
    // `nomeSocial`) para evitar ambiguidade com o nome do método de fábrica
    // no binding do EF Core: o materializador procura propriedades com o
    // mesmo nome (case-insensitive) dos parâmetros e dois `nomeSocial` no
    // contexto disparam "no suitable constructor" em EnsureCreatedAsync.
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
