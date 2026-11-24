namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Valores de ComposicaoVagasModalidade.</summary>
public enum ComposicaoVagasModalidade
{
    [CodigoDeContrato("RESTANTE_DAS_VAGAS_OFERTADAS", "Restante das vagas ofertadas", Descricao = "Vagas restantes após as reservas.")]
    RestanteDasVagasOfertadas = 1,

    [CodigoDeContrato("DENTRO_DA_RESERVA_LEGAL", "Dentro da reserva legal", Descricao = "Vagas incluídas na reserva legal.")]
    DentroDaReservaLegal = 2,

    [CodigoDeContrato("RETIRADA_DE_OUTRA_MODALIDADE", "Retirada de outra modalidade", Descricao = "Vagas retiradas de outra modalidade.")]
    RetiradaDeOutraModalidade = 3,

    [CodigoDeContrato("SUPLEMENTAR_AO_TOTAL", "Suplementar ao total", Descricao = "Vagas adicionais ao total ofertado.")]
    SuplementarAoTotal = 4,
}
