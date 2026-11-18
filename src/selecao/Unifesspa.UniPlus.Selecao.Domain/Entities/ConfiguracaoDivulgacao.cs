namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração de divulgação pública do certame (UNI-REQ-0050, issue #563): quais campos do
/// candidato aparecem nas listas públicas de resultado. Ausência desta entidade já é o default
/// minimizado — só o número de inscrição — e não uma escolha administrativa pendente.
/// </summary>
/// <remarks>
/// <para>
/// <b>Três estados, não dois (D13 da revisão adversarial).</b> No comando que grava esta
/// configuração, <c>CamposPublicos</c> nulo remove a configuração explícita (o processo volta ao
/// default minimizado); lista vazia é sempre recusada aqui; lista não nula cria ou substitui
/// integralmente. É essa distinção — nulo≠vazio — que permite desfazer uma ampliação anterior.
/// </para>
/// <para>
/// <b>O conjunto é guardado já deduplicado e na ordem canônica</b> (ASCII, <see cref="StringComparer.Ordinal"/>)
/// — não na ordem em que o cliente enviou. Sem isso, duas requisições semanticamente iguais
/// («numero_inscricao, nome_abreviado» e «nome_abreviado, numero_inscricao») persistiriam como
/// coleções distintas para o <c>ValueComparer</c> sequencial do jsonb, produzindo um <c>UPDATE</c>
/// espúrio a cada requisição equivalente.
/// </para>
/// </remarks>
public sealed class ConfiguracaoDivulgacao : EntityBase, IParteDoProcesso
{
    /// <summary>O piso da divulgação pública — sempre presente, é o único campo do default minimizado.</summary>
    public const string NumeroInscricao = "numero_inscricao";

    /// <summary>Forma reduzida do nome do candidato (ADR-0082) — dispensa justificativa.</summary>
    public const string NomeAbreviado = "nome_abreviado";

    /// <summary>Nome integral do candidato — exige justificativa (UNI-REQ-0050).</summary>
    public const string Nome = "nome";

    /// <summary>
    /// O vocabulário fechado da divulgação pública, na ordem canônica de apresentação — do
    /// piso à forma mais identificadora do candidato. É desta lista que sai tanto a recusa
    /// de campo não permitido quanto a leitura pública do vocabulário: uma segunda lista,
    /// mantida à parte para o cliente descobrir os códigos, envelheceria sozinha.
    /// </summary>
    /// <remarks>
    /// A ordem não é alfabética de propósito. Ela ordena por quanto cada campo identifica
    /// quem prestou o certame, que é a leitura que importa para quem decide o que publicar —
    /// e a ordem alfabética colocaria <c>nome</c> antes de <c>nome_abreviado</c>, invertendo
    /// justamente esse sentido. A ordem em que os campos são <b>persistidos</b> continua
    /// sendo a canônica ASCII, decidida em <see cref="Criar"/>.
    /// </remarks>
    public static readonly IReadOnlyList<CampoDivulgacaoPublica> CamposPermitidos =
    [
        new(NumeroInscricao, "Número de inscrição", Obrigatorio: true, ExigeJustificativa: false),
        new(NomeAbreviado, "Nome abreviado", Obrigatorio: false, ExigeJustificativa: false),
        new(Nome, "Nome completo", Obrigatorio: false, ExigeJustificativa: true),
    ];

    /// <summary>
    /// Teto da justificativa normalizada — mesma grandeza de <c>DocumentoExigidoBaseLegal.Observacao</c>: texto de negócio livre, não um rótulo curto.
    /// </summary>
    public const int JustificativaMaxLength = 1000;

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Os campos publicados, deduplicados e na ordem canônica — a mesma ordem que o envelope emite.</summary>
    public IReadOnlyList<string> CamposPublicos { get; private set; } = [];

    /// <summary>Justificativa da ampliação, já normalizada (Trim + NFC). Obrigatória apenas quando <see cref="Nome"/> está presente.</summary>
    public string? Justificativa { get; private set; }

    private ConfiguracaoDivulgacao() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o array
    /// <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as regras violadas no
    /// mesmo lote. As checagens rodam sobre a lista bruta (não a canônica): a construção da
    /// coleção deduplicada/ordenada só acontece se nenhuma violação for encontrada.
    /// </summary>
    public static Result<ConfiguracaoDivulgacao> Criar(IReadOnlyList<string> camposPublicos, string? justificativa)
    {
        ArgumentNullException.ThrowIfNull(camposPublicos);

        string? justificativaNormalizada = NormalizarJustificativa(justificativa);
        List<FieldError> erros = RecusasDosCampos(camposPublicos);
        erros.AddRange(RecusasDaJustificativa(justificativaNormalizada, camposPublicos.Contains(Nome, StringComparer.Ordinal)));

        return erros.Count > 0
            ? Result<ConfiguracaoDivulgacao>.ValidationFailure(erros)
            : Result<ConfiguracaoDivulgacao>.Success(Montar(camposPublicos, justificativaNormalizada));
    }

    private static ConfiguracaoDivulgacao Montar(IReadOnlyList<string> camposPublicos, string? justificativaNormalizada) => new()
    {
        CamposPublicos = [.. camposPublicos.Distinct(StringComparer.Ordinal).OrderBy(static c => c, StringComparer.Ordinal)],
        Justificativa = justificativaNormalizada,
    };

    /// <summary>
    /// O default minimizado: só o piso, sem justificativa — o efetivo que um processo sem
    /// configuração explícita já divulga (D5 do plano de congelamento). Consumido pelo decoder do
    /// envelope para decidir se o bloco reidratado deve virar ausência em vez de entidade.
    /// </summary>
    public static bool EhDefaultMinimizado(IReadOnlyList<string> camposPublicos, string? justificativa)
    {
        ArgumentNullException.ThrowIfNull(camposPublicos);
        return justificativa is null && camposPublicos.Count == 1 && camposPublicos[0] == NumeroInscricao;
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    private static List<FieldError> RecusasDosCampos(IReadOnlyList<string> camposPublicos)
    {
        List<FieldError> erros = [];
        ConferirListaNaoVazia(camposPublicos, erros);
        ConferirVocabulario(camposPublicos, erros);
        ConferirPiso(camposPublicos, erros);
        ConferirFormasExcludentes(camposPublicos, erros);
        return erros;
    }

    private static void ConferirListaNaoVazia(IReadOnlyList<string> camposPublicos, List<FieldError> erros)
    {
        if (camposPublicos.Count == 0)
        {
            Recusar(erros, "camposPublicos", "ConfiguracaoDivulgacao.CamposPublicosVazio",
                "A lista de campos públicos não pode ser vazia — para restaurar o default, remova a configuração explícita.");
        }
    }

    // Item nulo não pertence ao vocabulário — reportado junto dos demais campos fora do
    // vocabulário fechado, em vez de escapar de FirstOrDefault(...) is { } (que descarta um
    // resultado nulo como "não encontrado") e sobreviver silenciosamente na lista canônica.
    // A mensagem não ecoa os valores rejeitados (ADR-0023): errors[].message nunca carrega
    // reflexo do dado rejeitado, mesmo quando o campo em si não é PII.
    private static void ConferirVocabulario(IReadOnlyList<string> camposPublicos, List<FieldError> erros)
    {
        bool temCampoNaoPermitido = camposPublicos.Any(static c =>
            c is null || !CamposPermitidos.Any(permitido => string.Equals(permitido.Codigo, c, StringComparison.Ordinal)));
        if (temCampoNaoPermitido)
        {
            Recusar(erros, "camposPublicos", "ConfiguracaoDivulgacao.CampoNaoPermitido",
                $"Um ou mais campos não pertencem ao vocabulário de divulgação pública ({NumeroInscricao}, {NomeAbreviado}, {Nome}).");
        }
    }

    // Redundante com CamposPublicosVazio quando a lista já está vazia — não acrescenta um
    // segundo erro para a mesma causa raiz.
    private static void ConferirPiso(IReadOnlyList<string> camposPublicos, List<FieldError> erros)
    {
        if (camposPublicos.Count > 0 && !camposPublicos.Contains(NumeroInscricao, StringComparer.Ordinal))
        {
            Recusar(erros, "camposPublicos", "ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio",
                $"'{NumeroInscricao}' é o piso da divulgação pública — não pode ser removido da lista.");
        }
    }

    private static void ConferirFormasExcludentes(IReadOnlyList<string> camposPublicos, List<FieldError> erros)
    {
        if (camposPublicos.Contains(NomeAbreviado, StringComparer.Ordinal) && camposPublicos.Contains(Nome, StringComparer.Ordinal))
        {
            Recusar(erros, "camposPublicos", "ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes",
                $"'{NomeAbreviado}' e '{Nome}' não podem ser publicados ao mesmo tempo — escolha uma forma de identificação do candidato.");
        }
    }

    // Os dois primeiros ifs são independentes: uma justificativa longa demais E com caractere
    // inválido viola as duas regras ao mesmo tempo, e um else if esconderia a primeira até o
    // cliente reenviar sem a segunda.
    private static List<FieldError> RecusasDaJustificativa(string? justificativa, bool publicaNome)
    {
        List<FieldError> erros = [];
        ConferirCaracteresDaJustificativa(justificativa, erros);
        ConferirTamanhoDaJustificativa(justificativa, erros);
        if (publicaNome && justificativa is null)
        {
            Recusar(erros, "justificativa", "ConfiguracaoDivulgacao.JustificativaObrigatoria",
                $"Publicar '{Nome}' exige justificativa (UNI-REQ-0050) — '{NumeroInscricao}' e '{NomeAbreviado}' dispensam.");
        }

        return erros;
    }

    // Nulo, controle e formatação sobrevivem a IsNullOrWhiteSpace, Trim e NormalizeNfc, e o
    // nulo chegaria ao SaveChanges, onde o PostgreSQL recusa o byte zero (500 em vez de erro de
    // validação). Quebra de linha e tabulação são texto legítimo de justificativa. O mesmo guard
    // cobre o comando e a restauração do envelope, que passa por este método.
    private static void ConferirCaracteresDaJustificativa(string? justificativa, List<FieldError> erros)
    {
        if (justificativa is not null && TextoNormalizavel.TemCaractereRecusadoEmVariasLinhas(justificativa))
        {
            Recusar(erros, "justificativa", "ConfiguracaoDivulgacao.JustificativaCaractereInvalido",
                "A justificativa não pode conter o caractere nulo, de controle (exceto quebra de linha e tabulação) ou de formatação.");
        }
    }

    private static void ConferirTamanhoDaJustificativa(string? justificativa, List<FieldError> erros)
    {
        if (justificativa is { Length: > JustificativaMaxLength })
        {
            Recusar(erros, "justificativa", "ConfiguracaoDivulgacao.JustificativaMuitoLonga",
                $"A justificativa deve ter no máximo {JustificativaMaxLength} caracteres.");
        }
    }

    private static void Recusar(List<FieldError> erros, string campo, string codigo, string mensagem) =>
        erros.Add(new(campo, new DomainError(codigo, mensagem)));

    /// <summary>
    /// <b>Trim + NFC</b>: o valor guardado é o que o canonicalizador do envelope congelaria, e Postgres não normaliza
    /// texto. Uma justificativa só de espaços colapsa para <see langword="null"/> — não carrega
    /// informação a mais que a ausência.
    /// </summary>
    private static string? NormalizarJustificativa(string? justificativa) =>
        string.IsNullOrWhiteSpace(justificativa) ? null : HashCanonicalComputer.NormalizeNfc(justificativa.Trim());
}
