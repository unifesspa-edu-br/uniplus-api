namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Linq.Expressions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Motivo que o processo escolheu do cadastro para a decisão de uma fase ou de uma etapa (UNI-REQ-0285): cópia por
/// valor (ADR-0061) do código, da descrição, do sentido e da subclassificação. Mudar o cadastro depois não muda o
/// processo, e o motivo desativado continua utilizável onde já consta do edital publicado (UNI-REQ-0122).
/// </summary>
/// <remarks>
/// A escolha nasce já com a versão do edital em que entra: a primeira, porque só o rascunho escolhe. Congelar a versão
/// não grava nada nela e não refaz a cópia: o texto publicado é o da escolha, mesmo que o cadastro mude depois.
/// A regra de quando incluir e retirar é de <see cref="EscolhaDosMotivos"/>, que enxerga o estado do processo.
/// </remarks>
public sealed class MotivoDeDecisaoDoProcesso : EntityBase, IAuditableEntity
{
    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Id do motivo no cadastro, sem chave estrangeira: a cópia não depende da linha viva.</summary>
    public Guid MotivoDeDecisaoOrigemId { get; private set; }

    public TipoMotivoDeDecisao Tipo { get; private set; }

    public CodigoMotivoDecisao Codigo { get; private set; } = null!;

    public string Descricao { get; private set; } = string.Empty;

    public string? Subclassificacao { get; private set; }

    public SentidoDoMotivo Sentido { get; private set; }

    /// <summary>A fase ou a etapa em que o motivo é escolhido: o mesmo motivo pode ser escolhido em mais de um lugar.</summary>
    public LugarDaDecisao Lugar { get; private set; } = null!;

    /// <summary>As versões do edital em que a escolha vale.</summary>
    public VigenciaNoEdital Vigencia { get; private set; } = null!;

    /// <summary>
    /// Por que o motivo entrou depois da publicação (UNI-REQ-0124). Uso interno da administração:
    /// não é apresentada ao candidato. Nula na escolha anterior à publicação.
    /// </summary>
    public string? JustificativaDaInclusao { get; private set; }

    public string? CreatedBy { get; private set; }

    public string? UpdatedBy { get; private set; }

    private MotivoDeDecisaoDoProcesso()
    {
    }

    internal static MotivoDeDecisaoDoProcesso Copiar(
        Guid processoSeletivoId, LugarDaDecisao lugar, MotivoDeDecisao origem, int versaoDeEntrada, string? justificativa)
    {
        return new()
        {
            ProcessoSeletivoId = processoSeletivoId,
            Lugar = lugar,
            Vigencia = VigenciaNoEdital.APartirDa(versaoDeEntrada),
            MotivoDeDecisaoOrigemId = origem.Id,
            Tipo = origem.Tipo,
            Codigo = origem.Codigo,
            Descricao = origem.Descricao,
            Subclassificacao = origem.Subclassificacao,
            Sentido = origem.Sentido,
            JustificativaDaInclusao = justificativa,
        };
    }

    /// <summary>
    /// Se a escolha ainda não consta de edital publicado, estando <paramref name="numeroDaVersaoEmPreparo"/> em preparo: a
    /// que entra nessa versão ou depois dela ainda não consta; a que entrou antes já consta.
    /// </summary>
    public bool PendenteNa(int numeroDaVersaoEmPreparo) => Vigencia.VersaoDoEditalDeEntrada >= numeroDaVersaoEmPreparo;

    /// <summary>As escolhas que valem na versão do edital: a que entrou nela ou antes, e que não saiu até ela.</summary>
    public static Expression<Func<MotivoDeDecisaoDoProcesso, bool>> ValeNaVersao(int numeroDaVersao) =>
        m => m.Vigencia.VersaoDoEditalDeEntrada <= numeroDaVersao
            && (m.Vigencia.VersaoDoEditalDeSaida == null || m.Vigencia.VersaoDoEditalDeSaida > numeroDaVersao);

    /// <summary>As escolhas atuais: as que ainda não saíram do edital.</summary>
    public static Expression<Func<MotivoDeDecisaoDoProcesso, bool>> NaEscolhaAtual =>
        m => m.Vigencia.VersaoDoEditalDeSaida == null;
}
