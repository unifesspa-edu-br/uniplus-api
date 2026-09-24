namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cópia por valor do tipo de etapa resolvido em Configuração no momento da
/// definição: a identidade (origem, código, nome, origem da nota no ENEM) e o que o tipo
/// admite como caráter da etapa (pontuação, eliminação). A etapa nunca relê a configuração
/// de tipos para mudar a própria identidade; os sinalizadores são regravados só quando o
/// caráter ou o vínculo desta etapa muda — que é quando a gravação já confere o tipo no
/// cadastro.
/// </summary>
/// <remarks>
/// Duas etapas do mesmo tipo, definidas em momentos diferentes, podem congelar
/// sinalizadores distintos. Nada compara os sinalizadores entre etapas.
/// </remarks>
public sealed record TipoEtapaSnapshot
{
    private TipoEtapaSnapshot() { }

    private TipoEtapaSnapshot(
        Guid origemId, string codigo, string nome, bool admitePontuacao, bool admiteEliminacao, bool notaDeOrigemNoEnem)
    {
        OrigemId = origemId;
        Codigo = codigo;
        Nome = nome;
        AdmitePontuacao = admitePontuacao;
        AdmiteEliminacao = admiteEliminacao;
        NotaDeOrigemNoEnem = notaDeOrigemNoEnem;
    }

    /// <remarks>Usado apenas na construção; a identidade é persistida no próprio snapshot.</remarks>
    public Guid OrigemId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Se o tipo admitia etapa classificatória (que compõe a nota) quando foi congelado.</summary>
    public bool AdmitePontuacao { get; private set; }

    /// <summary>Se o tipo admitia etapa eliminatória quando foi congelado.</summary>
    public bool AdmiteEliminacao { get; private set; }

    /// <summary>
    /// Se a nota das etapas do tipo vem do ENEM. Faz parte da identidade: muda só quando o
    /// vínculo da etapa muda, nunca na regravação dos sinalizadores.
    /// </summary>
    public bool NotaDeOrigemNoEnem { get; private set; }

    public static Result<TipoEtapaSnapshot> Criar(
        Guid origemId,
        string codigo,
        string nome,
        bool admitePontuacao,
        bool admiteEliminacao,
        bool notaDeOrigemNoEnem)
    {
        if (origemId == Guid.Empty)
        {
            return Falha("TipoEtapaSnapshot.OrigemIdObrigatorio", "Origem do tipo de etapa é obrigatória.");
        }
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Falha("TipoEtapaSnapshot.CodigoObrigatorio", "Código do tipo de etapa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha("TipoEtapaSnapshot.NomeObrigatorio", "Nome do tipo de etapa é obrigatório.");
        }

        // O cadastro de tipos não admite tipo que não pontua nem elimina — nenhum caráter de
        // etapa sobraria para ele. A cópia congelada carrega a mesma garantia: sem ela, um
        // envelope adulterado reporia uma etapa cujo caráter nenhum tipo real admitiria.
        if (!admitePontuacao && !admiteEliminacao)
        {
            return Falha(
                "TipoEtapaSnapshot.SemCaraterAdmitido",
                "Snapshot do tipo de etapa deve admitir compor a nota final, eliminar candidato, ou os dois.");
        }

        // O cadastro não admite tipo com nota do ENEM que não pontue; o envelope que
        // chegasse assim reporia uma etapa de nota do ENEM que não compõe a média.
        if (notaDeOrigemNoEnem && !admitePontuacao)
        {
            return Falha(
                "TipoEtapaSnapshot.NotaDeOrigemNoEnemSemPontuacao",
                "Snapshot do tipo de etapa com nota de origem no ENEM deve admitir compor a nota final.");
        }

        // NFC na fronteira de congelamento (mesma normalização do payload canônico,
        // HashCanonicalComputer.NormalizeNfc): sem isso, o mesmo código digitado em forma
        // decomposta aqui e recomposta (NFC) ao serializar no envelope vira dois valores
        // ordinalmente diferentes depois de um ciclo de retificação descartada — o código
        // congelado muda de representação sem que o dado mude de significado, e
        // AvaliadorConformidadeLegal, que compara por igualdade ordinal, passa a reportar a
        // etapa como ausente mesmo com a regra e a etapa usando o "mesmo" código.
        string? codigoNormalizado = TextoCongelado.Normalizar(codigo);
        string? nomeNormalizado = TextoCongelado.Normalizar(nome);

        // Defesa de decode: um envelope adulterado não pode injetar U+0000 e só falhar
        // depois, na constraint do Postgres — o VO recusa aqui, na fronteira do domínio.
        if (codigoNormalizado is null || nomeNormalizado is null
            || TextoCongelado.ContemCaractereNulo(codigoNormalizado) || TextoCongelado.ContemCaractereNulo(nomeNormalizado))
        {
            return Falha(
                "TipoEtapaSnapshot.CaractereNulo",
                "Snapshot do tipo de etapa não pode conter o caractere nulo (U+0000) nem caractere que não seja texto.");
        }

        if (codigoNormalizado.Length > 64 || nomeNormalizado.Length > 200)
        {
            return Falha("TipoEtapaSnapshot.TamanhoInvalido", "Snapshot do tipo de etapa excede o tamanho permitido.");
        }

        return Result<TipoEtapaSnapshot>.Success(new TipoEtapaSnapshot(
            origemId, codigoNormalizado, nomeNormalizado, admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem));
    }

    /// <summary>
    /// O mesmo tipo com os sinalizadores relidos do cadastro. Identidade (origem, código,
    /// nome, origem da nota no ENEM) vem deste snapshot, nunca do cadastro: mudar o caráter
    /// da etapa refresca o que o tipo admite, não renomeia o que foi congelado.
    /// </summary>
    /// <remarks>
    /// Passa pelas mesmas regras de <see cref="Criar"/>. Os sinalizadores vêm de uma vista do
    /// cadastro, que já as cumpre; uma recusa aqui é erro de programação, não dado de entrada,
    /// e por isso lança em vez de devolver <c>Result</c>.
    /// </remarks>
    public TipoEtapaSnapshot ComSinalizadores(bool admitePontuacao, bool admiteEliminacao)
    {
        Result<TipoEtapaSnapshot> relido = Criar(OrigemId, Codigo, Nome, admitePontuacao, admiteEliminacao, NotaDeOrigemNoEnem);
        return relido.IsSuccess
            ? relido.Value!
            : throw new ArgumentException(relido.Error!.Message, nameof(admitePontuacao));
    }

    public override string ToString() => Codigo;

    private static Result<TipoEtapaSnapshot> Falha(string code, string message) =>
        Result<TipoEtapaSnapshot>.Failure(new DomainError(code, message));
}
