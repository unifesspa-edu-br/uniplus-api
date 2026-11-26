namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Extensions;
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
        SituacaoDoTexto? situacaoCodigo = SituacaoDoCampo(codigo, CodigoMaxLength, out string codigoCongelado);
        SituacaoDoTexto? situacaoRotulo = SituacaoDoCampo(rotulo, RotuloMaxLength, out string rotuloCongelado);
        DomainError? recusa = RecusaDoGrupo(situacaoCodigo, situacaoRotulo);
        return recusa is not null
            ? Result<GrupoAreaEnemSnapshot>.Failure(recusa)
            : Result<GrupoAreaEnemSnapshot>.Success(new GrupoAreaEnemSnapshot(codigoCongelado, rotuloCongelado));
    }

    public override string ToString() => Codigo;

    private static SituacaoDoTexto? SituacaoDoCampo(string? texto, int tamanhoMaximo, out string congelado)
    {
        congelado = string.Empty;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }
        // NFC no envelope evita mudanças após retificação; o texto decodificado pode não estar nessa forma.
        string? normalizado = TextoCongelado.Normalizar(texto);
        // Defesa contra envelope adulterado antes da constraint do Postgres.
        if (normalizado is null || TextoCongelado.TemCaractereInvalido(normalizado))
        {
            return SituacaoDoTexto.CaractereInvalido;
        }
        congelado = normalizado;
        return congelado.Length > tamanhoMaximo ? SituacaoDoTexto.TamanhoExcedido : SituacaoDoTexto.Valido;
    }

    private static DomainError? RecusaDoGrupo(SituacaoDoTexto? codigo, SituacaoDoTexto? rotulo) => (codigo, rotulo) switch
    {
        (null, _) => new("GrupoAreaEnemSnapshot.CodigoObrigatorio", "Código do grupo de área do ENEM é obrigatório."),
        (_, null) => new("GrupoAreaEnemSnapshot.RotuloObrigatorio", "Rótulo do grupo de área do ENEM é obrigatório."),
        (SituacaoDoTexto.CaractereInvalido, _) or (_, SituacaoDoTexto.CaractereInvalido) => new(
            "GrupoAreaEnemSnapshot.CaractereInvalido",
            "Grupo de área do ENEM não pode conter o caractere nulo, de controle ou de formatação, nem caractere que não seja texto."),
        (SituacaoDoTexto.TamanhoExcedido, _) or (_, SituacaoDoTexto.TamanhoExcedido) => new(
            "GrupoAreaEnemSnapshot.TamanhoInvalido",
            $"Grupo de área do ENEM excede o tamanho permitido (código até {CodigoMaxLength} e rótulo até {RotuloMaxLength} caracteres)."),
        _ => null,
    };
}
