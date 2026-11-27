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

    private const string CampoDoGrupoNoPedido = "grupoAreaEnem";

    private static readonly CampoDoGrupo DoCodigo = new("codigo", "O código do grupo de área do ENEM", CodigoMaxLength, "CodigoObrigatorio");

    private static readonly CampoDoGrupo DoRotulo = new("rotulo", "O rótulo do grupo de área do ENEM", RotuloMaxLength, "RotuloObrigatorio");

    // EF Core materialization
    private GrupoAreaEnemSnapshot() { }

    private GrupoAreaEnemSnapshot(string codigo, string rotulo)
    {
        Codigo = codigo;
        Rotulo = rotulo;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Rotulo { get; private set; } = string.Empty;

    /// <summary>Acumula a recusa do código e a do rótulo, cada uma no seu campo dentro de <c>grupoAreaEnem</c>.</summary>
    public static Result<GrupoAreaEnemSnapshot> Criar(string? codigo, string? rotulo)
    {
        (string codigoCongelado, FieldError? recusaDoCodigo) = ConferirCampo(codigo, DoCodigo);
        (string rotuloCongelado, FieldError? recusaDoRotulo) = ConferirCampo(rotulo, DoRotulo);
        FieldError[] recusas = [.. new[] { recusaDoCodigo, recusaDoRotulo }.OfType<FieldError>()];
        return recusas.Length > 0
            ? Result<GrupoAreaEnemSnapshot>.ValidationFailure(recusas)
            : Result<GrupoAreaEnemSnapshot>.Success(new GrupoAreaEnemSnapshot(codigoCongelado, rotuloCongelado));
    }

    public override string ToString() => Codigo;

    private static (string Congelado, FieldError? Recusa) ConferirCampo(string? texto, CampoDoGrupo campo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return (string.Empty, campo.Recusar(campo.CausaObrigatoria, $"{campo.Nome} é obrigatório."));
        }
        // NFC no envelope evita mudanças após retificação; o texto decodificado pode não estar nessa forma.
        string? normalizado = TextoCongelado.Normalizar(texto);
        // Defesa contra envelope adulterado antes da constraint do Postgres.
        return normalizado is null || TextoCongelado.TemCaractereInvalido(normalizado)
            ? (string.Empty, campo.Recusar(
                "CaractereInvalido",
                $"{campo.Nome} não pode conter o caractere nulo, de controle ou de formatação, nem caractere que não seja texto."))
            : ConferirTamanho(normalizado, campo);
    }

    private static (string Congelado, FieldError? Recusa) ConferirTamanho(string normalizado, CampoDoGrupo campo) =>
        normalizado.Length > campo.Maximo
            ? (normalizado, campo.Recusar("TamanhoInvalido", $"{campo.Nome} deve ter até {campo.Maximo} caracteres."))
            : (normalizado, null);

    private sealed record CampoDoGrupo(string Campo, string Nome, int Maximo, string CausaObrigatoria)
    {
        public FieldError Recusar(string causa, string mensagem) =>
            new FieldError(Campo, new DomainError($"GrupoAreaEnemSnapshot.{causa}", mensagem)).NoCaminhoDe(CampoDoGrupoNoPedido);
    }
}
