namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Repõe a configuração viva a partir do grafo congelado já validado: a partir daqui não há
/// caminho de falha, e é isso que torna uma restauração recusada incapaz de alterar o agregado
/// (ADR-0110 D2). A instância rastreada é reusada e atualizada, nunca substituída por outra de
/// mesmo Id, porque o identity map do EF recusaria a segunda.
/// </summary>
internal static class ReposicaoDaConfiguracao
{
    /// <summary>
    /// Os produtos que a fase VIVA recebe da fase congelada de mesma <see cref="FaseCronograma.Ordem"/>:
    /// as próprias instâncias congeladas quando as duas fases são a MESMA, e cópias com
    /// identidade nova quando não são.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O <c>Id</c> de <see cref="ProdutoDaFase"/> é congelado no envelope, e a reconciliação
    /// dentro de <see cref="FaseCronograma.AtualizarDe"/> é por <c>AtoCodigo</c>, feita
    /// já dentro de uma fase: ela não enxerga o cronograma inteiro e não tem como saber que a
    /// fase congelada caiu sobre outra fase viva. Quando isso acontece — a sessão editorial
    /// deslocou as ordens, e a reconciliação por Ordem passa a casar fases diferentes —,
    /// deixar o Id congelado atravessar produz dois estragos ao mesmo tempo: a linha do
    /// produto migra para uma fase que não é a que a congelou, e o MESMO Id pode entrar como
    /// instância nova enquanto a linha rastreada homônima sai de outra fase como órfã, o que
    /// o identity map do EF recusa — o descarte do rascunho estouraria, e o operador ficaria
    /// sem como desfazer o deslocamento.
    /// </para>
    /// <para>
    /// A identidade congelada só vale enquanto ancorada na fase que a congelou, e é por isso
    /// que ela é descartada aqui, com o produto recriado — a mesma disciplina que as bancas
    /// requeridas já têm por nunca terem Id no envelope. A âncora do prazo de recurso é a
    /// única referência que aponta para o Id de um produto, e
    /// <see cref="ReancorarRecursoNosProdutosRecriados"/> a acompanha; o caminho que PRECISA
    /// preservar o Id — a sombra de verificação, que nasce sem fase viva alguma — nunca passa
    /// por aqui, porque cai no ramo que adota a instância congelada inteira.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<ProdutoDaFase> ProdutosParaAFaseViva(FaseCronograma congelada, FaseCronograma viva) =>
        viva.Id == congelada.Id
            ? [.. congelada.Produtos]
            : [.. congelada.Produtos.Select(static p => ProdutoDaFase.Criar(p.AtoCodigo, p.Papel))];

    /// <summary>
    /// Reaponta a âncora do recurso da fase congelada para o produto recriado equivalente,
    /// quando <see cref="ProdutosParaAFaseViva"/> descartou as identidades congeladas.
    /// </summary>
    /// <remarks>
    /// Sem isto a regra de recurso restaurada ficaria apontando para um produto que a
    /// restauração acabou de descartar, e o ato publicado deixaria de encontrar o prazo que
    /// lhe corresponde. A ponte é o PAR <see cref="ProdutoDaFase.AtoCodigo"/> +
    /// <see cref="ProdutoDaFase.Papel"/>, a mesma chave que as demais reconciliações de produto
    /// usam: o ato sozinho deixou de ser único dentro da fase quando a mesma matéria passou a
    /// ser publicável como preliminar e como definitiva, e casar só por ele remapeava a âncora
    /// do preliminar para o definitivo sempre que este viesse primeiro — o recurso passaria a
    /// correr da publicação errada, e a snapshot preservaria o engano. A segunda etapa — do
    /// produto recriado para a instância rastreada que porventura sobreviva — é feita por
    /// <c>FaseCronograma.AtualizarDe</c>.
    /// </remarks>
    internal static void ReancorarRecursoNosProdutosRecriados(
        FaseCronograma congelada,
        IReadOnlyList<ProdutoDaFase> produtosDaViva)
    {
        if (congelada.RegraRecurso is not { } regraRecurso
            || congelada.Produtos.FirstOrDefault(p => p.Id == regraRecurso.ProdutoAncoraId) is not { } ancoraCongelada)
        {
            return;
        }

        ProdutoDaFase? ancoraRecriada = produtosDaViva.FirstOrDefault(
            p => string.Equals(p.AtoCodigo, ancoraCongelada.AtoCodigo, StringComparison.Ordinal)
                && p.Papel == ancoraCongelada.Papel);
        if (ancoraRecriada is not null && ancoraRecriada.Id != regraRecurso.ProdutoAncoraId)
        {
            regraRecurso.RemapearAncora(ancoraRecriada.Id);
        }
    }

