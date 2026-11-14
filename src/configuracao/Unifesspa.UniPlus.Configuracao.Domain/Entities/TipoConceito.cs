namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cadastro configurável dos tipos de conceito com que uma etapa avalia: numérico, medido numa escala,
/// ou categórico, escolhido entre valores ordenados que contam ou não como aprovação.
/// </summary>
/// <remarks>
/// O código é chave natural imutável e reservado para sempre: desativar tira o tipo de uso, mas não libera o
/// código. A natureza pode ser trocada na edição, que sempre traz os parâmetros da natureza escolhida e
/// descarta os da anterior; quem consome congela a própria cópia por valor, então a troca não alcança o que
/// já foi gravado.
/// </remarks>
public sealed class TipoConceito : EntityBase, IAuditableEntity
{
    private readonly List<ValorCategorico> _valores = [];

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public bool Ativo { get; private set; }
    public NaturezaConceito Natureza { get; private set; }

    /// <summary>Menor valor da escala; só no tipo numérico.</summary>
    public decimal? EscalaMinima { get; private set; }

    /// <summary>Maior valor da escala; só no tipo numérico.</summary>
    public decimal? EscalaMaxima { get; private set; }

    /// <summary>Casas decimais da escala; só no tipo numérico.</summary>
    public int? CasasDecimais { get; private set; }

    /// <summary>Os valores do tipo categórico, na ordem em que foram informados.</summary>
    public IReadOnlyList<ValorCategorico> Valores => [.. _valores.OrderBy(static valor => valor.Posicao)];

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoConceito()
    {
    }

    /// <summary>
    /// Cria o tipo, acumulando no mesmo lote as violações do código e da definição. A unicidade do
    /// código é responsabilidade do handler.
    /// </summary>
    public static Result<TipoConceito> Criar(string? codigo, DefinicaoInformada definicao)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        List<FieldError> erros = [];
        string? codigoNormalizado = CamposDoTipoConceito.Codigo.Obrigatorio(
            codigo, "codigo", TipoConceitoErrorCodes.CodigoObrigatorio, erros);
        Result<DefinicaoValidada> validada = definicao.Validar();
        erros.AddRange(validada.Errors);
        if (erros.Count > 0)
        {
            return Result<TipoConceito>.ValidationFailure(erros);
        }

        TipoConceito tipo = new() { Codigo = codigoNormalizado!, Ativo = true };
        tipo.Aplicar(validada.Value!);
        return Result<TipoConceito>.Success(tipo);
    }

    /// <summary>Aplica uma definição já validada; o código, o id e a situação não mudam.</summary>
    public void Atualizar(DefinicaoValidada definicao)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        Aplicar(definicao);
    }

    /// <summary>Desativa o tipo. Desativar o que já está desativado não muda nada.</summary>
    public void Desativar() => Ativo = false;

    private void Aplicar(DefinicaoValidada definicao)
    {
        Nome = definicao.Nome;
        Descricao = definicao.Descricao;
        Natureza = definicao.Natureza;
        EscalaMinima = definicao.Escala?.Minima;
        EscalaMaxima = definicao.Escala?.Maxima;
        CasasDecimais = definicao.Escala?.CasasDecimais;
        _valores.Clear();
        _valores.AddRange(definicao.Valores);
    }
}
