namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// A forma de um item do formulário, a mesma no formulário do processo e no modelo: o código do
/// fato, a ordem, o rótulo, o tipo de campo, a ajuda, o formato e as restrições de valor.
/// Acumula toda violação independente em vez de parar na primeira (ADR-0125).
/// </summary>
public static class FormaDoItem
{
    public const int FatoCodigoMaxLength = FatoDoCatalogo.CodigoMaxLength;

    /// <summary>A mesma grandeza de um nome de cadastro curto; o envelope aplica o mesmo limite.</summary>
    public const int RotuloMaxLength = 300;

    public const int FormatoMaxLength = 30;

    /// <summary>A mesma grandeza do formato — token canônico curto (o maior hoje é "IDENTIFICADOR").</summary>
    public const int ClassificacaoProtecaoMaxLength = 30;

    /// <summary>O token canônico da hipótese legal de tratamento copiada do catálogo.</summary>
    public const int HipoteseLegalMaxLength = 40;

    /// <summary>A finalidade do tratamento copiada do catálogo.</summary>
    public const int FinalidadeTratamentoMaxLength = 500;

    /// <summary>A descrição do fato, copiada do catálogo, que explica o dado a quem analisa a inscrição.</summary>
    public const int DescricaoDoFatoMaxLength = 1000;

    /// <summary>O token canônico do componente de exibição copiado do catálogo.</summary>
    public const int ComponenteExibicaoMaxLength = 20;

    public const int AjudaMaxLength = 1000;

    /// <summary>A mensagem que explica ao candidato por que a resposta impede a inscrição.</summary>
    public const int MensagemDoImpedimentoMaxLength = 500;

    /// <summary>As casas decimais dos limites da faixa numérica, as mesmas com que o edital os congela.</summary>
    public const int CasasDecimaisDaFaixa = 4;

    /// <summary>
    /// Teto de itens de um formulário. O maior formulário do edital de Medicina 2027 tem algumas
    /// dezenas de campos; o teto dá folga larga sem deixar uma entrada pequena gerar resposta
    /// desproporcional, porque acima dele a lista é recusada inteira, sem um erro por item.
    /// </summary>
    public const int MaximoDeItens = 200;

    /// <summary>
    /// O que o teto do formulário conta: cada item, cada grupo e cada campo de grupo, dados os
    /// itens e a quantidade de campos de cada grupo.
    /// </summary>
    public static int QuantidadeNoTeto(int itens, IEnumerable<int> camposPorGrupo)
    {
        ArgumentNullException.ThrowIfNull(camposPorGrupo);
        return itens + camposPorGrupo.Sum(static campos => 1 + campos);
    }

    /// <summary>
    /// A quantidade de itens não depende do catálogo: existe separada para quem recebe a lista
    /// recusá-la acima do teto antes de ler o catálogo e de conferir item a item.
    /// </summary>
    public static List<FieldError> ValidarQuantidade(int quantidade)
    {
        List<FieldError> erros = [];
        if (quantidade > MaximoDeItens)
        {
            erros.Add(new("itens", new DomainError(
                ItemFormularioErrorCodes.ItensEmExcesso, $"O formulário admite no máximo {MaximoDeItens} itens; vieram {quantidade}.")));
        }

        return erros;
    }

