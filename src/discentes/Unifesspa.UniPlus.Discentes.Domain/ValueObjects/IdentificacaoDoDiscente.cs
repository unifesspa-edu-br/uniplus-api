namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que identifica o discente no vínculo: o identificador do vínculo no SIGAA, a
/// matrícula, o CPF e o nome. É o que a réplica guarda sobre a pessoa, e por isso nenhum
/// campo aparece em texto de diagnóstico.
/// </summary>
public sealed record IdentificacaoDoDiscente
{
    public IdDoDiscenteNoSigaa IdDiscenteSigaa { get; }
    public MatriculaDoDiscente Matricula { get; }
    public Cpf Cpf { get; }
    public NomeDoDiscente Nome { get; }

    private IdentificacaoDoDiscente(
        IdDoDiscenteNoSigaa idDiscenteSigaa,
        MatriculaDoDiscente matricula,
        Cpf cpf,
        NomeDoDiscente nome)
    {
        IdDiscenteSigaa = idDiscenteSigaa;
        Matricula = matricula;
        Cpf = cpf;
        Nome = nome;
    }

    public static Result<IdentificacaoDoDiscente> Criar(long idDiscenteSigaa, string? matricula, Cpf cpf, string? nome)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        return IdDoDiscenteNoSigaa.Criar(idDiscenteSigaa).Encadear(id =>
            MatriculaDoDiscente.Criar(matricula).Encadear(numero =>
                NomeDoDiscente.Criar(nome).Compor(chamado => new IdentificacaoDoDiscente(id, numero, cpf, chamado))));
    }

    /// <summary>
    /// Sobrescreve o <c>ToString()</c> sintetizado do record — que, por padrão, enumeraria
    /// Nome, Matrícula e CPF em qualquer interpolação ou log deste objeto.
    /// </summary>
    public override string ToString() => $"[IdentificacaoDoDiscente IdDiscenteSigaa={IdDiscenteSigaa.Valor}]";
}
