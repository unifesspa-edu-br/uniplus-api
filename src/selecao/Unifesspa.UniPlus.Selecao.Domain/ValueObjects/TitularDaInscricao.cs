namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed record TitularDaInscricao
{
    private TitularDaInscricao(Cpf cpf, string contaId, string emissorToken)
    {
        Cpf = cpf;
        ContaId = contaId;
        EmissorToken = emissorToken;
    }

    public Cpf Cpf { get; }
    public string ContaId { get; }
    public string EmissorToken { get; }

    public static TitularDaInscricao Reidratar(Cpf cpf, string contaId, string emissorToken)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        return new(cpf, contaId, emissorToken);
    }

    public static Result<TitularDaInscricao> Identificar(Cpf cpf, string contaId, string emissorToken)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        if (string.IsNullOrWhiteSpace(contaId))
        {
            return Result<TitularDaInscricao>.Failure(new DomainError(InscricaoErrorCodes.ContaIdObrigatorio, "O token precisa trazer o claim sub para iniciar a inscrição."));
        }

        return string.IsNullOrWhiteSpace(emissorToken)
            ? Result<TitularDaInscricao>.Failure(new DomainError(InscricaoErrorCodes.EmissorObrigatorio, "O token precisa trazer o emissor para iniciar a inscrição."))
            : Result<TitularDaInscricao>.Success(new(cpf, contaId, emissorToken));
    }

    public DomainError? ConferirDono(string? contaId)
    {
        if (string.IsNullOrWhiteSpace(contaId))
        {
            return new DomainError(InscricaoErrorCodes.ContaIdObrigatorio, "O token precisa trazer o claim sub para acessar a inscrição.");
        }

        return string.Equals(ContaId, contaId, StringComparison.Ordinal)
            ? null
            : new DomainError(InscricaoErrorCodes.DonoDivergente, "Esta inscrição pertence a outro candidato.");
    }

    public IEnumerable<FieldError> RecusasDoCpf(IReadOnlyDictionary<string, JsonElement> respostas)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        if (!respostas.TryGetValue(ConjuntoBasicoDaInscricao.Cpf, out JsonElement valor))
        {
            return [];
        }

        DomainError? recusa = RecusaDoCpf(valor);
        return recusa is null ? [] : [new FieldError(ConjuntoBasicoDaInscricao.Cpf, recusa)];
    }

    private DomainError? RecusaDoCpf(JsonElement valor)
    {
        if (valor.ValueKind == JsonValueKind.Null)
        {
            return new DomainError(InscricaoErrorCodes.CpfNaoPodeSerRemovido,
                "O CPF faz parte da inscrição e não pode ser apagado. Informe o CPF da sua conta.");
        }

        bool doDono = valor.ValueKind == JsonValueKind.String
            && Cpf.Criar(valor.GetString()) is { IsSuccess: true } respondido
            && respondido.Value!.Valor == Cpf.Valor;
        return doDono ? null : new DomainError(InscricaoErrorCodes.CpfDiferenteDoDono,
            "O CPF informado tem de ser o do titular da conta.");
    }
}
