namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cópia por valor do grupo de área do ENEM do curso de uma oferta, com código e rótulo,
/// no momento em que a distribuição de vagas é definida (snapshot-copy, ADR-0061). O
/// código é a identidade do grupo — é por ele que o processo casa a oferta com a linha de
/// Pesos por Área —, e o rótulo é o que o edital publicado mostra.
/// </summary>
/// <remarks>
/// Editar o curso depois não alcança o processo; quem quiser o grupo novo redefine a
/// distribuição.
/// </remarks>
public sealed record GrupoAreaEnemSnapshot
{
    /// <summary>Tamanho máximo do código congelado — o mesmo da coluna do cadastro de cursos.</summary>
    public const int CodigoMaxLength = 30;

    /// <summary>Tamanho máximo do rótulo congelado — o mesmo da coluna do cadastro de cursos.</summary>
    public const int RotuloMaxLength = 60;

    private const char CaractereNulo = (char)0;

    // EF Core materialization
    private GrupoAreaEnemSnapshot() { }

    private GrupoAreaEnemSnapshot(string codigo, string rotulo)
    {
        Codigo = codigo;
        Rotulo = rotulo;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Rotulo { get; private set; } = string.Empty;

    public static Result<GrupoAreaEnemSnapshot> Criar(string? codigo, string? rotulo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Falha("GrupoAreaEnemSnapshot.CodigoObrigatorio", "Código do grupo de área do ENEM é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(rotulo))
        {
            return Falha("GrupoAreaEnemSnapshot.RotuloObrigatorio", "Rótulo do grupo de área do ENEM é obrigatório.");
        }

        // Código e rótulo entram no envelope canônico em NFC. A normalização na fronteira de
        // congelamento evita que o mesmo texto, em forma decomposta, vire outro valor depois
        // de um ciclo de retificação. O código vindo do cadastro é ASCII, mas este tipo também
        // recebe o que o decodificador leu do envelope, e ali nada garante a forma.
        string codigoNormalizado = codigo.Trim().Normalize(NormalizationForm.FormC);
        string rotuloNormalizado = rotulo.Trim().Normalize(NormalizationForm.FormC);

        // Defesa de decode: um envelope adulterado não pode injetar U+0000 e só falhar
        // depois, na constraint do Postgres.
        if (codigoNormalizado.Contains(CaractereNulo) || rotuloNormalizado.Contains(CaractereNulo))
        {
            return Falha(
                "GrupoAreaEnemSnapshot.CaractereNulo",
                "Grupo de área do ENEM não pode conter o caractere nulo (U+0000).");
        }

        if (codigoNormalizado.Length > CodigoMaxLength || rotuloNormalizado.Length > RotuloMaxLength)
        {
            return Falha(
                "GrupoAreaEnemSnapshot.TamanhoInvalido",
                $"Grupo de área do ENEM excede o tamanho permitido (código até {CodigoMaxLength} e rótulo até {RotuloMaxLength} caracteres).");
        }

        return Result<GrupoAreaEnemSnapshot>.Success(new GrupoAreaEnemSnapshot(codigoNormalizado, rotuloNormalizado));
    }

    public override string ToString() => Codigo;

    private static Result<GrupoAreaEnemSnapshot> Falha(string code, string message) =>
        Result<GrupoAreaEnemSnapshot>.Failure(new DomainError(code, message));
}
