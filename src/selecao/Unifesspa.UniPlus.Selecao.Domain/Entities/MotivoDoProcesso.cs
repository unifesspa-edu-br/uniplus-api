namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Linq.Expressions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Motivo que o processo escolheu do cadastro para a decisão de uma fase ou de uma etapa (UNI-REQ-0285): cópia por
/// valor (ADR-0061) do código, da descrição, do sentido e da subclassificação. Mudar o cadastro depois não muda o
/// processo, e o motivo desativado continua utilizável onde já consta do edital publicado (UNI-REQ-0122).
/// </summary>
/// <remarks>
/// A escolha nasce já com a versão do edital em que entra: a primeira, no rascunho, ou a que a sessão de retificação
/// prepara. Congelar a versão não grava nada nela; a descrição se refaz do cadastro só na versão em que a escolha entra.
/// A regra de quando incluir e retirar é de <see cref="EscolhaDosMotivos"/>, que enxerga o estado do processo.
/// </remarks>
public sealed class MotivoDoProcesso : EntityBase, IAuditableEntity
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

    private MotivoDoProcesso()
    {
    }

    internal static MotivoDoProcesso Copiar(
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

    /// <summary>Refaz a cópia da descrição vigente no cadastro; quem chama só recopia a escolha que entra na versão.</summary>
    internal void Recopiar(MotivoDeDecisao origem) => Descricao = origem.Descricao;

    /// <summary>
    /// Se a escolha ainda é só da sessão, estando <paramref name="numeroDaVersaoEmPreparo"/> em preparo: a que entra nessa
    /// versão (ou depois dela, se sobrou de uma sessão descartada) ainda não consta de edital publicado; a que entrou antes
    /// já consta.
    /// </summary>
    public bool PendenteNa(int numeroDaVersaoEmPreparo) => Vigencia.VersaoDoEditalDeEntrada >= numeroDaVersaoEmPreparo;

    /// <summary>As escolhas que valem na versão do edital: a que entrou nela ou antes, e que não saiu até ela.</summary>
    public static Expression<Func<MotivoDoProcesso, bool>> ValeNaVersao(int numeroDaVersao) =>
        m => m.Vigencia.VersaoDoEditalDeEntrada <= numeroDaVersao
            && (m.Vigencia.VersaoDoEditalDeSaida == null || m.Vigencia.VersaoDoEditalDeSaida > numeroDaVersao);

    /// <summary>As escolhas atuais: as que ainda não saíram do edital.</summary>
    public static Expression<Func<MotivoDoProcesso, bool>> NaEscolhaAtual =>
        m => m.Vigencia.VersaoDoEditalDeSaida == null;
}