    /// <summary>
    /// Repõe no lugar as partes rastreadas que a versão congelada também tem e devolve a mudança nas
    /// coleções: o que sai e o que entra, com a instância rastreada no lugar da congelada sempre que há uma.
    /// </summary>
    internal static ReposicaoRealizada Repor(EstadoDaConfiguracao estado, GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(grafo);

        MudancaDaConfiguracao mudanca = ReporOQueNaoDependeDasFases(estado, grafo);
        FasesRepostas fases = ReporFases(estado, grafo);
        mudanca = ReporOQueDependeDasFases(estado, grafo, fases.CongeladaParaViva, mudanca with { CronogramaFases = fases.Troca });

        return new ReposicaoRealizada(mudanca, ReferenciaTemporalRemapeada(grafo, fases.CongeladaParaViva));
    }

    private static MudancaDaConfiguracao ReporOQueNaoDependeDasFases(EstadoDaConfiguracao estado, GrafoConfiguracao grafo) =>
        ReporColeta(estado, grafo) with
        {
            Etapas = ReporEtapas(estado, grafo),
            DistribuicaoVagas = Troca.Substituindo(estado.DistribuicaoVagas, grafo.DistribuicaoVagas),
            CriteriosDesempate = Troca.Substituindo(estado.CriteriosDesempate, grafo.CriteriosDesempate),
            TermosExigidos = ReporTermos(estado, grafo),
            OpcoesDeclaradas = ReporOpcoesDeclaradas(estado, grafo),
            RegrasDerivacao = ReporDerivacoes(estado, grafo),
        };

    private static MudancaDaConfiguracao ReporOQueDependeDasFases(
        EstadoDaConfiguracao estado, GrafoConfiguracao grafo, IReadOnlyDictionary<Guid, Guid> faseCongeladaParaViva, MudancaDaConfiguracao mudanca)
    {
        Troca<DocumentoExigido> documentos = ReporDocumentos(estado, grafo, faseCongeladaParaViva);
        return mudanca with
        {
            DocumentosExigidos = documentos,
            Formularios = ReporFormularios(estado, grafo, faseCongeladaParaViva),
            NosExigencia = ReporArvoreDeExigencias(estado, grafo, documentos.Entram),
        };
    }

    private static Troca<EtapaProcesso> ReporEtapas(EstadoDaConfiguracao estado, GrafoConfiguracao grafo)
    {
        Dictionary<Guid, EtapaProcesso> rastreadas = estado.Etapas.ToDictionary(e => e.Id);
        List<EtapaProcesso> etapas = [];
        foreach (EtapaProcesso congelada in grafo.Etapas)
        {
            etapas.Add(ReporEtapa(rastreadas, congelada));
        }

        return Troca.Substituindo(estado.Etapas, etapas);
    }

    private static EtapaProcesso ReporEtapa(Dictionary<Guid, EtapaProcesso> rastreadas, EtapaProcesso congelada)
    {
        if (!rastreadas.TryGetValue(congelada.Id, out EtapaProcesso? viva))
        {
            return congelada;
        }

        viva.ReporDe(congelada);
        return viva;
    }

    private static Troca<TermoExigidoFormulario> ReporTermos(EstadoDaConfiguracao estado, GrafoConfiguracao grafo)
    {
        Dictionary<(FinalidadeFormulario, string), TermoExigidoFormulario> rastreados =
            estado.TermosExigidos.ToDictionary(static t => (t.Finalidade, t.Codigo));
        return Troca.Substituindo(
            estado.TermosExigidos,
            grafo.TermosExigidos.Select(congelado => rastreados.GetValueOrDefault((congelado.Finalidade, congelado.Codigo)) ?? congelado));
    }

    /// <summary>
    /// A reconciliação é por Ordem, não por Id (índice único de Ordem): a fase viva sobrevive e
    /// recebe o conteúdo congelado. O mapa devolvido leva as referências feitas ao Id congelado
    /// (documentos, formulários, âncora temporal) para o Id da fase que ficou.
    /// </summary>
    private static FasesRepostas ReporFases(EstadoDaConfiguracao estado, GrafoConfiguracao grafo)
    {
        Dictionary<int, FaseCronograma> rastreadas = estado.CronogramaFases.ToDictionary(f => f.Ordem);
        List<(FaseCronograma Congelada, FaseCronograma Final)> pares = [];
        foreach (FaseCronograma congelada in grafo.CronogramaFases)
        {
            pares.Add((congelada, ReporFase(rastreadas, congelada)));
        }

        Dictionary<Guid, Guid> congeladaParaViva = pares
            .Where(static par => par.Final.Id != par.Congelada.Id)
            .ToDictionary(static par => par.Congelada.Id, static par => par.Final.Id);
        return new FasesRepostas(Troca.Substituindo(estado.CronogramaFases, pares.Select(static par => par.Final)), congeladaParaViva);
    }

