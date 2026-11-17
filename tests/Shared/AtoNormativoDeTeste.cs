namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Os dados de um ato normativo de teste, com os valores de um edital comum; o teste declara só o
/// que o cenário exige. O <see cref="Id"/> vazio faz a fábrica gerar o identificador. É o único
/// ponto dos testes que conhece a assinatura de <c>AtoNormativo.Registrar</c>.
/// </summary>
internal sealed record AtoNormativoDeTeste
{
    public const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    public Guid Id { get; init; } = Guid.Empty;

    public string Orgao { get; init; } = "CEPS";

    public string Serie { get; init; } = "EDITAL";

    public int Ano { get; init; } = 2026;

    public string? Numero { get; init; } = "13";

    public string TipoCodigo { get; init; } = "EDITAL_ABERTURA";

    public string? TipoNome { get; init; }

    public bool CongelaConfiguracao { get; init; }

    public bool EfeitoIrreversivel { get; init; }

    public bool UnicoPorObjeto { get; init; }

    public DateOnly DataDoAto { get; init; } = new(2026, 3, 13);

    public string DocumentoHash { get; init; } = HashValido;

    public string Assinante { get; init; } = PersonasDeTeste.NomeKevinCarvalho;

    public DateTimeOffset PublicadoEm { get; init; } = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    public ReferenciaVersaoDoObjeto? VersaoDoObjeto { get; init; }

    public Guid? AtoRetificadoId { get; init; }

    public string? MotivoRetificacao { get; init; }

    public IEnumerable<(string EntidadeTipo, Guid EntidadeId)>? Vinculos { get; init; }

    public IEnumerable<(string Nome, string TipoMidia, string Hash)>? Anexos { get; init; }

    public AtoNormativo Registrar() =>
        AtoNormativo.Registrar(
            Id, Orgao, Serie, Ano, Numero, TipoCodigo, CongelaConfiguracao, EfeitoIrreversivel,
            UnicoPorObjeto, DataDoAto, DocumentoHash, Assinante, PublicadoEm, VersaoDoObjeto,
            AtoRetificadoId, MotivoRetificacao, Vinculos, TipoNome, Anexos);
}
