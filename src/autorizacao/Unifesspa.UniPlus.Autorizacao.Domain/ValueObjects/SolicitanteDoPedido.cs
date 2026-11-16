namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Quem pediu o acesso, como o token dele o trouxe quando pediu. O e-mail é o verificado do token e serve só para avisá-lo do
/// desfecho; o nome informa quem lê a trilha. Nunca carrega CPF.
/// </summary>
public sealed record SolicitanteDoPedido
{
    public string Emissor { get; private init; } = string.Empty;

    public string ContaId { get; private init; } = string.Empty;

    public string? Nome { get; private init; }

    /// <summary>E-mail verificado do token do solicitante, em minúsculas; nulo quando a sessão não trouxe e-mail verificado.</summary>
    public string? Email { get; private init; }

    public Identidade Identidade => new(Emissor, ContaId, Nome);

    // Construtor de materialização do EF Core.
    private SolicitanteDoPedido()
    {
    }

    public static SolicitanteDoPedido De(PessoaNoToken pessoa)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return new SolicitanteDoPedido
        {
            Emissor = pessoa.Identidade.Emissor,
            ContaId = pessoa.Identidade.ContaId,
            Nome = NomeDeExibicao.Normalizar(pessoa.Nome),
            Email = pessoa.EmailVerificadoNormalizado,
        };
    }

    public bool Eh(Identidade identidade) => Identidade == identidade;
}
