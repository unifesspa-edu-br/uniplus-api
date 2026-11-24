namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Valores de NaturezaLegalModalidade.</summary>
public enum NaturezaLegalModalidade
{
    [CodigoDeContrato("COTA_RESERVADA", "Cota reservada", Descricao = "Vagas reservadas pela Lei de Cotas.")]
    CotaReservada = 1,

    [CodigoDeContrato("AMPLA", "Ampla concorrência", Descricao = "Vagas destinadas à ampla concorrência.")]
    Ampla = 2,

    [CodigoDeContrato("ACAO_AFIRMATIVA", "Ação afirmativa", Descricao = "Vagas de ação afirmativa institucional.")]
    AcaoAfirmativa = 3,
}
