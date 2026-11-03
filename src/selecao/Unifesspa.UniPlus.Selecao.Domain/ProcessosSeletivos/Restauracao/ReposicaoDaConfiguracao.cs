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

    internal static ReposicaoDosEscalares Repor(ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(colecoes);
        ArgumentNullException.ThrowIfNull(grafo);

        ReporEtapas(colecoes, processoId, grafo);
        PartesDoProcesso.Substituir(colecoes.DistribuicaoVagas, grafo.DistribuicaoVagas, processoId);
        PartesDoProcesso.Substituir(colecoes.CriteriosDesempate, grafo.CriteriosDesempate, processoId);
        ReporTermos(colecoes, processoId, grafo);
        Dictionary<Guid, Guid> faseCongeladaParaViva = ReporFases(colecoes, processoId, grafo);
        List<DocumentoExigido> documentos = ReporDocumentos(colecoes, processoId, grafo, faseCongeladaParaViva);
        ReporFormularios(colecoes, processoId, grafo, faseCongeladaParaViva);
        ReporArvoreDeExigencias(colecoes, processoId, grafo, documentos);
        ReporColeta(colecoes, processoId, grafo);
        ReporOpcoesDeclaradas(colecoes, processoId, grafo);

        return ReposicaoDosEscalares.De(grafo, ReferenciaTemporalRemapeada(grafo, faseCongeladaParaViva));
    }

    private static void ReporEtapas(ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo)
    {
        Dictionary<Guid, EtapaProcesso> rastreadas = colecoes.Etapas.ToDictionary(e => e.Id);
        List<EtapaProcesso> etapas = [];
        foreach (EtapaProcesso congelada in grafo.Etapas)
        {
            if (rastreadas.TryGetValue(congelada.Id, out EtapaProcesso? viva))
            {
                viva.ReporDe(congelada);
                etapas.Add(viva);
            }
            else
            {
                etapas.Add(congelada);
            }
        }

        PartesDoProcesso.Substituir(colecoes.Etapas, etapas, processoId);
    }

    private static void ReporTermos(ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo)
    {
        Dictionary<(FinalidadeFormulario, string), TermoExigidoFormulario> rastreados =
            colecoes.TermosExigidos.ToDictionary(static t => (t.Finalidade, t.Codigo));
        PartesDoProcesso.Substituir(
            colecoes.TermosExigidos,
            grafo.TermosExigidos.Select(congelado => rastreados.GetValueOrDefault((congelado.Finalidade, congelado.Codigo)) ?? congelado),
            processoId);
    }

    /// <summary>
    /// A reconciliação é por Ordem, não por Id (índice único de Ordem): a fase viva sobrevive e
    /// recebe o conteúdo congelado. O mapa devolvido leva as referências feitas ao Id congelado
    /// (documentos, formulários, âncora temporal) para o Id da fase que ficou.
    /// </summary>
    private static Dictionary<Guid, Guid> ReporFases(ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo)
    {
        Dictionary<int, FaseCronograma> rastreadas = colecoes.CronogramaFases.ToDictionary(f => f.Ordem);
        Dictionary<Guid, Guid> congeladaParaViva = [];
        List<FaseCronograma> fases = [];
        foreach (FaseCronograma congelada in grafo.CronogramaFases)
        {
            if (!rastreadas.TryGetValue(congelada.Ordem, out FaseCronograma? viva))
            {
                fases.Add(congelada);
                continue;
            }

            IReadOnlyList<ProdutoDaFase> produtos = ProdutosParaAFaseViva(congelada, viva);
            ReancorarRecursoNosProdutosRecriados(congelada, produtos);
            viva.AtualizarDe(congelada, produtos);
            fases.Add(viva);
            if (viva.Id != congelada.Id)
            {
                congeladaParaViva[congelada.Id] = viva.Id;
            }
        }

        PartesDoProcesso.Substituir(colecoes.CronogramaFases, fases, processoId);
        return congeladaParaViva;
    }

    private static List<DocumentoExigido> ReporDocumentos(
        ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo, Dictionary<Guid, Guid> faseCongeladaParaViva)
    {
        Dictionary<Guid, DocumentoExigido> rastreados = colecoes.DocumentosExigidos.ToDictionary(d => d.Id);
        List<DocumentoExigido> documentos = [];
        foreach (DocumentoExigido congelado in grafo.DocumentosExigidos)
        {
            if (faseCongeladaParaViva.TryGetValue(congelado.ExigidoNaFaseId, out Guid faseViva))
            {
                congelado.RemapearFase(faseViva);
            }

            documentos.Add(rastreados.GetValueOrDefault(congelado.Id) ?? congelado);
        }

        PartesDoProcesso.Substituir(colecoes.DocumentosExigidos, documentos, processoId);
        return documentos;
    }

    private static void ReporFormularios(
        ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo, Dictionary<Guid, Guid> faseCongeladaParaViva)
    {
        Dictionary<FinalidadeFormulario, FormularioProcesso> rastreados = colecoes.Formularios.ToDictionary(static f => f.Finalidade);
        List<FormularioProcesso> formularios = [];
        foreach (FormularioProcesso congelado in grafo.Formularios)
        {
            if (congelado.FaseId is { } faseCongelada && faseCongeladaParaViva.TryGetValue(faseCongelada, out Guid faseViva))
            {
                congelado.RemapearFase(faseViva);
            }

            if (rastreados.TryGetValue(congelado.Finalidade, out FormularioProcesso? vivo))
            {
                vivo.Repor(congelado);
                formularios.Add(vivo);
            }
            else
            {
                formularios.Add(congelado);
            }
        }

        PartesDoProcesso.Substituir(colecoes.Formularios, formularios, processoId);
    }

    /// <summary>
    /// A folha aponta para a instância final do documento (a viva ou a congelada, a que a
    /// reconciliação dos documentos escolheu); sem o remapeamento ela ficaria apontando para o
    /// objeto congelado descartado.
    /// </summary>
    private static void ReporArvoreDeExigencias(
        ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo, List<DocumentoExigido> documentos)
    {
        Dictionary<Guid, DocumentoExigido> documentosPorId = documentos.ToDictionary(d => d.Id);
        Dictionary<Guid, NoExigencia> rastreados = colecoes.NosExigencia.ToDictionary(n => n.Id);
        List<NoExigencia> nos = [];
        foreach (NoExigencia congelado in grafo.NosExigencia)
        {
            NoExigencia no = rastreados.GetValueOrDefault(congelado.Id) ?? congelado;
            if (no.Tipo == TipoNo.Folha && documentosPorId.TryGetValue(no.DocumentoExigidoId!.Value, out DocumentoExigido? documento))
            {
                no.RemapearDocumentoExigido(documento);
            }

            nos.Add(no);
        }

        colecoes.NosExigencia.Clear();
        foreach (NoExigencia raiz in nos.Where(static n => n.NoPaiId is null))
        {
            PartesDoProcesso.Vincular(raiz, processoId);
        }

        colecoes.NosExigencia.AddRange(nos);
    }

    /// <summary>
    /// O descarte limpa a coleta e a derivação e grava antes desta reposição, então as coleções
    /// chegam vazias e as instâncias congeladas entram como inserção; o ramo rastreado só vale
    /// quando a coleção viva não foi editada (sombra de verificação, restauração sem edição).
    /// </summary>
    private static void ReporColeta(ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo)
    {
        Dictionary<string, FatoColetado> fatosRastreados = colecoes.Campos
            .Where(static f => f.GrupoColetadoId is null).ToDictionary(f => f.FatoCodigo, StringComparer.Ordinal);
        Dictionary<(FinalidadeFormulario, string), GrupoColetado> gruposRastreados =
            colecoes.GruposColetados.ToDictionary(static g => (g.Finalidade, g.Codigo));
        Dictionary<string, ConfiguracaoDerivacaoFato> derivacoesRastreadas =
            colecoes.RegrasDerivacao.ToDictionary(c => c.CodigoFato, StringComparer.Ordinal);

        PartesDoProcesso.Substituir(colecoes.Campos, grafo.FatosColetados.Select(c => fatosRastreados.GetValueOrDefault(c.FatoCodigo) ?? c), processoId);
        List<GrupoColetado> grupos = [.. grafo.GruposColetados.Select(c => gruposRastreados.GetValueOrDefault((c.Finalidade, c.Codigo)) ?? c)];
        PartesDoProcesso.Substituir(colecoes.GruposColetados, grupos, processoId);
        colecoes.Campos.AddRange(grupos.SelectMany(static g => g.Subitens));
        PartesDoProcesso.Substituir(
            colecoes.RegrasDerivacao,
            grafo.RegrasDerivacao.Select(c => derivacoesRastreadas.GetValueOrDefault(c.CodigoFato) ?? c),
            processoId);
    }

    /// <summary>
    /// Por fato congelado, a versão manda: a opção viva de mesma chave recebe rótulo e ordem
    /// congelados, a viva que a versão não tem sai e a congelada que falta entra. Opções de fato
    /// que a versão não congela são configuração viva e ficam como estão.
    /// </summary>
    private static void ReporOpcoesDeclaradas(ColecoesDoProcesso colecoes, Guid processoId, GrafoConfiguracao grafo)
    {
        foreach (IGrouping<string, OpcaoDeclaradaFato> doFato in grafo.OpcoesDeclaradas.GroupBy(static o => o.FatoCodigo))
        {
            Dictionary<string, OpcaoDeclaradaFato> congeladas = doFato.ToDictionary(static o => o.Codigo, StringComparer.Ordinal);
            colecoes.OpcoesDeclaradas.RemoveAll(o => string.Equals(o.FatoCodigo, doFato.Key, StringComparison.Ordinal)
                && !congeladas.ContainsKey(o.Codigo));

            foreach (OpcaoDeclaradaFato congelada in congeladas.Values)
            {
                OpcaoDeclaradaFato? viva = colecoes.OpcoesDeclaradas.FirstOrDefault(o =>
                    string.Equals(o.FatoCodigo, congelada.FatoCodigo, StringComparison.Ordinal)
                    && string.Equals(o.Codigo, congelada.Codigo, StringComparison.Ordinal));
                if (viva is not null)
                {
                    viva.ReporConteudo(congelada.Rotulo, congelada.Ordem);
                    continue;
                }

                PartesDoProcesso.Vincular(congelada, processoId);
                colecoes.OpcoesDeclaradas.Add(congelada);
            }
        }
    }

    private static ReferenciaTemporalFatos? ReferenciaTemporalRemapeada(GrafoConfiguracao grafo, Dictionary<Guid, Guid> faseCongeladaParaViva) =>
        grafo.ReferenciaTemporalFatos is { FaseId: { } faseCongelada } referencia
            && faseCongeladaParaViva.TryGetValue(faseCongelada, out Guid faseViva)
                ? referencia.ComFaseIdRemapeada(faseViva)
                : grafo.ReferenciaTemporalFatos;
}
