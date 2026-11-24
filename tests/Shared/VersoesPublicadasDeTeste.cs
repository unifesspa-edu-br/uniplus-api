namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As versões publicadas de um processo, para os testes que abrem a retificação sem passar pelo
/// envelope: só o que diz que fatos cada formulário coletou — os formulários, os itens e os grupos.
/// As demais dimensões do grafo são mínimas, porque a leitura dos fatos não as consulta.
/// </summary>
internal static class VersoesPublicadasDeTeste
{
    private static readonly ConfiguracaoClassificacao ClassificacaoMinima = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), 'b'),
        RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
    }).Value!;

    /// <summary>Uma versão publicada com estes formulários, itens e grupos.</summary>
    public static ConfiguracaoPublicada Versao(
        IEnumerable<FormularioProcesso> formularios, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado>? grupos = null) => new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, []),
            new AvaliacaoDeclarada([], [], ClassificacaoMinima),
            new VagasDeclaradas([], null, false,
                null, null, AtendimentoEspecializado.Criar([], [], []).Value!),
            new ExigenciasDeclaradas([], []),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta([.. itens], [.. grupos ?? []]), [.. grupos ?? []], [.. formularios], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

    /// <summary>A versão que o processo publicaria agora, com os formulários, os itens e os grupos que ele tem.</summary>
    public static ConfiguracaoPublicada DoProcesso(ProcessoSeletivo processo) =>
        Versao(processo.Formularios, processo.FatosColetados, processo.GruposColetados);

    /// <summary>O processo publicado uma vez só, com o que ele tem agora.</summary>
    public static FatosDasVersoesPublicadas SoAVigente(ProcessoSeletivo processo) =>
        SemEixosCongelados([DoProcesso(processo)]);

    /// <summary>As versões lidas sem o que cada uma congelou além dos itens: a guarda de metadado e de agregado não tem o que comparar.</summary>
    public static FatosDasVersoesPublicadas SemEixosCongelados(IReadOnlyList<ConfiguracaoPublicada> versoes) =>
        FatosDasVersoesPublicadas.De(
            versoes,
            [.. versoes.Select(static _ => new EixosDeVersaoPublicada(new Dictionary<string, EixosDoMetadadoCongelado>(), new Dictionary<string, string>()))]);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));
}
