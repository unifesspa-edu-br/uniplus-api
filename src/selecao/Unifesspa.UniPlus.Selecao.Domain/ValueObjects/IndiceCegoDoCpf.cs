namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O índice cego (HMAC) de um CPF: serve para achar a inscrição de um CPF sem decifrar nenhuma
/// e não permite recuperar o CPF. Opaco para quem o recebe — só a igualdade interessa.
/// </summary>
public readonly record struct IndiceCegoDoCpf
{
    private IndiceCegoDoCpf(string hexadecimal) => Hexadecimal = hexadecimal;

    public string Hexadecimal { get; }

    public static IndiceCegoDoCpf De(ReadOnlySpan<byte> indice)
    {
        if (indice.IsEmpty)
        {
            throw new ArgumentException("O índice cego do CPF não pode ser vazio.", nameof(indice));
        }

        return new IndiceCegoDoCpf(Convert.ToHexStringLower(indice));
    }
}

/// <summary>Uma inscrição enviada que escolheu a edição do ENEM, identificada pelo índice cego do CPF.</summary>
public sealed record InscritoDaEdicao(Guid InscricaoId, IndiceCegoDoCpf Indice);
