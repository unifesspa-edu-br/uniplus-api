namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os valores de uma linha de <c>atos_normativos</c> inserida por SQL cru, fora do agregado, com os de um ato
/// comum; cada teste muda só o que o CHECK exercitado exige.
/// </summary>
internal sealed record LinhaCruaDoAto
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public int Ano { get; init; } = 2026;

    public DateOnly DataDoAto { get; init; } = new(2026, 3, 13);

    public string DocumentoHash { get; init; } = AtoNormativoDeTeste.HashValido;

    public Guid? VersaoId { get; init; }

    public string? VersaoHash { get; init; }

    public Guid? AtoRetificadoId { get; init; }

    public string? MotivoRetificacao { get; init; }
}
