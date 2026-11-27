namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// A declaração do Fator de Atenuação das Desigualdades de Acesso (FADA) como o documento a traz: se o processo o aplica, a regra, o
/// multiplicador, o teto, o dado do candidato que dá a elegibilidade e a base legal pelo código. A identificação, a descrição e os
/// municípios da base legal são só de leitura e não entram.
/// </summary>
public sealed record FadaNoDocumento(
    bool Aplica,
    ReferenciaDeRegraNoDocumento? Regra,
    decimal? Fator,
    decimal? Teto,
    string? CodigoDoDadoDeElegibilidade,
    string? BaseLegalCodigo);

/// <summary>O atendimento especializado como o documento o traz: as condições, os recursos de acessibilidade e os tipos de deficiência, cada um pelo código e com a posição que tinha na lista enviada.</summary>
public sealed record AtendimentoNoDocumento(
    IReadOnlyList<CodigoNoDocumento> Condicoes,
    IReadOnlyList<CodigoNoDocumento> Recursos,
    IReadOnlyList<CodigoNoDocumento> TiposDeficiencia);

/// <summary>O código de cadastro de um item de lista como foi escrito, com a posição do item na lista enviada.</summary>
public sealed record CodigoNoDocumento(int PosicaoNoDocumento, string? Codigo);
