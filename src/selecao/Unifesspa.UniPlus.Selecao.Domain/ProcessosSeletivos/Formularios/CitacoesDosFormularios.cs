namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
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
    public static Result<GrafoDependenciaConjunta> ConstruirGrafoDependencia(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return GrafoDependenciaConjunta.Construir([.. estado.Itens], estado.DerivacoesEfetivas(), estado.DocumentosExigidos, estado.Formularios, estado.TermosExigidos, estado.GruposColetados);
    }

    /// <summary>Os fatos coletados pela inscrição, que os formulários das outras finalidades podem citar.</summary>
    internal static HashSet<string> FatosDaInscricao(IEnumerable<FatoColetado> fatos) =>
        new(fatos.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao).Select(static f => f.FatoCodigo), StringComparer.Ordinal);

    /// <summary>
    /// Sob retificação, a primeira citação que usa, de outra finalidade, fato que o formulário dela
    /// não coletou em todas as versões publicadas (UNI-REQ-0144): quem preencheu aquele formulário
    /// numa versão anterior pode não o ter informado. Vale para o fato citado e para todo fato de que
    /// ele depende: pela regra de derivação viva, pelo mecanismo do derivado do sistema ou, no
    /// agregado sobre grupo repetível, pelo fato de membro. Fora da sessão, nula: nenhum candidato
    /// preencheu versão anterior.
    /// </summary>
    /// <param name="citacoes">Cada fato citado, com quem o cita, como a mensagem o nomeia.</param>
    /// <param name="finalidadePropria">
    /// A finalidade de quem cita: os fatos dela seguem a versão do próprio formulário. Nula quando
    /// quem cita não tem finalidade própria, e então todo fato de formulário precisa estar garantido.
    /// </param>
    internal static DomainError? CitacaoNaoGarantida(
        EstadoDaConfiguracao estado,
        IEnumerable<(string Citante, string Citado)> citacoes,
        FinalidadeFormulario? finalidadePropria,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        if (estado.Rascunho?.FatosDasVersoesPublicadas is not { } publicadas)
        {
            return null;
        }

        foreach ((string citante, string citado) in citacoes)
        {
            if (FatoNaoGarantido(estado, citado, finalidadePropria, publicadas, membroPorAgregado, []) is { } naoGarantido)
            {
                string anterior = EstruturaFormulario.ParaToken(naoGarantido.Produtora);
                string sujeito = string.Equals(citado, naoGarantido.Fato, StringComparison.Ordinal)
                    ? $"O fato '{citado}', citado por {citante}, não"
                    : $"O fato '{citado}', citado por {citante}, depende de '{naoGarantido.Fato}', que não";
                return new DomainError(
                    "RascunhoRetificacao.FatoAusenteDeVersaoPublicada",
                    $"{sujeito} é coletado pelo formulário de {anterior} em todas as versões publicadas do edital: quem preencheu esse formulário numa versão anterior não o informou.");
            }
        }

        return null;
    }

    /// <summary>
    /// O fato coletado por outra finalidade e fora do que ela garante, entre o fato e as dependências
    /// dele. O fato coletado encerra a busca: ele não depende de outro.
    /// </summary>
    internal static (string Fato, FinalidadeFormulario Produtora)? FatoNaoGarantido(
        EstadoDaConfiguracao estado,
        string fato,
        FinalidadeFormulario? finalidadePropria,
        FatosDasVersoesPublicadas publicadas,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        HashSet<string> emAvaliacao)
    {
        if (estado.Campos.FirstOrDefault(f => string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal)) is { } coletado)
        {
            return coletado.Finalidade != finalidadePropria && !publicadas.Garante(coletado.Finalidade, fato)
                ? (fato, coletado.Finalidade)
                : null;
        }

        if (!emAvaliacao.Add(fato))
        {
            return null;
        }

        foreach (string dependencia in FaseEfetivaDoFato.DependenciasDoFato(estado, fato, membroPorAgregado))
        {
            if (FatoNaoGarantido(estado, dependencia, finalidadePropria, publicadas, membroPorAgregado, emAvaliacao) is { } naoGarantido)
            {
                return naoGarantido;
            }
        }

        return null;
    }

    /// <summary>As citações das regras de um formulário, com quem cita nomeado junto do formulário.</summary>
    internal static IEnumerable<(string Citante, string Citado)> DoFormulario(
        FinalidadeFormulario finalidade, IEnumerable<(string Dono, string Citado)> citacoes) =>
        citacoes.Select(c => ($"{c.Dono} do formulário de {EstruturaFormulario.ParaToken(finalidade)}", c.Citado));

    /// <summary>
    /// Sob retificação, a regra de formulário que usa, de outra finalidade, fato fora do que o
    /// formulário dela coletou em todas as versões publicadas. A conferência das escritas alcança o que
    /// cada uma escreve; esta alcança o que mudou depois, inclusive a regra de derivação que passou a
    /// fazer um derivado citado depender de fato novo.
    /// </summary>
    internal static DomainError? PendenciaDeFatoAusenteDeVersaoPublicada(EstadoDaConfiguracao estado) =>
        estado.Formularios
            .Select(formulario => CitacaoNaoGarantida(
                estado,
                DoFormulario(
                    formulario.Finalidade,
                    CitacoesDoFormulario(
                        estado,
                        formulario,
                        [.. estado.Itens.Where(f => f.Finalidade == formulario.Finalidade)],
                        [.. GruposDaFinalidade(estado, formulario.Finalidade)])
                    .Select(static c => (c.Dono, c.Citado))),
                formulario.Finalidade,
                SemCatalogo))
            .FirstOrDefault(static recusa => recusa is not null);

    /// <summary>
    /// Trocar os itens da inscrição não pode tirar um fato que uma regra de outra finalidade cita,
    /// direta ou por um derivado: ela passaria a depender de uma resposta que ninguém pede. Só a
    /// citação que a troca invalida é recusada; a que já era inválida fica para a publicação.
    /// </summary>
    internal static DomainError? CitacaoQueFicariaOrfa(EstadoDaConfiguracao estado, IReadOnlyCollection<FatoColetado> novosDaInscricao) =>
        CitacoesQueFicariamOrfas(
            estado,
            // Os itens novos ainda não têm a finalidade vinculada: valem pelo código.
            new HashSet<string>(novosDaInscricao.Select(static f => f.FatoCodigo), StringComparer.Ordinal),
            outra => [.. estado.Itens.Where(f => f.Finalidade == outra)],
            DependenciasDasDerivacoes(estado)).FirstOrDefault();

    /// <summary>
    /// As citações das outras finalidades que uma mudança na inscrição, nos itens delas ou nas
    /// derivações deixaria sem o fato citado: compara cada citação antes e depois e devolve só as
    /// que a mudança invalida.
    /// </summary>
    internal static IEnumerable<DomainError> CitacoesQueFicariamOrfas(
        EstadoDaConfiguracao estado,
        IReadOnlySet<string> daInscricaoDepois,
        Func<FinalidadeFormulario, FatoColetado[]> itensDepois,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoesDepois)
    {
        HashSet<string> atuais = FatosDaInscricao(estado.Itens);
        Dictionary<string, IReadOnlyCollection<string>> derivacoesAntes = DependenciasDasDerivacoes(estado);
        foreach (FormularioProcesso formulario in estado.Formularios.Where(static f => f.Finalidade != FinalidadeFormulario.Inscricao))
        {
            FinalidadeFormulario outra = formulario.Finalidade;
            FatoColetado[] itens = itensDepois(outra);
            GrupoColetado[] grupos = [.. GruposDaFinalidade(estado, outra)];
            DependenciasDoFormulario antes = DependenciasDe(outra, estado.Itens.Where(f => f.Finalidade == outra), atuais, derivacoesAntes);
            DependenciasDoFormulario depois = DependenciasDe(outra, itens, daInscricaoDepois, derivacoesDepois);
            foreach ((string dono, long posicao, string citado) in CitacoesDoFormulario(estado, formulario, itens, grupos))
            {
                if (antes.Conferir(citado, posicao) is null && depois.Conferir(citado, posicao) is not null)
                {
                    yield return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                        $"O fato '{citado}', citado por {dono} do formulário de {EstruturaFormulario.ParaToken(outra)}, deixaria de ser conhecido por ele.");
                }
            }
        }
    }

    /// <summary>
    /// Cada citação das regras do formulário, com quem cita e a posição de onde cita: as dos itens e
    /// dos grupos dados, a exibição das seções e as condições dos termos da finalidade.
    /// </summary>
    internal static IEnumerable<(string Dono, long Posicao, string Citado)> CitacoesDoFormulario(
        EstadoDaConfiguracao estado,
        FormularioProcesso formulario, FatoColetado[] itens, GrupoColetado[] grupos) =>
        CitacoesDosCampos(itens, grupos)
            .Concat(formulario.Etapas.SelectMany(secao => CitacoesDaSecao(secao)
                .Select(c => (c.Dono,
                    GrafoDoFormulario.PosicaoDaEtapa(
                        secao.ParaGrafo(),
                        formulario.Etapas.Select(static e => e.ParaGrafo()),
                        itens.Select(static i => i.ParaGrafo()),
                        grupos.Select(static g => g.ParaGrafo())),
                    c.Citado))))
            .Concat(estado.TermosExigidos.Where(t => t.Finalidade == formulario.Finalidade)
                .SelectMany(static termo => CitacoesDoTermo(termo).Select(static c => (c.Dono, DependenciasDoFormulario.PosicaoDosTermos, c.Citado))));

    /// <summary>As citações das regras dos itens e dos grupos, cada uma na posição do item ou do grupo que cita.</summary>
    internal static IEnumerable<(string Dono, long Posicao, string Citado)> CitacoesDosCampos(
        IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        itens.SelectMany(static item => item.FatosCitados.Select(c => ($"o item '{item.FatoCodigo}'", (long)item.Ordem, c)))
            .Concat(grupos.SelectMany(static grupo => CitacoesDeFora(grupo).Select(c => ($"o grupo '{grupo.Codigo}'", (long)grupo.Ordem, c))));

    internal static IEnumerable<(string Dono, string Citado)> CitacoesDaSecao(EtapaFormulario secao) =>
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
        IReadOnlyCollection<EtapaFormulario> etapas,
        IReadOnlyList<FatoColetado> fatos,
        IReadOnlyList<GrupoColetado> grupos,
        HashSet<string> daInscricao,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        GrafoDoFormulario.ValidarColeta(
            [.. fatos.Select(static f => f.ParaGrafo())],
            [.. etapas.Select(static e => e.ParaGrafo())],
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

    /// <summary>Os grupos repetíveis do formulário da finalidade.</summary>
    internal static IEnumerable<GrupoColetado> GruposDaFinalidade(EstadoDaConfiguracao estado, FinalidadeFormulario finalidade) =>
        estado.GruposColetados.Where(g => g.Finalidade == finalidade);

    /// <summary>A quantidade que o teto do formulário conta: cada item, cada grupo e cada campo de grupo.</summary>
    internal static int QuantidadeNoTeto(int itens, IEnumerable<GrupoColetado> grupos) =>
        FormaDoItem.QuantidadeNoTeto(itens, grupos.Select(static g => g.Subitens.Count));

    /// <summary>Os itens e os grupos, na forma que a estrutura confere: o grupo ocupa uma posição na ordem dos itens.</summary>
    internal static IReadOnlyList<ItemEstrutura> ParaEstrutura(IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        [.. itens.Select(static f => new ItemEstrutura(f.FatoCodigo, f.Ordem, f.EtapaCodigo)), .. grupos.Select(static g => g.ParaEstrutura())];

    /// <summary>
    /// A primeira regra de item ou condição de termo que cita fato que o formulário dela não
    /// conhece. É a mesma conferência da definição, repetida na publicação e na leitura do edital
    /// congelado.
    /// </summary>
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
        Dictionary<string, IReadOnlyCollection<string>> dependenciasDasDerivacoes =
            MapaDeDerivacoes(derivacoes);
        foreach (FormularioProcesso formulario in formularios)
        {
            FinalidadeFormulario finalidade = formulario.Finalidade;
            FatoColetado[] itens = [.. fatos.Where(f => f.Finalidade == finalidade)];
            DependenciasDoFormulario dependencias = DependenciasDe(finalidade, itens, daInscricao, dependenciasDasDerivacoes);
            if (GrafoDoFormulario.CitacaoInvalida(
                    [.. itens.Select(static i => i.ParaGrafo())],
                    [.. formulario.Etapas.Select(static e => e.ParaGrafo())],
                    termos.Where(t => t.Finalidade == finalidade).Select(static t => t.ParaGrafo()),
                    dependencias,
                    [.. (grupos ?? []).Where(g => g.Finalidade == finalidade).Select(static g => g.ParaGrafo())]) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    /// <summary>
    /// O que as regras do formulário da finalidade podem citar: os campos dele, na ordem; os da
    /// inscrição, quando o formulário é de outra finalidade; e os derivados por regra desses.
    /// </summary>
    internal static DependenciasDoFormulario DependenciasDe(
        EstadoDaConfiguracao estado,
        FinalidadeFormulario finalidade, IEnumerable<FatoColetado> itens, IReadOnlySet<string> daInscricao) =>
        DependenciasDe(finalidade, itens, daInscricao, DependenciasDasDerivacoes(estado));

    internal static DependenciasDoFormulario DependenciasDe(
        FinalidadeFormulario finalidade,
        IEnumerable<FatoColetado> itens,
        IReadOnlySet<string> daInscricao,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        GrafoDoFormulario.Dependencias(
            itens.Select(static f => f.ParaGrafo()),
            finalidade == FinalidadeFormulario.Inscricao ? new HashSet<string>(StringComparer.Ordinal) : daInscricao,
            derivacoes);

    internal static Dictionary<string, IReadOnlyCollection<string>> DependenciasDasDerivacoes(EstadoDaConfiguracao estado) =>
        MapaDeDerivacoes(estado.DerivacoesEfetivas());

    /// <summary>
    /// As dependências de cada derivado que uma regra do formulário pode citar: os derivados por regra
    /// do processo e os derivados do sistema, com as dependências declaradas pelo mecanismo deles.
    /// </summary>
    internal static Dictionary<string, IReadOnlyCollection<string>> MapaDeDerivacoes(IEnumerable<ConfiguracaoDerivacaoFato> derivacoes) =>
        DerivadosDoSistema.ComAsDoSistema(derivacoes.Select(static c => KeyValuePair.Create(c.CodigoFato, c.FatosCitados)));
}