    /// <summary>
    /// Os campos que não dependem do catálogo nem das regras — código do fato, ordem e rótulo: existe separada para quem
    /// recebe a lista inteira conferir a forma de todos os itens antes de resolver o catálogo, e
    /// assim um código vazio não cai num "fato desconhecido", menos específico.
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? fatoCodigo, int ordem, string? rotulo) =>
        [.. RecusasDoCodigoDoFato(fatoCodigo), .. RecusasDaOrdem(ordem), .. RecusasDoRotulo(rotulo)];

    private static IEnumerable<FieldError> RecusasDoCodigoDoFato(string? fatoCodigo)
    {
        if (string.IsNullOrWhiteSpace(fatoCodigo))
        {
            yield return new("fatoCodigo", new DomainError(ItemFormularioErrorCodes.FatoCodigoObrigatorio, "O código do fato do item é obrigatório."));
        }
        else if (fatoCodigo.Length > FatoCodigoMaxLength)
        {
            yield return new("fatoCodigo", new DomainError(
                ItemFormularioErrorCodes.FatoCodigoTamanho, $"O código do fato do item deve ter no máximo {FatoCodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(ItemFormularioErrorCodes.OrdemInvalida, "A ordem do item não pode ser negativa."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoRotulo(string? rotulo)
    {
        if (string.IsNullOrWhiteSpace(rotulo))
        {
            yield return new($"{FatoColetadoInput.CaminhoDaApresentacao}.rotulo", new DomainError(ItemFormularioErrorCodes.RotuloObrigatorio, "O rótulo do item é obrigatório."));
        }
        else if (rotulo.Trim().Length > RotuloMaxLength)
        {
            yield return new($"{FatoColetadoInput.CaminhoDaApresentacao}.rotulo", new DomainError(
                ItemFormularioErrorCodes.RotuloTamanho, $"O rótulo do item deve ter no máximo {RotuloMaxLength} caracteres."));
        }
    }

    /// <summary>
    /// A forma inteira do item: a básica, a ajuda, o formato (só o campo de texto tem), nenhuma
    /// regra citando o próprio fato e as restrições cabendo no campo. A autorreferência é ciclo de
    /// comprimento um, recusada aqui com um erro específico, sem precisar conhecer os outros itens.
    /// O impedimento é a exceção: cada cláusula dele cita a resposta do próprio campo.
    /// </summary>
    public static List<FieldError> Conferir(string? fatoCodigo, int ordem, ApresentacaoDoCampo apresentacao, string? formato, RegrasDoCampo regras)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        ArgumentNullException.ThrowIfNull(regras);

        return
        [
            .. ValidarFormaBasica(fatoCodigo, ordem, apresentacao.Rotulo),
            .. RecusasDaAjuda(apresentacao.Ajuda).Select(static erro => erro.NoCaminhoDe(FatoColetadoInput.CaminhoDaApresentacao)),
            .. RecusasDoFormato(apresentacao.TipoDeCampo, formato),
            .. RegrasLidasDoCampo.De(regras).ConferirNoCampo(fatoCodigo ?? string.Empty, apresentacao.TipoDeCampo)
                .Select(static erro => erro.NoCaminhoDe(FatoColetadoInput.CaminhoDasRegras)),
        ];
    }

    /// <summary>A ajuda, quando há, cabe no limite. O rótulo é conferido com o código e a ordem do item.</summary>
    public static IEnumerable<FieldError> RecusasDaAjuda(string? ajuda)
    {
        if (TextoOpcional(ajuda) is { Length: > AjudaMaxLength })
        {
            yield return new("ajuda", new DomainError(
                ItemFormularioErrorCodes.AjudaTamanho, $"A ajuda do campo tem no máximo {AjudaMaxLength} caracteres."));
        }
    }

    internal static IEnumerable<FieldError> RecusasDoFormato(TipoDeCampo tipoDeCampo, string? formato)
    {
        string? formatoNormalizado = TextoOpcional(formato);
        if ((tipoDeCampo == TipoDeCampo.Texto) != (formatoNormalizado is not null)
            || formatoNormalizado is { Length: > FormatoMaxLength })
        {
            yield return new("formato", new DomainError(
                ItemFormularioErrorCodes.FormatoIncoerente,
                $"O campo de texto tem o formato do fato no catálogo, com no máximo {FormatoMaxLength} caracteres; os demais campos não têm formato."));
        }
    }

    /// <summary>Se o tipo de restrição se aplica ao tipo de campo.</summary>
    public static bool RestricaoCabeNoCampo(TipoRestricaoValor restricao, TipoDeCampo campo) =>
        restricao switch
        {
            TipoRestricaoValor.FaixaNumerica => campo == TipoDeCampo.Numero,
            TipoRestricaoValor.TamanhoTexto => campo == TipoDeCampo.Texto,
            TipoRestricaoValor.OpcoesPermitidas or TipoRestricaoValor.OpcoesDasRespostas => campo.EhEscolha(),
            TipoRestricaoValor.MunicipiosDaUf => campo == TipoDeCampo.Municipio,
            TipoRestricaoValor.FaixaDeData => campo == TipoDeCampo.Data,
            _ => throw new ArgumentOutOfRangeException(nameof(restricao), restricao, "Tipo de restrição desconhecido."),
        };

    /// <summary>O texto aparado, ou nulo quando vazio — a forma em que o item guarda ajuda e formato.</summary>
    public static string? TextoOpcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
