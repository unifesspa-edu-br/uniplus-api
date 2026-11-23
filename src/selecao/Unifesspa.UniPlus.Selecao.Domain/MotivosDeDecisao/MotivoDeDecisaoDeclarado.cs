namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Cópia dos dados de uma escolha que compõem a versão do processo.</summary>
public sealed record MotivoDeDecisaoDeclarado
{
    public required TipoMotivoDeDecisao Tipo { get; init; }
    public required string Codigo { get; init; }
    public required LugarDaDecisao Lugar { get; init; }
    public required string Descricao { get; init; }
    public string? Subclassificacao { get; init; }
    public required SentidoDoMotivo Sentido { get; init; }
    public required int VersaoDeEntrada { get; init; }
    public string? JustificativaDaInclusao { get; init; }

    public static MotivoDeDecisaoDeclarado DaEscolha(MotivoDeDecisaoDoProcesso escolha)
    {
        ArgumentNullException.ThrowIfNull(escolha);
        return Copiar(escolha);
    }

    private static MotivoDeDecisaoDeclarado Copiar(MotivoDeDecisaoDoProcesso escolha) => new()
    {
        Tipo = escolha.Tipo,
        Codigo = escolha.Codigo.Valor,
        Lugar = escolha.Lugar,
        Descricao = escolha.Descricao,
        Subclassificacao = escolha.Subclassificacao,
        Sentido = escolha.Sentido,
        VersaoDeEntrada = escolha.Vigencia.VersaoDoEditalDeEntrada,
        JustificativaDaInclusao = escolha.JustificativaDaInclusao,
    };
}
