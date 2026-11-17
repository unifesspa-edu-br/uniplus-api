namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que identifica o discente no vínculo: o identificador do vínculo no SIGAA, a
/// matrícula, o CPF e o nome. É o que a réplica guarda sobre a pessoa, e por isso nenhum
/// campo aparece em texto de diagnóstico.
/// </summary>
public sealed record IdentificacaoDoDiscente
{
    public long IdDiscenteSigaa { get; }
    public string Matricula { get; }
    public Cpf Cpf { get; }
    public string Nome { get; }

    private IdentificacaoDoDiscente(long idDiscenteSigaa, string matricula, Cpf cpf, string nome)
    {
        IdDiscenteSigaa = idDiscenteSigaa;
        Matricula = matricula;
        Cpf = cpf;
        Nome = nome;
    }

    public static Result<IdentificacaoDoDiscente> Criar(long idDiscenteSigaa, string? matricula, Cpf cpf, string? nome)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        IdentificacaoDoDiscente identificacao = new(idDiscenteSigaa, matricula ?? string.Empty, cpf, nome ?? string.Empty);

        DomainError? erro = identificacao.ValidarId() ?? identificacao.ValidarMatricula() ?? identificacao.ValidarNome();

        return erro is null
            ? Result<IdentificacaoDoDiscente>.Success(identificacao)
            : Result<IdentificacaoDoDiscente>.Failure(erro);
    }

    /// <summary>
    /// Diz se o texto é composto apenas por dígitos, que é o formato de matrícula que a
    /// origem promete.
    /// </summary>
    public static bool ApenasDigitos(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);

        return valor.Length > 0 && !valor.AsSpan().ContainsAnyExceptInRange('0', '9');
    }

    /// <summary>
    /// Sobrescreve o <c>ToString()</c> sintetizado do record — que, por padrão, enumeraria
    /// Nome, Matrícula e CPF em texto claro em qualquer interpolação ou log deste objeto.
    /// </summary>
    public override string ToString() => $"[IdentificacaoDoDiscente IdDiscenteSigaa={IdDiscenteSigaa}]";

    private DomainError? ValidarId() =>
        IdDiscenteSigaa <= 0
            ? new DomainError("VinculoDiscente.IdSigaaInvalido", "Id SIGAA deve ser positivo.")
            : null;

    private DomainError? ValidarMatricula()
    {
        if (string.IsNullOrWhiteSpace(Matricula))
            return new DomainError("VinculoDiscente.MatriculaVazia", "Matrícula é obrigatória.");

        if (!ApenasDigitos(Matricula))
            return new DomainError(
                "VinculoDiscente.MatriculaNaoNumerica",
                "Matrícula precisa ser composta apenas por dígitos.");

        if (Matricula.Length > LimitesDaReplica.Matricula)
            return new DomainError(
                "VinculoDiscente.MatriculaLonga",
                $"Matrícula excede {LimitesDaReplica.Matricula} caracteres, o máximo que a réplica comporta.");

        return null;
    }

    private DomainError? ValidarNome()
    {
        if (string.IsNullOrWhiteSpace(Nome))
            return new DomainError("VinculoDiscente.NomeVazio", "Nome é obrigatório.");

        if (Nome.Length > LimitesDaReplica.Nome)
            return new DomainError(
                DiscentesErrorCodes.VinculoDiscente.NomeLongo,
                $"Nome excede {LimitesDaReplica.Nome} caracteres, o máximo que a réplica comporta.");

        return null;
    }
}
