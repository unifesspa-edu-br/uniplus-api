namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

/// <summary>
/// Um valor do tipo de conceito categórico como chega no corpo da requisição. Se conta como aprovação é
/// obrigatório, mas anulável aqui: o valor ausente chega à validação de domínio e volta como erro do campo
/// do valor, em vez de virar <c>false</c> em silêncio — o valor nasceria sem aprovar — ou de derrubar a
/// leitura do corpo.
/// </summary>
public sealed record ValorCategoricoCommand(
    string? Codigo,
    string? Rotulo,
    bool? ContaComoAprovacao);
