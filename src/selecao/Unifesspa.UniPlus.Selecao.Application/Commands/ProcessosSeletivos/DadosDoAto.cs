namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Dados documentais do ato normativo que o operador declara ao publicar ou retificar
/// (ADR-0108): o que o documento diz de si mesmo, e que Seleção não tem como saber.
/// </summary>
/// <remarks>
/// <para>
/// Nada aqui é derivado. O órgão não sai da Unidade, o assinante não é o usuário
/// autenticado, a data não é o relógio — é a data que o documento declara, distinção que
/// a #803 já tinha custado caro — e o tipo não se infere do contexto: a ADR-0103 é
/// explícita em que retificação é uma <b>relação</b>, não um tipo, e um aviso pode
/// retificar um edital. Presumir qualquer um destes campos seria inventar conteúdo
/// documental.
/// </para>
/// <para>
/// <see cref="TipoAtoCodigo"/> é validado contra o catálogo de tipos de
/// <c>Publicacoes</c>, que é cadastro — acrescentar um tipo de ato é inserir linha, nunca
/// alterar código (ADR-0103).
/// </para>
/// </remarks>
public sealed record DadosDoAto(
    string Orgao,
    string Serie,
    int Ano,
    DateOnly DataPublicacao,
    string Assinante,
    string TipoAtoCodigo);
