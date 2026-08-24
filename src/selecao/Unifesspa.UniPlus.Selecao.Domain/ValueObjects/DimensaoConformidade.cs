namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As seções do agregado em que uma pendência estrutural pode aparecer — o conjunto fechado
/// que <see cref="ItemConformidade.Dimensao"/> usa.
/// </summary>
/// <remarks>
/// <para>
/// A dimensão diz <b>onde</b> corrigir; o código do item diz <b>o que</b>. É o que permite a
/// um cliente levar quem publica até a seção certa do editor sem comparar frases em
/// português — uma tabela de mensagens no frontend quebraria na primeira vez que a redação
/// mudasse.
/// </para>
/// <para>
/// São seções do <b>agregado</b>, não da interface. Nenhuma delas carrega rota, aba ou
/// qualquer nome de tela: a mesma dimensão precisa servir a um editor web, a um relatório e
/// a quem lê a resposta pelo terminal.
/// </para>
/// </remarks>
public static class DimensaoConformidade
{
    public const string AtendimentoEspecializado = "atendimento_especializado";
    public const string DistribuicaoVagas = "distribuicao_vagas";
    public const string Classificacao = "classificacao";
    public const string Cronograma = "cronograma";
    public const string TaxaInscricao = "taxa_inscricao";
    public const string ExigenciasDocumentais = "exigencias_documentais";
    public const string CascataRemanejamento = "cascata_remanejamento";
    public const string ColetaDeFatos = "coleta_de_fatos";
}
