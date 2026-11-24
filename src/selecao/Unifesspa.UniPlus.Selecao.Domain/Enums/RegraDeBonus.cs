namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Regras reconhecidas pelo catálogo.</summary>
public enum RegraDeBonus
{
    [CodigoDeContrato("FADA_MULTIPLICATIVO", "Multiplica a nota final pelo Fator de Atenuação das Desigualdades de Acesso (FADA)",
        Descricao = "Multiplica a nota final pelo Fator de Atenuação das Desigualdades de Acesso (FADA) após a aplicação dos pesos.")]
    Multiplicativo = 1,
}
