namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// SPIKE #820 — dados documentais do ato fixados para provar o MECANISMO.
/// </summary>
/// <remarks>
/// Na implementação real nada disto é constante: órgão, série, ano, assinante e o
/// próprio tipo do ato são <b>declarados pelo operador</b> no request de publicação. A
/// revisão do plano foi explícita em não presumir nenhum deles — a data documental é o
/// que o documento declara (não o relógio), o assinante não é o usuário autenticado, e a
/// ADR-0103 diz que o tipo do ato não se infere: um aviso pode retificar um edital.
/// </remarks>
internal static class DadosDoAtoSpike
{
    public const string Orgao = "CEPS";
    public const string Serie = "EDITAL";
    public const int Ano = 2026;
    public const string Assinante = "Spike 819/820";
    public const string TipoAbertura = "EDITAL_ABERTURA";
    public const string EntidadeProcessoSeletivo = "PROCESSO_SELETIVO";
}
