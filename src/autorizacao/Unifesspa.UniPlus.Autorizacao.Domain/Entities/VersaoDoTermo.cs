namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Uma versão publicada do termo de responsabilidade. Só se acrescenta, nunca se altera nem se apaga: é evidência
/// forense (ADR-0063), porque pedidos e aceites apontam para ela. O autor fica gravado para auditoria e nunca traz CPF.
/// </summary>
public sealed class VersaoDoTermo : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid TermoId { get; private init; }

    /// <summary>Número sequencial da versão, a partir de 1.</summary>
    public int Numero { get; private init; }

    /// <summary>O texto já normalizado: o que se exibe e o que se resume.</summary>
    public string Texto { get; private init; } = string.Empty;

    /// <summary>SHA-256 do texto, em hexadecimal minúsculo.</summary>
    public string Resumo { get; private init; } = string.Empty;

    /// <summary>O ato institucional que aprova a versão.</summary>
    public string Ato { get; private init; } = string.Empty;

    public string AutorEmissor { get; private init; } = string.Empty;

    public string AutorSub { get; private init; } = string.Empty;

    public string? AutorNome { get; private init; }

    public DateTimeOffset PublicadaEm { get; private init; }

    // Construtor de materialização do EF Core.
    private VersaoDoTermo()
    {
    }

    internal static VersaoDoTermo Registrar(Guid termoId, int numero, ConteudoDoTermo conteudo, Identidade autor, DateTimeOffset instante) =>
        new()
        {
            TermoId = termoId,
            Numero = numero,
            Texto = conteudo.Texto,
            Resumo = conteudo.Resumo,
            Ato = conteudo.Ato,
            AutorEmissor = autor.Emissor,
            AutorSub = autor.Sub,
            AutorNome = autor.Nome,
            PublicadaEm = instante,
        };
}