    private static FaseCronograma ReporFase(Dictionary<int, FaseCronograma> rastreadas, FaseCronograma congelada)
    {
        if (!rastreadas.TryGetValue(congelada.Ordem, out FaseCronograma? viva))
        {
            return congelada;
        }

        IReadOnlyList<ProdutoDaFase> produtos = ProdutosParaAFaseViva(congelada, viva);
        ReancorarRecursoNosProdutosRecriados(congelada, produtos);
        viva.AtualizarDe(congelada, produtos);
        return viva;
    }

    private static Troca<DocumentoExigido> ReporDocumentos(
        EstadoDaConfiguracao estado, GrafoConfiguracao grafo, IReadOnlyDictionary<Guid, Guid> faseCongeladaParaViva)
    {
        Dictionary<Guid, DocumentoExigido> rastreados = estado.DocumentosExigidos.ToDictionary(d => d.Id);
        List<DocumentoExigido> documentos = [];
        foreach (DocumentoExigido congelado in grafo.DocumentosExigidos)
        {
            if (faseCongeladaParaViva.TryGetValue(congelado.ExigidoNaFaseId, out Guid faseViva))
            {
                congelado.RemapearFase(faseViva);
            }

            documentos.Add(rastreados.GetValueOrDefault(congelado.Id) ?? congelado);
        }

        return Troca.Substituindo(estado.DocumentosExigidos, documentos);
    }

    private static Troca<FormularioProcesso> ReporFormularios(
        EstadoDaConfiguracao estado, GrafoConfiguracao grafo, IReadOnlyDictionary<Guid, Guid> faseCongeladaParaViva)
    {
        Dictionary<FinalidadeFormulario, FormularioProcesso> rastreados = estado.Formularios.ToDictionary(static f => f.Finalidade);
        List<FormularioProcesso> formularios = [];
        foreach (FormularioProcesso congelado in grafo.Formularios)
        {
            formularios.Add(ReporFormulario(rastreados, faseCongeladaParaViva, congelado));
        }

        return Troca.Substituindo(estado.Formularios, formularios);
    }

    private static FormularioProcesso ReporFormulario(
        Dictionary<FinalidadeFormulario, FormularioProcesso> rastreados, IReadOnlyDictionary<Guid, Guid> faseCongeladaParaViva, FormularioProcesso congelado)
    {
        if (congelado.FaseId is { } faseCongelada && faseCongeladaParaViva.TryGetValue(faseCongelada, out Guid faseViva))
        {
            congelado.RemapearFase(faseViva);
        }

        if (!rastreados.TryGetValue(congelado.Finalidade, out FormularioProcesso? vivo))
        {
            return congelado;
        }

        vivo.Repor(congelado);
        return vivo;
    }

    /// <summary>
    /// A folha aponta para a instância final do documento (a viva ou a congelada, a que a
    /// reconciliação dos documentos escolheu); sem o remapeamento ela ficaria apontando para o
    /// objeto congelado descartado.
    /// </summary>
    private static Troca<NoExigencia> ReporArvoreDeExigencias(
        EstadoDaConfiguracao estado, GrafoConfiguracao grafo, IReadOnlyList<DocumentoExigido> documentos)
    {
        Dictionary<Guid, DocumentoExigido> documentosPorId = documentos.ToDictionary(d => d.Id);
        Dictionary<Guid, NoExigencia> rastreados = estado.NosExigencia.ToDictionary(n => n.Id);
        List<NoExigencia> nos = [];
        foreach (NoExigencia congelado in grafo.NosExigencia)
        {
            nos.Add(ReporNo(rastreados.GetValueOrDefault(congelado.Id) ?? congelado, documentosPorId));
        }

        return Troca.Substituindo(estado.NosExigencia, nos);
    }

    private static NoExigencia ReporNo(NoExigencia no, Dictionary<Guid, DocumentoExigido> documentosPorId)
    {
        if (no.Tipo == TipoNo.Folha && documentosPorId.TryGetValue(no.DocumentoExigidoId!.Value, out DocumentoExigido? documento))
        {
            no.RemapearDocumentoExigido(documento);
        }

        return no;
    }

