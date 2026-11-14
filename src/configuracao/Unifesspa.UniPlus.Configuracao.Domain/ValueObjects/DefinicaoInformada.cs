namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o operador declara de um tipo de conceito, ainda não validado: tudo o que a gravação aceita
/// além do código, que é imutável e só entra na criação.
/// </summary>
/// <param name="Nome">Nome informado.</param>
/// <param name="Descricao">Descrição informada, opcional.</param>
/// <param name="Natureza">Natureza como no contrato REST (<c>numerico</c> ou <c>categorico</c>).</param>
/// <param name="Numerico">Escala do tipo numérico; deve ser <see langword="null"/> no categórico.</param>
/// <param name="Valores">Valores do tipo categórico, em ordem; devem ser <see langword="null"/> no numérico.</param>
public sealed record DefinicaoInformada(
    string? Nome,
    string? Descricao,
    string? Natureza,
    EscalaInformada? Numerico,
    IReadOnlyList<ValorInformado>? Valores)
{
    /// <summary>
    /// Confere tudo sem I/O e sem mutar nada, acumulando as violações independentes: o numérico exige a
    /// escala e recusa valores, e o categórico exige os valores e recusa a escala.
    /// </summary>
    public Result<DefinicaoValidada> Validar()
    {
        List<FieldError> erros = [];
        string? nome = CamposDoTipoConceito.Nome.Obrigatorio(Nome, "nome", TipoConceitoErrorCodes.NomeObrigatorio, erros);
        string? descricao = CamposDoTipoConceito.Descricao.Opcional(Descricao, "descricao", erros);
        NaturezaConceito natureza = ConferirNatureza(erros);
        ConferirParametrosIncompativeis(natureza, erros);
        EscalaNumerica? escala = natureza == NaturezaConceito.Numerico ? EscalaNumerica.Conferir(Numerico, erros) : null;
        IReadOnlyList<ValorCategorico>? valores = natureza == NaturezaConceito.Categorico ? ValoresCategoricos.Conferir(Valores, erros) : null;

        return erros.Count > 0
            ? Result<DefinicaoValidada>.ValidationFailure(erros)
            : Result<DefinicaoValidada>.Success(new DefinicaoValidada(nome!, descricao, natureza, escala, valores ?? []));
    }

    private NaturezaConceito ConferirNatureza(List<FieldError> erros)
    {
        if (NaturezasConceito.TryAnalisarContrato(Natureza, out NaturezaConceito natureza))
        {
            return natureza;
        }

        erros.Add(new("natureza", new DomainError(
            TipoConceitoErrorCodes.NaturezaInvalida, "A natureza deve ser numerico ou categorico.")));
        return NaturezaConceito.Nenhuma;
    }

    private void ConferirParametrosIncompativeis(NaturezaConceito natureza, List<FieldError> erros)
    {
        if (natureza == NaturezaConceito.Numerico && Valores is not null)
        {
            erros.Add(Incompativel("valores", "O tipo de conceito numérico não admite valores categóricos."));
        }

        if (natureza == NaturezaConceito.Categorico && Numerico is not null)
        {
            erros.Add(Incompativel("numerico", "O tipo de conceito categórico não admite escala numérica."));
        }
    }

    private static FieldError Incompativel(string campo, string mensagem) =>
        new(campo, new DomainError(TipoConceitoErrorCodes.ParametrosIncompativeisComANatureza, mensagem));
}
