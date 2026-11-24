namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum TratamentoDaFaltaDeApuracao
{
    [CodigoDeContrato("NOVO_CORRETOR", "Novo corretor", Descricao = "Novo corretor.")]
    NovoCorretor = 1,

    [CodigoDeContrato("DECISAO_DA_BANCA", "Decisão da banca", Descricao = "Decisão da banca.")]
    DecisaoDaBanca = 2,
}