    /// <summary>
    /// O descarte limpa a coleta e a derivação e grava antes desta reposição, então as coleções
    /// chegam vazias e as instâncias congeladas entram como inserção; o ramo rastreado só vale
    /// quando a coleção viva não foi editada (sombra de verificação, restauração sem edição).
    /// </summary>
    private static MudancaDaConfiguracao ReporColeta(EstadoDaConfiguracao estado, GrafoConfiguracao grafo)
    {
        Dictionary<string, FatoColetado> itensRastreados = estado.Itens.ToDictionary(f => f.FatoCodigo, StringComparer.Ordinal);
        Dictionary<(FinalidadeFormulario, string), GrupoColetado> gruposRastreados =
            estado.GruposColetados.ToDictionary(static g => (g.Finalidade, g.Codigo));
        IEnumerable<FatoColetado> itens = grafo.FatosColetados.Select(c => itensRastreados.GetValueOrDefault(c.FatoCodigo) ?? c);
        List<GrupoColetado> grupos = [.. grafo.GruposColetados.Select(c => gruposRastreados.GetValueOrDefault((c.Finalidade, c.Codigo)) ?? c)];
        return new MudancaDaConfiguracao
        {
            Campos = Troca.Substituindo(estado.Campos, [.. itens, .. grupos.SelectMany(static g => g.Subitens)]),
            GruposColetados = Troca.Substituindo(estado.GruposColetados, grupos),
        };
    }

    private static Troca<ConfiguracaoDerivacaoFato> ReporDerivacoes(EstadoDaConfiguracao estado, GrafoConfiguracao grafo)
    {
        Dictionary<string, ConfiguracaoDerivacaoFato> rastreadas = estado.RegrasDerivacao.ToDictionary(c => c.CodigoFato, StringComparer.Ordinal);
        return Troca.Substituindo(
            estado.RegrasDerivacao,
            grafo.RegrasDerivacao.Select(c => rastreadas.GetValueOrDefault(c.CodigoFato) ?? c));
    }

    private static Troca<OpcaoDeclaradaFato> ReporOpcoesDeclaradas(EstadoDaConfiguracao estado, GrafoConfiguracao grafo)
    {
        List<Troca<OpcaoDeclaradaFato>> trocas = [];
        foreach (IGrouping<string, OpcaoDeclaradaFato> doFato in grafo.OpcoesDeclaradas.GroupBy(static o => o.FatoCodigo))
        {
            trocas.Add(ReporOpcoesDoFato(estado.OpcoesDeclaradas, doFato));
        }

        return Troca.Reunindo(trocas);
    }

    /// <summary>
    /// Por fato congelado, a versão manda: a opção viva de mesma chave recebe rótulo e ordem
    /// congelados, a viva que a versão não tem sai e a congelada que falta entra. Opções de fato
    /// que a versão não congela são configuração viva e ficam como estão.
    /// </summary>
    private static Troca<OpcaoDeclaradaFato> ReporOpcoesDoFato(
        IReadOnlyCollection<OpcaoDeclaradaFato> opcoesDoProcesso, IGrouping<string, OpcaoDeclaradaFato> doFato)
    {
        List<OpcaoDeclaradaFato> vivas = [.. opcoesDoProcesso.Where(o => string.Equals(o.FatoCodigo, doFato.Key, StringComparison.Ordinal))];
        Dictionary<string, OpcaoDeclaradaFato> congeladas = doFato.ToDictionary(static o => o.Codigo, StringComparer.Ordinal);
        List<OpcaoDeclaradaFato> entram = [];
        foreach (OpcaoDeclaradaFato congelada in congeladas.Values)
        {
            if (vivas.Find(o => string.Equals(o.Codigo, congelada.Codigo, StringComparison.Ordinal)) is { } viva)
            {
                viva.ReporConteudo(congelada.Rotulo, congelada.Ordem);
            }
            else
            {
                entram.Add(congelada);
            }
        }

        return Troca.Substituindo(vivas.Where(o => !congeladas.ContainsKey(o.Codigo)), entram);
    }

    private static ReferenciaTemporalFatos? ReferenciaTemporalRemapeada(GrafoConfiguracao grafo, IReadOnlyDictionary<Guid, Guid> faseCongeladaParaViva) =>
        grafo.ReferenciaTemporalFatos is { FaseId: { } faseCongelada } referencia
            && faseCongeladaParaViva.TryGetValue(faseCongelada, out Guid faseViva)
                ? referencia.ComFaseIdRemapeada(faseViva)
                : grafo.ReferenciaTemporalFatos;

    private sealed record FasesRepostas(Troca<FaseCronograma> Troca, IReadOnlyDictionary<Guid, Guid> CongeladaParaViva);
}
