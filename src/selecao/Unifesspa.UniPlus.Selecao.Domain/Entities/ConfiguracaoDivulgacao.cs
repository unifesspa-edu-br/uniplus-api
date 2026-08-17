namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
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
/// integralmente. É essa distinção — nulo≠vazio — que permite desfazer uma ampliação anterior,
/// inclusive durante uma sessão de retificação.
/// </para>
/// <para>
/// <b>O conjunto é guardado já deduplicado e na ordem canônica</b> (ASCII, <see cref="StringComparer.Ordinal"/>)
/// — não na ordem em que o cliente enviou. Sem isso, duas requisições semanticamente iguais
/// («numero_inscricao, nome_abreviado» e «nome_abreviado, numero_inscricao») persistiriam como
/// coleções distintas para o <c>ValueComparer</c> sequencial do jsonb, produzindo um <c>UPDATE</c>
/// espúrio a cada requisição equivalente.
/// </para>
/// </remarks>
public sealed class ConfiguracaoDivulgacao : EntityBase
{
    /// <summary>O piso da divulgação pública — sempre presente, é o único campo do default minimizado.</summary>
    public const string NumeroInscricao = "numero_inscricao";

    /// <summary>Forma reduzida do nome do candidato (ADR-0082) — dispensa justificativa.</summary>
    public const string NomeAbreviado = "nome_abreviado";

    /// <summary>Nome integral do candidato — exige justificativa (UNI-REQ-0050).</summary>
    public const string Nome = "nome";

    /// <summary>
    /// Teto da justificativa normalizada — mesma grandeza de <see cref="RascunhoRetificacao.MotivoMaxLength"/>
    /// e de <c>DocumentoExigidoBaseLegal.Observacao</c>: texto de negócio livre, não um rótulo curto.
    /// </summary>
    public const int JustificativaMaxLength = 1000;

    /// <summary>
    /// U+0000, escrito por código de caractere (não como literal <c>'\0'</c> no fonte) para que
    /// nenhum editor ou ferramenta de texto insira o byte cru no arquivo — o PostgreSQL recusa
    /// esse byte em coluna de texto, e é exatamente o que este caractere representa.
    /// </summary>
    private const char CaractereNulo = (char)0;

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

        List<FieldError> erros = [];

        if (camposPublicos.Count == 0)
        {
            erros.Add(new("camposPublicos", new DomainError(
                "ConfiguracaoDivulgacao.CamposPublicosVazio",
                "A lista de campos públicos não pode ser vazia — para restaurar o default, remova a configuração explícita.")));
        }

        // Item nulo não pertence ao vocabulário — reportado junto dos demais campos fora do
        // vocabulário fechado, em vez de escapar de FirstOrDefault(...) is { } (que descarta um
        // resultado nulo como "não encontrado") e sobreviver silenciosamente na lista canônica.
        // A mensagem não ecoa os valores rejeitados (ADR-0023): errors[].message nunca carrega
        // reflexo do dado rejeitado, mesmo quando o campo em si não é PII.
        bool temCampoNaoPermitido = camposPublicos.Any(static c => c is not (NumeroInscricao or NomeAbreviado or Nome));
        if (temCampoNaoPermitido)
        {
            erros.Add(new("camposPublicos", new DomainError(
                "ConfiguracaoDivulgacao.CampoNaoPermitido",
                $"Um ou mais campos não pertencem ao vocabulário de divulgação pública ({NumeroInscricao}, {NomeAbreviado}, {Nome}).")));
        }

        // Redundante com CamposPublicosVazio quando a lista já está vazia — não acrescenta um
        // segundo erro para a mesma causa raiz.
        if (camposPublicos.Count > 0 && !camposPublicos.Contains(NumeroInscricao, StringComparer.Ordinal))
        {
            erros.Add(new("camposPublicos", new DomainError(
                "ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio",
                $"'{NumeroInscricao}' é o piso da divulgação pública — não pode ser removido da lista.")));
        }

        bool temNomeAbreviado = camposPublicos.Contains(NomeAbreviado, StringComparer.Ordinal);
        bool temNome = camposPublicos.Contains(Nome, StringComparer.Ordinal);

        if (temNomeAbreviado && temNome)
        {
            erros.Add(new("camposPublicos", new DomainError(
                "ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes",
                $"'{NomeAbreviado}' e '{Nome}' não podem ser publicados ao mesmo tempo — escolha uma forma de identificação do candidato.")));
        }

        string? justificativaNormalizada = NormalizarJustificativa(justificativa);

        // U+0000 sobrevive a IsNullOrWhiteSpace, Trim e NormalizeNfc — nenhum dos três o
        // reconhece como espaço em branco nem o remove. Sem esta guarda ele chegaria ao
        // SaveChanges e o PostgreSQL recusaria o byte zero em coluna de texto (500 em vez de
        // erro de validação); recusado aqui, o mesmo guard cobre tanto o comando quanto a
        // restauração do envelope, que também passa por este método.
        if (justificativaNormalizada is not null && justificativaNormalizada.Contains(CaractereNulo))
        {
            erros.Add(new("justificativa", new DomainError(
                "ConfiguracaoDivulgacao.JustificativaComCaractereNulo",
                "A justificativa não pode conter o caractere nulo (U+0000).")));
        }
        else if (justificativaNormalizada is { Length: > JustificativaMaxLength })
        {
            erros.Add(new("justificativa", new DomainError(
                "ConfiguracaoDivulgacao.JustificativaMuitoLonga",
                $"A justificativa deve ter no máximo {JustificativaMaxLength} caracteres.")));
        }

        if (temNome && justificativaNormalizada is null)
        {
            erros.Add(new("justificativa", new DomainError(
                "ConfiguracaoDivulgacao.JustificativaObrigatoria",
                $"Publicar '{Nome}' exige justificativa (UNI-REQ-0050) — '{NumeroInscricao}' e '{NomeAbreviado}' dispensam.")));
        }

        if (erros.Count > 0)
        {
            return Result<ConfiguracaoDivulgacao>.ValidationFailure(erros);
        }

        string[] canonico = [.. camposPublicos.Distinct(StringComparer.Ordinal).OrderBy(static c => c, StringComparer.Ordinal)];

        return Result<ConfiguracaoDivulgacao>.Success(new ConfiguracaoDivulgacao
        {
            CamposPublicos = canonico,
            Justificativa = justificativaNormalizada,
        });
    }

    /// <summary>
    /// O default minimizado: só o piso, sem justificativa — o efetivo que um processo sem
    /// configuração explícita já divulga (D5 do plano de congelamento). Consumido pelo decoder do
    /// envelope para decidir se o bloco restaurado deve virar ausência em vez de entidade.
    /// </summary>
    public static bool EhDefaultMinimizado(IReadOnlyList<string> camposPublicos, string? justificativa)
    {
        ArgumentNullException.ThrowIfNull(camposPublicos);
        return justificativa is null && camposPublicos.Count == 1 && camposPublicos[0] == NumeroInscricao;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// <b>Trim + NFC</b> — mesmo raciocínio de <see cref="RascunhoRetificacao.NormalizarMotivo"/>:
    /// o valor guardado é o que o canonicalizador do envelope congelaria, e Postgres não normaliza
    /// texto. Uma justificativa só de espaços colapsa para <see langword="null"/> — não carrega
    /// informação a mais que a ausência.
    /// </summary>
    private static string? NormalizarJustificativa(string? justificativa) =>
        string.IsNullOrWhiteSpace(justificativa) ? null : HashCanonicalComputer.NormalizeNfc(justificativa.Trim());
}
