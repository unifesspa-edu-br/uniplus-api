namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Forma de composição das vagas de uma <see cref="Entities.Modalidade"/> de
/// concorrência (UNI-REQ-0011): descreve, em domínio fechado, como as vagas da
/// modalidade se relacionam com o total do processo (residuais das vagas ofertadas base, dentro da
/// reserva legal, retiradas de outra modalidade ou suplementares ao total). A ordem de declaração é a de exibição.
/// </summary>
public enum ComposicaoVagas
{
    /// <summary>Vagas que restam das vagas ofertadas base após as reservas.</summary>
    [CodigoDeContrato("RESTANTE_DAS_VAGAS_OFERTADAS", "Restante das vagas ofertadas")]
    RestanteDasVagasOfertadas = 1,

    /// <summary>Vagas contidas dentro da reserva legal.</summary>
    [CodigoDeContrato("DENTRO_DA_RESERVA_LEGAL", "Dentro da reserva legal")]
    DentroDaReservaLegal = 2,

    /// <summary>Vagas retiradas de outra modalidade (exige <c>ComposicaoOrigem</c>).</summary>
    [CodigoDeContrato("RETIRADA_DE_OUTRA_MODALIDADE", "Retirada de outra modalidade")]
    RetiradaDeOutraModalidade = 3,

    /// <summary>Vagas suplementares, acrescidas ao total do processo.</summary>
    [CodigoDeContrato("SUPLEMENTAR_AO_TOTAL", "Suplementar ao total")]
    SuplementarAoTotal = 4,
}
