namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Forma de composição das vagas de uma <see cref="Entities.Modalidade"/> de
/// concorrência (UNI-REQ-0011): descreve, em domínio fechado, como as vagas da
/// modalidade se relacionam com o total do processo (residuais das vagas ofertadas base, dentro da
/// reserva legal, retiradas de outra modalidade ou suplementares ao total). Persistida como
/// token UPPER_SNAKE (<see cref="ComposicoesVagas"/>).
/// </summary>
public enum ComposicaoVagas
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Vagas que restam das vagas ofertadas base após as reservas.</summary>
    RestanteDasVagasOfertadas = 1,

    /// <summary>Vagas contidas dentro da reserva legal.</summary>
    DentroDaReservaLegal = 2,

    /// <summary>Vagas retiradas de outra modalidade (exige <c>ComposicaoOrigem</c>).</summary>
    RetiradaDeOutraModalidade = 3,

    /// <summary>Vagas suplementares, acrescidas ao total do processo.</summary>
    SuplementarAoTotal = 4,
}
