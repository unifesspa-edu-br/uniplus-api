namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class Candidato : EntityBase
{
    public Cpf Cpf { get; private set; } = null!;
    public NomeSocial NomeSocial { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public DateOnly DataNascimento { get; private set; }
    public string? Telefone { get; private set; }

    private Candidato() { }

    public static Candidato Criar(Cpf cpf, NomeSocial nomeSocial, Email email, DateOnly dataNascimento, string? telefone = null)
    {
        return new Candidato
        {
            Cpf = cpf,
            NomeSocial = nomeSocial,
            Email = email,
            DataNascimento = dataNascimento,
            Telefone = telefone
        };
    }

    public string NomeExibicao => NomeSocial.NomeExibicao;
}
