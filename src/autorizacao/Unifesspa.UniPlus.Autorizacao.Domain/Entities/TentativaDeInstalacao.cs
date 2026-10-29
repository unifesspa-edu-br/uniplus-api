namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Tentativa de confirmar a instalação com um código, de quem iniciou ou de outra pessoa. Só
/// se acrescenta, nunca se altera, e nunca guarda o código. É evidência forense (ADR-0063): não
/// tem soft delete nem auditoria de alteração.
/// </summary>
public sealed class TentativaDeInstalacao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>A instalação tentada; nula quando quem tentou não tem instalação aguardando código.</summary>
    public Guid? InstalacaoId { get; private init; }

    public string AutorEmissor { get; private init; } = string.Empty;

    public string AutorSub { get; private init; } = string.Empty;

    public DateTimeOffset Instante { get; private init; }

    public ResultadoDaTentativaDeInstalacao Resultado { get; private init; }

    // Construtor de materialização do EF Core.
    private TentativaDeInstalacao()
    {
    }

    public static TentativaDeInstalacao Registrar(
        Guid? instalacaoId,
        Identidade autor,
        ResultadoDaTentativaDeInstalacao resultado,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(autor);

        return new TentativaDeInstalacao
        {
            InstalacaoId = instalacaoId,
            AutorEmissor = autor.Emissor,
            AutorSub = autor.Sub,
            Instante = instante,
            Resultado = resultado,
        };
    }
}
