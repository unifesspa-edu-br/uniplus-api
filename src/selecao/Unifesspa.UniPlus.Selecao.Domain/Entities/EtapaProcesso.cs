namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Etapa <em>pontuada</em> do <see cref="ProcessoSeletivo"/> (peso, caráter e
/// nota mínima) — distinta de fase do cronograma, que é o eixo temporal do
/// certame. Entidade interna do agregado: criada, substituída e persistida
/// exclusivamente pela raiz.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete) de propósito:
/// a configuração em rascunho é substituível por inteiro (comandos
/// <c>Definir*</c>) e a trilha auditável do que valeu em cada publicação é o
/// snapshot RN08 da Story #759 — não faz sentido acumular linhas logicamente
/// excluídas de rascunho. A <c>Etapa</c> ligada a <c>Edital</c> continua
/// existindo para a conformidade da Story #460 e é aposentada junto com o
/// CRUD de Edital na #759.
/// </remarks>
public sealed class EtapaProcesso : EntityBase
{
    /// <summary>Alinhado a <c>EtapaProcessoConfiguration</c> (varchar(300)).</summary>
    public const int NomeMaxLength = 300;

    public Guid ProcessoSeletivoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public CaraterEtapa Carater { get; private set; }

    /// <summary>
    /// Cópia por valor do tipo de etapa resolvido em Configuração (ADR-0061). Fonte
    /// estável de identidade para avaliação de conformidade legal — o rótulo editorial
    /// (<see cref="Nome"/>) pode divergir do nome do tipo e não participa da avaliação.
    /// </summary>
    public TipoEtapaSnapshot TipoEtapa { get; private set; } = null!;

    public Guid TipoEtapaOrigemId => TipoEtapa.OrigemId;
    public decimal? Peso { get; private set; }
    public decimal? NotaMinima { get; private set; }
    public int? Ordem { get; private set; }

    private EtapaProcesso() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125). Precisão/
    /// escala de <see cref="Peso"/>/<see cref="NotaMinima"/> (<c>numeric(18,4)</c>) permanecem
    /// só no validator — não há ainda um helper de domínio para limite decimal (achado B2 do
    /// mapeamento da rolagem), então checar aqui duplicaria uma regra de forma de wire/coluna
    /// sem trazer nenhum ganho de correção.
    /// </summary>
    public static Result<EtapaProcesso> Criar(
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso = null,
        decimal? notaMinima = null,
        int? ordem = null)
    {
        ArgumentNullException.ThrowIfNull(tipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(nome, carater, peso, notaMinima, ordem);
        if (erros.Count > 0)
        {
            return Result<EtapaProcesso>.ValidationFailure(erros);
        }

        return Result<EtapaProcesso>.Success(new EtapaProcesso
        {
            Nome = nome.Trim(),
            Carater = carater,
            TipoEtapa = tipoEtapa,
            Peso = peso,
            NotaMinima = notaMinima,
            Ordem = ordem,
        });
    }

    /// <summary>
    /// Nome/Carater/Peso/NotaMinima/Ordem não dependem de nenhum cadastro cross-módulo —
    /// diferente de <see cref="TipoEtapa"/> (resolvido à parte, contra o cadastro vivo de
    /// Configuração, por quem chama). Compartilhada entre <see cref="Criar"/> e
    /// <see cref="AtualizarDados"/> (mesmas checagens) e exposta para o handler poder
    /// confirmar a forma de TODAS as etapas do payload numa primeira passada, antes de
    /// resolver o tipo de etapa no cadastro (mesmo padrão de
    /// <c>FatoColetado.ValidarFormaBasica</c>, PR #1214).
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(
        string? nome, CaraterEtapa carater, decimal? peso, decimal? notaMinima, int? ordem)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                "EtapaProcesso.NomeObrigatorio", "O nome da etapa é obrigatório.")));
        }
        else if (nome.Trim().Length > NomeMaxLength)
        {
            erros.Add(new("nome", new DomainError(
                "EtapaProcesso.NomeTamanho", $"O nome da etapa deve ter no máximo {NomeMaxLength} caracteres.")));
        }

        if (carater == CaraterEtapa.Nenhum)
        {
            erros.Add(new("carater", new DomainError(
                "EtapaProcesso.CaraterObrigatorio",
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).")));
        }

        if (peso is <= 0)
        {
            erros.Add(new("peso", new DomainError(
                "EtapaProcesso.PesoInvalido", "O peso da etapa, quando informado, deve ser maior que zero.")));
        }

        if (notaMinima is < 0)
        {
            erros.Add(new("notaMinima", new DomainError(
                "EtapaProcesso.NotaMinimaInvalida", "A nota mínima, quando informada, não pode ser negativa.")));
        }

        if (ordem is <= 0)
        {
            erros.Add(new("ordem", new DomainError(
                "EtapaProcesso.OrdemInvalida", "A ordem da etapa, quando informada, deve ser maior que zero.")));
        }

        return erros;
    }

    /// <summary>
    /// Reidrata uma etapa a partir de uma <see cref="VersaoConfiguracao"/> congelada,
    /// <b>preservando o <see cref="EntityBase.Id"/></b> — que <see cref="Criar"/> não
    /// aceita, por decisão (a identidade de uma etapa nova é do sistema, não do cliente).
    /// </summary>
    /// <remarks>
    /// <para>
    /// O <c>id</c> da etapa é o <b>único</b> id de entidade-filha que o envelope congela
    /// (ADR-0110 D2), porque <c>criteriosDesempate.args.etapaRef</c> e
    /// <c>regrasEliminacao.args.etapaRef</c> apontam para ele: sem preservá-lo, o
    /// snapshot reidratado teria referências que não resolvem, e o desempate e a
    /// eliminação do certame ficariam inexecutáveis. Os ids das demais filhas são
    /// regenerados — nenhuma referência de negócio exige estabilidade deles, e as FKs
    /// internas são reconstruídas junto com o grafo.
    /// </para>
    /// <para>
    /// Reidratar não é criar: os dados vêm de um documento com peso jurídico, já
    /// validado quando foi congelado. As guardas aqui são a última linha contra erro de
    /// programação — o decoder do envelope é quem recusa bytes inválidos, e o faz com
    /// <c>Result</c>, nunca com exceção.
    /// </para>
    /// </remarks>
    public static EtapaProcesso Reidratar(
        Guid id,
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso,
        decimal? notaMinima,
        int? ordem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentNullException.ThrowIfNull(tipoEtapa);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A etapa reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).",
                nameof(carater));
        }

        return new EtapaProcesso
        {
            Id = id,
            Nome = nome.Trim(),
            Carater = carater,
            TipoEtapa = tipoEtapa,
            Peso = peso,
            NotaMinima = notaMinima,
            Ordem = ordem,
        };
    }

    /// <summary>
    /// Uma etapa compõe a nota final quando o seu caráter pontua
    /// (classificatória ou ambas) e ela declara peso — é o critério que a
    /// inclui no divisor da média (<see cref="ProcessoSeletivo.CalcularDivisorMedia"/>).
    /// </summary>
    public bool ComponeNota => Carater is CaraterEtapa.Classificatoria or CaraterEtapa.Ambas && Peso.HasValue;

    /// <summary>
    /// Atualiza os dados da MESMA etapa (mesmo <see cref="EntityBase.Id"/>) em
    /// vez de recriá-la — permite que <c>DefinirEtapasCommandHandler</c>
    /// reconcilie o payload de <c>PUT /etapas</c> com o agregado tracked
    /// preservando a identidade de uma etapa já referenciada por critério de
    /// desempate ou regra de eliminação da classificação (sem isso, qualquer
    /// reconfiguração de etapas quebraria essas referências por construção).
    /// </summary>
    public Result AtualizarDados(
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso,
        decimal? notaMinima,
        int? ordem)
    {
        ArgumentNullException.ThrowIfNull(tipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(nome, carater, peso, notaMinima, ordem);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Nome = nome.Trim();
        Carater = carater;
        TipoEtapa = tipoEtapa;
        Peso = peso;
        NotaMinima = notaMinima;
        Ordem = ordem;

        return Result.Success();
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
