namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um valor que um <see cref="TipoConceito"/> categórico admite. A posição é o lugar do valor na lista
/// informada e a única expressão da ordem; os valores de um tipo são sempre substituídos em bloco.
/// </summary>
public sealed class ValorCategorico
{
    public const int CodigoDoValorMaxLength = 50;

    public string Codigo { get; private set; } = string.Empty;
    public string Rotulo { get; private set; } = string.Empty;
    public bool ContaComoAprovacao { get; private set; }

    /// <summary>Posição do valor na lista, a partir de 1.</summary>
    public int Posicao { get; private set; }

    // EF Core materialization
    private ValorCategorico()
    {
    }

    private ValorCategorico(string codigo, string rotulo, bool contaComoAprovacao, int posicao)
    {
        Codigo = codigo;
        Rotulo = rotulo;
        ContaComoAprovacao = contaComoAprovacao;
        Posicao = posicao;
    }

    /// <summary>O valor válido, ou <see langword="null"/> com as violações acumuladas em <paramref name="erros"/>.</summary>
    internal static ValorCategorico? Conferir(ValorInformado informado, int indice, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(informado);
        ArgumentNullException.ThrowIfNull(erros);
        string campo = $"valores[{indice}]";
        string? codigo = ConferirCodigo(informado.Codigo, $"{campo}.codigo", erros);
        string? rotulo = CamposDoTipoConceito.RotuloDoValor.Obrigatorio(
            informado.Rotulo, $"{campo}.rotulo", TipoConceitoErrorCodes.ValorCategoricoRotuloInvalido, erros);

        bool? aprovacao = ConferirAprovacao(informado.ContaComoAprovacao, $"{campo}.contaComoAprovacao", erros);

        return codigo is null || rotulo is null || aprovacao is null
            ? null
            : new ValorCategorico(codigo, rotulo, aprovacao.Value, indice + 1);
    }

    private static bool? ConferirAprovacao(bool? informada, string caminho, List<FieldError> erros)
    {
        if (informada is null)
        {
            erros.Add(new(caminho, new DomainError(
                TipoConceitoErrorCodes.ValorCategoricoContaComoAprovacaoObrigatoria,
                "Informe se o valor conta como aprovação.")));
        }

        return informada;
    }

    private static string? ConferirCodigo(string? informado, string caminho, List<FieldError> erros)
    {
        if (informado is not null && informado.Length <= CodigoDoValorMaxLength
            && FormatoDoCodigo.TemFormatoValido(informado))
        {
            return informado;
        }

        erros.Add(new(caminho, new DomainError(
            TipoConceitoErrorCodes.ValorCategoricoCodigoInvalido,
            "O código do valor categórico deve começar com letra maiúscula e conter só letras maiúsculas, dígitos e sublinhado.")));
        return null;
    }

}
