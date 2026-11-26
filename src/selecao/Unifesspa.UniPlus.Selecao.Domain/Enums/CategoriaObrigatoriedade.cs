namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Categorias de <c>ObrigatoriedadeLegal</c> per ADR-0058 §"Forma de
/// ObrigatoriedadeLegal" — agrupam as regras pela dimensão do edital que
/// avaliam (etapa, modalidade, documento, FADA, atendimento, desempate)
/// para suportar filtros admin e dashboards de conformidade.
/// </summary>
/// <remarks>
/// Novas categorias entram via amendment do ADR-0058 e
/// adição de variante tipada correspondente em
/// <c>PredicadoObrigatoriedade</c> quando for o caso.
/// </remarks>
public enum CategoriaObrigatoriedade
{
    [CodigoDeContrato("ETAPA", "Etapa", Descricao = "Regra sobre as etapas do processo seletivo.")]
    Etapa = 1,

    [CodigoDeContrato("MODALIDADE", "Modalidade", Descricao = "Regra sobre as modalidades de concorrência e a reserva de vagas.")]
    Modalidade = 2,

    [CodigoDeContrato("DESEMPATE", "Desempate", Descricao = "Regra sobre os critérios de desempate.")]
    Desempate = 3,

    [CodigoDeContrato("DOCUMENTO", "Documento", Descricao = "Regra sobre os documentos exigidos dos candidatos.")]
    Documento = 4,

    [CodigoDeContrato("FADA", "Fator de Atenuação das Desigualdades de Acesso (FADA)", Descricao = "Regra sobre o Fator de Atenuação das Desigualdades de Acesso (FADA).")]
    Fada = 5,

    [CodigoDeContrato("ATENDIMENTO", "Atendimento", Descricao = "Regra sobre o atendimento especializado.")]
    Atendimento = 6,

    [CodigoDeContrato("OUTROS", "Outros", Descricao = "Regra que não pertence a nenhuma das outras categorias.")]
    Outros = 7,
}
