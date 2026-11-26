namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class CitacoesDosFormularios
{
    internal static readonly IReadOnlyDictionary<string, string> SemCatalogo = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Monta o grafo de dependência conjunto a partir das dimensões da configuração
    /// que o alimentam — os fatos coletados (campo + fato + regras do item), as regras de derivação
    /// (fato derivado + dependências), as exigências (gatilho), as seções com exibição condicional e
    /// os termos com condição — e valida a sua
    /// aciclicidade sobre as quatro classes de aresta juntas. Projeção read-only, sem mutar o
    /// agregado: um ciclo volta como erro nomeado, nunca lança. O congelamento do grafo no envelope e
    /// a recusa de publicação por ciclo são da fatia de determinismo (§7).
    /// </summary>
    public static Result<GrafoDependenciaConjunta> ConstruirGrafoDependencia(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return GrafoDependenciaConjunta.Construir([.. configuracao.Coleta.Itens], configuracao.DerivacoesEfetivas(), configuracao.Exigencias.DocumentosExigidos, configuracao.Coleta.Formularios, configuracao.Coleta.TermosExigidos, configuracao.Coleta.GruposColetados);
    }

    /// <summary>Os fatos coletados pela inscrição, que os formulários das outras finalidades podem citar.</summary>
    internal static HashSet<string> FatosDaInscricao(IEnumerable<FatoColetado> fatos) =>
        new(fatos.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao).Select(static f => f.FatoCodigo), StringComparer.Ordinal);

    /// <summary>
    /// A primeira citação que usa, de outro formulário, fato que ele não coletou em todas as versões
    /// publicadas (UNI-REQ-0144): quem preencheu aquele formulário numa versão anterior pode não o ter
    /// informado. Vale para o fato citado e para todo fato de que ele depende: pela regra de derivação
    /// viva, pelo mecanismo do derivado do sistema ou, no agregado sobre grupo repetível, pelo fato de membro.
    /// </summary>
    /// <param name="citacoes">Cada fato citado, com quem o cita, como a mensagem o nomeia.</param>
    /// <param name="formularioProprio">
    /// O formulário de quem cita: os fatos dele seguem a versão do próprio formulário. Nulo quando quem cita
    /// não é formulário, e então todo fato de formulário precisa estar garantido.
    /// </param>
    internal static DomainError? CitacaoNaoGarantida(
        ConfiguracaoDeclarada configuracao,
        FatosDasVersoesPublicadas publicadas,
        IEnumerable<(string Citante, string Citado)> citacoes,
        CodigoDoFormulario? formularioProprio,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        foreach ((string citante, string citado) in citacoes)
        {
            if (FatoNaoGarantido(configuracao, citado, formularioProprio, publicadas, membroPorAgregado, []) is { } naoGarantido)
            {
                return ErroDeFatoNaoGarantido(citante, citado, naoGarantido);
            }
        }

        return null;
    }

    // Provisório até a retirada da finalidade: quem ainda cita pela finalidade tem o formulário dela como próprio.
    internal static DomainError? CitacaoNaoGarantida(
        ConfiguracaoDeclarada configuracao,
        FatosDasVersoesPublicadas publicadas,
        IEnumerable<(string Citante, string Citado)> citacoes,
        FinalidadeFormulario? finalidadePropria,
        IReadOnlyDictionary<string, string> membroPorAgregado) =>
        CitacaoNaoGarantida(
            configuracao, publicadas, citacoes, finalidadePropria is { } propria ? FormularioDeclarado.DaFinalidade(propria, null).Codigo : null, membroPorAgregado);

    private static DomainError ErroDeFatoNaoGarantido(
        string citante, string citado, (string Fato, CodigoDoFormulario Produtor) naoGarantido)
    {
        string sujeito = string.Equals(citado, naoGarantido.Fato, StringComparison.Ordinal)
            ? $"O fato '{citado}', citado por {citante}, não"
            : $"O fato '{citado}', citado por {citante}, depende de '{naoGarantido.Fato}', que não";
        return new DomainError(
            RetificacaoErrorCodes.FatoAusenteDeVersaoPublicada,
            $"{sujeito} é coletado pelo formulário de {naoGarantido.Produtor.Valor} em todas as versões publicadas do edital: quem preencheu esse formulário numa versão anterior não o informou.");
    }

    /// <summary>
    /// O fato coletado por outro formulário e fora do que ele garante, entre o fato e as dependências
    /// dele. O fato coletado encerra a busca: ele não depende de outro.
    /// </summary>
    internal static (string Fato, CodigoDoFormulario Produtor)? FatoNaoGarantido(
        ConfiguracaoDeclarada configuracao,
        string fato,
        CodigoDoFormulario? formularioProprio,
        FatosDasVersoesPublicadas publicadas,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        HashSet<string> emAvaliacao)
    {
        if (configuracao.Coleta.Campos.FirstOrDefault(f => string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal)) is { } coletado)
        {
            return configuracao.Coleta.Formularios.FirstOrDefault(f => f.Id == coletado.FormularioId)?.Codigo is { } produtor
                && produtor != formularioProprio && !publicadas.Garante(produtor, fato)
                ? (fato, produtor)
                : null;
        }

        if (!emAvaliacao.Add(fato))
        {
            return null;
        }

        return DependenciaNaoGarantida(configuracao, fato, formularioProprio, publicadas, membroPorAgregado, emAvaliacao);
    }

    private static (string Fato, CodigoDoFormulario Produtor)? DependenciaNaoGarantida(
        ConfiguracaoDeclarada configuracao,
        string fato,
        CodigoDoFormulario? formularioProprio,
        FatosDasVersoesPublicadas publicadas,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        HashSet<string> emAvaliacao)
    {
        foreach (string dependencia in FaseEfetivaDoFato.DependenciasDoFato(configuracao, fato, membroPorAgregado))
        {
            if (FatoNaoGarantido(configuracao, dependencia, formularioProprio, publicadas, membroPorAgregado, emAvaliacao) is { } naoGarantido)
            {
                return naoGarantido;
            }
        }

        return null;
    }

    /// <summary>As citações das regras de um formulário, com quem cita nomeado junto do formulário.</summary>
    internal static IEnumerable<(string Citante, string Citado)> DoFormulario(
        CodigoDoFormulario formulario, IEnumerable<(string Dono, string Citado)> citacoes) =>
        citacoes.Select(c => ($"{c.Dono} do formulário de {formulario.Valor}", c.Citado));

    /// <summary>
    /// A regra de formulário que usa, de outro formulário, fato fora do que ele coletou em
    /// todas as versões publicadas. Alcança o que o documento novo declara, inclusive a regra de
    /// derivação que passou a fazer um derivado citado depender de fato novo. Sem versão publicada,
    /// nenhum candidato preencheu versão anterior e não há o que conferir.
    /// </summary>
    internal static DomainError? PendenciaDeFatoAusenteDeVersaoPublicada(ConfiguracaoDeclarada configuracao, FatosDasVersoesPublicadas? publicadas) =>
        publicadas is null
            ? null
            : configuracao.Coleta.Formularios
                .Select(formulario => CitacaoNaoGarantidaDoFormulario(configuracao, publicadas, formulario))
                .FirstOrDefault(static recusa => recusa is not null);

    private static DomainError? CitacaoNaoGarantidaDoFormulario(
        ConfiguracaoDeclarada configuracao, FatosDasVersoesPublicadas publicadas, FormularioProcesso formulario) =>
        CitacaoNaoGarantida(
            configuracao,
            publicadas,
            DoFormulario(
                formulario.Codigo,
                CitacoesDoFormulario(
                    configuracao,
                    formulario,
                    [.. configuracao.Coleta.ItensDo(formulario)],
                    [.. configuracao.Coleta.GruposDo(formulario)])
                .Select(static c => (c.Dono, c.Citado))),
            formulario.Codigo,
            SemCatalogo);

    /// <summary>
    /// Trocar os itens de um formulário da inscrição não pode tirar um fato que uma regra de outro
    /// formulário cita, direta ou por um derivado: ela passaria a depender de uma resposta que ninguém
    /// pede. Só a citação que a troca invalida é recusada; a que já era inválida fica para a publicação.
    /// O formulário que não é da inscrição não tem o que deixar órfão.
    /// </summary>
    internal static DomainError? CitacaoQueFicariaOrfa(
        ConfiguracaoDeclarada configuracao, FormularioProcesso formulario, IReadOnlyCollection<FatoColetado> novosItens)
    {
        if (!configuracao.EhDaInscricao(formulario.Declarado))
        {
            return null;
        }

        // Os itens novos ainda não têm o formulário vinculado: valem pelo código.
        HashSet<string> daInscricaoDepois = configuracao.FatosDaInscricaoSem(formulario);
        daInscricaoDepois.UnionWith(novosItens.Select(static f => f.FatoCodigo));
        return CitacoesQueFicariamOrfas(
            configuracao, daInscricaoDepois, outro => [.. configuracao.Coleta.ItensDo(outro)], DependenciasDasDerivacoes(configuracao)).FirstOrDefault();
    }

    /// <summary>
    /// As citações dos formulários que não são da inscrição que uma mudança na inscrição, nos itens deles ou nas
    /// derivações deixaria sem o fato citado: compara cada citação antes e depois e devolve só as
    /// que a mudança invalida.
    /// </summary>
    internal static IEnumerable<DomainError> CitacoesQueFicariamOrfas(
        ConfiguracaoDeclarada configuracao,
        IReadOnlySet<string> daInscricaoDepois,
        Func<FormularioProcesso, FatoColetado[]> itensDepois,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoesDepois)
    {
        HashSet<string> atuais = configuracao.FatosDaInscricao();
        IReadOnlySet<Guid> daInscricao = configuracao.IdsDaInscricao();
        Dictionary<string, IReadOnlyCollection<string>> derivacoesAntes = DependenciasDasDerivacoes(configuracao);
        foreach (FormularioProcesso formulario in configuracao.Coleta.Formularios.Where(f => !daInscricao.Contains(f.Id)))
        {
            FatoColetado[] itens = itensDepois(formulario);
            GrupoColetado[] grupos = [.. configuracao.Coleta.GruposDo(formulario)];
            DependenciasDoFormulario antes = DependenciasDe(configuracao.Coleta.ItensDo(formulario), atuais, derivacoesAntes);
            DependenciasDoFormulario depois = DependenciasDe(itens, daInscricaoDepois, derivacoesDepois);
            foreach (DomainError recusa in CitacoesInvalidadas(configuracao, formulario, itens, grupos, antes, depois))
            {
                yield return recusa;
            }
        }
    }

    private static IEnumerable<DomainError> CitacoesInvalidadas(
        ConfiguracaoDeclarada configuracao, FormularioProcesso formulario,
        FatoColetado[] itens, GrupoColetado[] grupos, DependenciasDoFormulario antes, DependenciasDoFormulario depois)
    {
        foreach ((string dono, long posicao, string citado) in CitacoesDoFormulario(configuracao, formulario, itens, grupos))
        {
            if (antes.Conferir(citado, posicao) is null && depois.Conferir(citado, posicao) is not null)
            {
                yield return new DomainError(
                    GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                    $"O fato '{citado}', citado por {dono} do formulário de {formulario.Codigo.Valor}, deixaria de ser conhecido por ele.");
            }
        }
    }

    /// <summary>
    /// Cada citação das regras do formulário, com quem cita e a posição de onde cita: as dos itens e
    /// dos grupos dados, a exibição das seções e as condições dos termos do formulário.
    /// </summary>
    internal static IEnumerable<(string Dono, long Posicao, string Citado)> CitacoesDoFormulario(
        ConfiguracaoDeclarada configuracao,
        FormularioProcesso formulario, FatoColetado[] itens, GrupoColetado[] grupos) =>
        CitacoesDosCampos(itens, grupos)
            .Concat(formulario.Paginas.SelectMany(secao => CitacoesDaSecao(secao)
                .Select(c => (c.Dono,
                    GrafoDoFormulario.PosicaoDaPagina(
                        secao.ParaGrafo(),
                        formulario.Paginas.Select(static e => e.ParaGrafo()),
                        itens.Select(static i => i.ParaGrafo()),
                        grupos.Select(static g => g.ParaGrafo())),
                    c.Citado))))
            .Concat(configuracao.Coleta.TermosDo(formulario)
                .SelectMany(static termo => CitacoesDoTermo(termo).Select(static c => (c.Dono, DependenciasDoFormulario.PosicaoDosTermos, c.Citado))));

    /// <summary>As citações das regras dos itens e dos grupos, cada uma na posição do item ou do grupo que cita.</summary>
    internal static IEnumerable<(string Dono, long Posicao, string Citado)> CitacoesDosCampos(
        IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        itens.SelectMany(static item => item.FatosCitados.Select(c => ($"o item '{item.FatoCodigo}'", (long)item.Ordem, c)))
            .Concat(grupos.SelectMany(static grupo => CitacoesDeFora(grupo).Select(c => ($"o grupo '{grupo.Codigo}'", (long)grupo.Ordem, c))));

    internal static IEnumerable<(string Dono, string Citado)> CitacoesDaSecao(PaginaDoFormulario secao) =>
        secao.FatosCitados.Select(c => ($"a exibição da seção '{secao.Codigo}'", c));

    internal static IEnumerable<(string Dono, string Citado)> CitacoesDoTermo(TermoExigidoFormulario termo) =>
        termo.FatosCitados.Select(c => ($"o termo '{termo.Codigo}'", c));

    /// <summary>
    /// O grafo de coleta de um formulário. As regras de um campo citam campo anterior do mesmo
    /// formulário, em outra finalidade um fato já coletado pela inscrição, que todo candidato
    /// preenche antes, ou um derivado por regra cujas dependências estão todas entre esses
    /// (UNI-REQ-0144, UNI-REQ-0145).
    /// </summary>
    internal static DomainError? ValidarGrafoDeFatos(
        IReadOnlyCollection<PaginaDoFormulario> paginas,
        IReadOnlyList<FatoColetado> fatos,
        IReadOnlyList<GrupoColetado> grupos,
        HashSet<string> daInscricao,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        GrafoDoFormulario.ValidarColeta(
            [.. fatos.Select(static f => f.ParaGrafo())],
            [.. paginas.Select(static e => e.ParaGrafo())],
            daInscricao,
            derivacoes,
            [.. grupos.Select(static g => g.ParaGrafo())]);

    /// <summary>
    /// Os fatos que o grupo cita fora dele: os da exibição e da obrigatoriedade do grupo e os que os
    /// campos citam além dos campos do próprio grupo.
    /// </summary>
    internal static IEnumerable<string> CitacoesDeFora(GrupoColetado grupo)
    {
        HashSet<string> doGrupo = new(grupo.Subitens.Select(static s => s.FatoCodigo), StringComparer.Ordinal);
        return grupo.FatosCitados.Concat(grupo.Subitens.SelectMany(static s => s.FatosCitados).Where(c => !doGrupo.Contains(c)))
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>A quantidade que o teto do formulário conta: cada item, cada grupo e cada campo de grupo.</summary>
    internal static int QuantidadeNoTeto(int itens, IEnumerable<GrupoColetado> grupos) =>
        FormaDoItem.QuantidadeNoTeto(itens, grupos.Select(static g => g.Subitens.Count));

    /// <summary>Os itens e os grupos, na forma que a estrutura confere: o grupo ocupa uma posição na ordem dos itens.</summary>
    internal static IReadOnlyList<ItemEstrutura> ParaEstrutura(IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        [.. itens.Select(static f => new ItemEstrutura(f.FatoCodigo, f.Ordem, f.PaginaCodigo)), .. grupos.Select(static g => g.ParaEstrutura())];

    /// <summary>
    /// A primeira regra de item ou condição de termo que cita fato que o formulário dela não
    /// conhece. É a mesma conferência da definição, repetida na publicação e na leitura do edital
    /// congelado. O formulário da inscrição cita só os campos dele; os demais citam também os da inscrição.
    /// </summary>
    public static DomainError? CitacaoInvalidaNosFormularios(
        IEnumerable<FaseCronograma> cronograma,
        IReadOnlyCollection<FormularioProcesso> formularios,
        IReadOnlyCollection<FatoColetado> fatos,
        IReadOnlyCollection<TermoExigidoFormulario> termos,
        IReadOnlyCollection<ConfiguracaoDerivacaoFato> derivacoes,
        IReadOnlyCollection<GrupoColetado>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(derivacoes);

        HashSet<CodigoDoFormulario> codigosDaInscricao = [.. FormulariosDaInscricao.De([.. formularios.Select(static f => f.Declarado)], cronograma).Select(static f => f.Codigo)];
        IReadOnlySet<Guid> daInscricao = formularios.Where(f => codigosDaInscricao.Contains(f.Codigo)).Select(static f => f.Id).ToHashSet();
        Dictionary<string, IReadOnlyCollection<string>> dependenciasDasDerivacoes = MapaDeDerivacoes(derivacoes);
        return formularios.Select(formulario => CitacaoInvalidaDoFormulario(
            formulario, fatos, termos, grupos ?? [], daInscricao, dependenciasDasDerivacoes))
            .FirstOrDefault(static recusa => recusa is not null);
    }

    private static DomainError? CitacaoInvalidaDoFormulario(
        FormularioProcesso formulario, IReadOnlyCollection<FatoColetado> fatos, IReadOnlyCollection<TermoExigidoFormulario> termos,
        IReadOnlyCollection<GrupoColetado> grupos, IReadOnlySet<Guid> daInscricao,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        FatoColetado[] itens = [.. fatos.Where(f => f.FormularioId == formulario.Id)];
        HashSet<string> citaveisDaInscricao = daInscricao.Contains(formulario.Id)
            ? new(StringComparer.Ordinal)
            : PartesDoFormulario.FatosDosFormularios(fatos.Where(static f => f.GrupoColetadoId is null), daInscricao);
        return CitacaoInvalida(
            formulario.Paginas,
            itens,
            termos.Where(t => t.FormularioId == formulario.Id),
            grupos.Where(g => g.FormularioId == formulario.Id),
            DependenciasDe(itens, citaveisDaInscricao, derivacoes));
    }

    /// <summary>A primeira regra de item, de seção, de grupo ou de termo que cita fato que as dependências dadas não alcançam.</summary>
    internal static DomainError? CitacaoInvalida(
        IReadOnlyCollection<PaginaDoFormulario> paginas,
        IEnumerable<FatoColetado> itens,
        IEnumerable<TermoExigidoFormulario> termos,
        IEnumerable<GrupoColetado> grupos,
        DependenciasDoFormulario dependencias) =>
        GrafoDoFormulario.CitacaoInvalida(
            [.. itens.Select(static i => i.ParaGrafo())], [.. paginas.Select(static e => e.ParaGrafo())],
            termos.Select(static t => t.ParaGrafo()), dependencias,
            [.. grupos.Select(static g => g.ParaGrafo())]);

    // Provisório até a retirada da finalidade: quem ainda confere pela finalidade dos itens (a leitura do documento
    // congelado e a pendência dos fatos citados) segue por esta forma, que sai com a finalidade.
    public static DomainError? CitacaoInvalidaNosFormularios(
        IEnumerable<FormularioProcesso> formularios,
        IReadOnlyCollection<FatoColetado> fatos,
        IReadOnlyCollection<TermoExigidoFormulario> termos,
        IReadOnlyCollection<ConfiguracaoDerivacaoFato> derivacoes,
        IReadOnlyCollection<GrupoColetado>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(derivacoes);

        HashSet<string> daInscricao = FatosDaInscricao(fatos);
        Dictionary<string, IReadOnlyCollection<string>> dependenciasDasDerivacoes = MapaDeDerivacoes(derivacoes);
        return formularios.Select(formulario => CitacaoInvalidaDaFinalidade(
            formulario, fatos, termos, grupos ?? [], daInscricao, dependenciasDasDerivacoes))
            .FirstOrDefault(static recusa => recusa is not null);
    }

    private static DomainError? CitacaoInvalidaDaFinalidade(
        FormularioProcesso formulario, IReadOnlyCollection<FatoColetado> fatos, IReadOnlyCollection<TermoExigidoFormulario> termos,
        IReadOnlyCollection<GrupoColetado> grupos, IReadOnlySet<string> daInscricao,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        FinalidadeFormulario finalidade = formulario.Finalidade;
        FatoColetado[] itens = [.. fatos.Where(f => f.Finalidade == finalidade)];
        return CitacaoInvalida(
            formulario.Paginas,
            itens,
            termos.Where(t => t.Finalidade == finalidade),
            grupos.Where(g => g.Finalidade == finalidade),
            DependenciasDe(itens, finalidade == FinalidadeFormulario.Inscricao ? new HashSet<string>(StringComparer.Ordinal) : daInscricao, derivacoes));
    }

    internal static IEnumerable<FieldError> RecusasDosTermos(
        IReadOnlyList<TermoExigidoFormulario> termos, DependenciasDoFormulario dependencias)
    {
        for (int indice = 0; indice < termos.Count; indice++)
        {
            if (GrafoDoFormulario.CitacaoInvalidaDoTermo(termos[indice].ParaGrafo(), dependencias) is { } recusa)
            {
                yield return new FieldError($"termos[{indice}]", recusa);
            }
        }
    }

    /// <summary>
    /// O que as regras do formulário podem citar: os campos dele, na ordem; os dos formulários da
    /// inscrição, quando ele não é um deles; e os derivados por regra desses.
    /// </summary>
    internal static DependenciasDoFormulario DependenciasDe(
        IEnumerable<FatoColetado> itens,
        IReadOnlySet<string> daInscricao,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        GrafoDoFormulario.Dependencias(itens.Select(static f => f.ParaGrafo()), daInscricao, derivacoes);

    /// <summary>
    /// O que as regras do formulário declarado podem citar na configuração: o formulário da inscrição cita só os campos
    /// dele, e o outro cita também os da inscrição.
    /// </summary>
    internal static DependenciasDoFormulario DependenciasDe(
        ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado, IEnumerable<FatoColetado> itens) =>
        DependenciasDe(
            itens,
            configuracao.EhDaInscricao(declarado) ? new HashSet<string>(StringComparer.Ordinal) : configuracao.FatosDaInscricao(),
            DependenciasDasDerivacoes(configuracao));

    internal static Dictionary<string, IReadOnlyCollection<string>> DependenciasDasDerivacoes(ConfiguracaoDeclarada configuracao) =>
        MapaDeDerivacoes(configuracao.DerivacoesEfetivas());

    /// <summary>
    /// As dependências de cada derivado que uma regra do formulário pode citar: os derivados por regra
    /// do processo e os derivados do sistema, com as dependências declaradas pelo mecanismo deles.
    /// </summary>
    internal static Dictionary<string, IReadOnlyCollection<string>> MapaDeDerivacoes(IEnumerable<ConfiguracaoDerivacaoFato> derivacoes) =>
        DerivadosDoSistema.ComAsDoSistema(derivacoes.Select(static c => KeyValuePair.Create(c.CodigoFato, c.FatosCitados)));
}
